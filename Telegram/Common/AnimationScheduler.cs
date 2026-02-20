//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Telegram.Collections;

namespace Telegram.Common
{
    public interface IAnimation
    {
        double FrameRate { get; }

        void RenderNextFrame();
    }

    public partial class AnimationScheduler : IDisposable
    {
        private class AnimationBatch
        {
            public double FrameRate { get; init; }
            public double Interval { get; init; }
            public double MaxExecution { get; init; }

            public LockFreeArrayList<IAnimation> Animations { get; init; }
            public Timer Timer { get; set; }
            public Stopwatch ExecutionTimer { get; } = new Stopwatch();

            public AnimationBatch(double frameRate)
            {
                FrameRate = frameRate;
                Interval = 1000.0 / frameRate;
                MaxExecution = 1000.0 / frameRate * 0.8; // 0.75?
                Animations = new LockFreeArrayList<IAnimation>(GetBatchSize(frameRate));
            }

            private AnimationBatch(double frameRate, double interval, double maxExecution, LockFreeArrayList<IAnimation> animations)
            {
                FrameRate = frameRate;
                Interval = interval;
                MaxExecution = maxExecution;
                Animations = animations;
            }

            private static int GetBatchSize(double size)
            {
                // Maybe needs some more tuning.
                const double a = 400.0;
                const double b = 0.8;
                int batch = (int)Math.Round(a * Math.Pow(size, -b));
                return Math.Clamp(batch, 1, 50);
            }

            public AnimationBatch SplitHalf()
            {
                return new AnimationBatch(FrameRate, Interval, MaxExecution, Animations.SplitHalf());
            }
        }

        private readonly ConcurrentDictionary<IAnimation, byte> _allAnimations = new();
        private readonly ConcurrentDictionary<double, List<AnimationBatch>> _batchesByFps = new();
        private readonly object _batchLock = new();
        private bool _disposed;

        public AnimationScheduler()
        {
        }

        public void Subscribe(IAnimation animation)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(AnimationScheduler));
            if (animation == null) throw new ArgumentNullException(nameof(animation));

            if (_allAnimations.TryAdd(animation, 0))
            {
                lock (_batchLock)
                {
                    AssignAnimationToBatch(animation);
                }
            }
        }

        public void Unsubscribe(IAnimation animation)
        {
            if (_allAnimations.TryRemove(animation, out _))
            {
                lock (_batchLock)
                {
                    RemoveAnimationFromBatches(animation);
                }
            }
        }

        public int ActiveAnimationCount => _allAnimations.Count;

        public string GetBatchingStats()
        {
            lock (_batchLock)
            {
                var stats = new List<string>();
                foreach (var kvp in _batchesByFps.OrderByDescending(x => x.Key))
                {
                    var fps = kvp.Key;
                    var batches = kvp.Value;
                    var totalAnims = batches.Sum(b => b.Animations.Count);
                    stats.Add($"{fps}fps: {totalAnims} animations in {batches.Count} batch(es)");
                }
                return string.Join("\n", stats);
            }
        }

        private void AssignAnimationToBatch(IAnimation animation)
        {
            var fps = animation.FrameRate;
            var batches = _batchesByFps.GetOrAdd(fps, _ => new List<AnimationBatch>());

            // Try to find a batch with room
            // TODO: too small batches?
            var targetBatch = batches.FirstOrDefault(b => b.Animations.Count < b.Animations.Capacity);

            if (targetBatch == null)
            {
                targetBatch = new AnimationBatch(fps);
                batches.Add(targetBatch);

                var interval = (int)Math.Max(1, targetBatch.Interval);
                targetBatch.Timer = new Timer(ExecuteBatch, targetBatch, 0, interval);
            }

            targetBatch.Animations.Add(animation);
        }

        private void RemoveAnimationFromBatches(IAnimation animation)
        {
            var fps = animation.FrameRate;
            if (_batchesByFps.TryGetValue(fps, out var batches))
            {
                for (int i = 0; i < batches.Count; i++)
                {
                    AnimationBatch batch = batches[i];
                    batch.Animations.Remove(animation);

                    if (batch.Animations.Count == 0)
                    {
                        batch.Timer?.Dispose();
                        batches.RemoveAt(i);
                        i--;
                    }
                }

                if (batches.Count == 0)
                {
                    _batchesByFps.TryRemove(fps, out _);
                }
            }
        }

        private void ExecuteBatch(object state)
        {
            if (_disposed || state is not AnimationBatch batch) return;

            batch.ExecutionTimer.Restart();

            if (batch.Animations.TrySnapshot(out var animations, out int length))
            {
                for (int i = 0; i < length; i++)
                {
                    try
                    {
                        animations[i].RenderNextFrame();
                    }
                    catch (Exception ex)
                    {
                        // Log exception but don't let one animation crash the batch
                        Logger.Info($"Animation threw exception: {ex.Message}");
                    }

                    // Check if we're exceeding our time budget
                    if (batch.ExecutionTimer.ElapsedMilliseconds > batch.MaxExecution)
                    {
                        // Mark for rebalancing
                        ThreadPool.QueueUserWorkItem(RebalanceBatch, batch);
                        break;
                    }
                }

                ArrayPool<IAnimation>.Shared.Return(animations, true);
            }

            batch.ExecutionTimer.Stop();
        }

        private void RebalanceBatch(object state)
        {
            lock (_batchLock)
            {
                // If batch is now small enough, no need to rebalance
                if (state is not AnimationBatch overloadedBatch || overloadedBatch.Animations.Count <= 2) return;

                var fps = overloadedBatch.FrameRate;
                var newBatch = overloadedBatch.SplitHalf();

                var batches = _batchesByFps[fps];
                batches.Add(newBatch);

                // Start timer for new batch
                var interval = (int)Math.Max(1, newBatch.Interval);
                newBatch.Timer = new Timer(ExecuteBatch, newBatch, interval, interval);

                Logger.Info($"Rebalanced {fps}fps batch: split into {overloadedBatch.Animations.Count} and {newBatch.Animations.Count} animations");
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            lock (_batchLock)
            {
                foreach (var batchList in _batchesByFps.Values)
                {
                    foreach (var batch in batchList)
                    {
                        batch.Timer?.Dispose();
                    }
                }
                _batchesByFps.Clear();
            }

            _allAnimations.Clear();
        }
    }
}
