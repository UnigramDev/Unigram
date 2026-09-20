//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using Telegram.Common;
using Telegram.Composition;
using Telegram.Controls.Media;
using Telegram.Native;
using Telegram.Navigation;
using Telegram.Services;
using Telegram.Td.Api;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media;

namespace Telegram.Controls.Stories
{
    // TODO: Rewrite to use plain animations without rendering callback.
    // Both axes are closed form in time - X is baseX + amplitude * sin(2pi * (t / period + phase)),
    // Y is linear - so an expression animation over a shared time property set would do the whole
    // thing on the compositor. The integration below would have to become that closed form first.
    public partial class StoryReactionStream : Canvas
    {
        private sealed partial class ItemLayer
        {
            private readonly StoryReactionStream _owner;
            private readonly UIElement _image;
            private readonly Visual _visual;
            private readonly uint _id;

            private readonly TypedEventHandler<object, CompositionBatchCompletedEventArgs> _batchCompleted;
            private CompositionScopedBatch _batch;

            public readonly float Amplitude;
            public readonly float Period;
            public readonly float PhaseOffset;
            public readonly float BaseX;
            public readonly float VerticalVelocity;

            public float TimeValue;

            // Nothing else writes the offset, so the position is kept here rather than read back:
            // the getter would otherwise be a second call across the ABI per item per frame.
            private Vector2 _position;

            public ItemLayer(StoryReactionStream owner, uint id, UIElement image, float amplitude, float period, float phaseOffset, float baseX, float verticalVelocity)
            {
                _owner = owner;
                _id = id;
                _image = image;
                _visual = ElementComposition.GetElementVisual(image);
                _batchCompleted = OnBatchCompleted;

                Amplitude = amplitude;
                Period = period;
                PhaseOffset = phaseOffset;
                BaseX = baseX;
                VerticalVelocity = verticalVelocity;
            }

            public Vector2 Position
            {
                get => _position;
                set
                {
                    _position = value;
                    _visual.Offset = new Vector3(value, 0);
                }
            }

            /// <summary>
            /// In radians, which is what the physics computes and what the layer transform this was
            /// ported from takes. Writing it to RotationAngleInDegrees, as this used to, turned a
            /// sway of about 14 degrees into a quarter of one.
            /// </summary>
            public float RotationAngle
            {
                set => _visual.RotationAngle = value;
            }

            public void Play(float duration)
            {
                var compositor = _visual.Compositor;
                var delay = duration - 0.1f - 0.18f;

                // The key frames land on progresses derived from this item's own duration, so these
                // two cannot be shared templates the way the level animations elsewhere are.
                var scale = compositor.CreateVector3KeyFrameAnimation();
                scale.InsertKeyFrame(0, new Vector3(0.001f));
                scale.InsertKeyFrame(0.2f / duration, new Vector3(1));
                scale.InsertKeyFrame(delay / duration, new Vector3(1));
                scale.InsertKeyFrame(1, new Vector3(0.001f));
                scale.Duration = TimeSpan.FromSeconds(duration);

                var alpha = compositor.CreateScalarKeyFrameAnimation();
                alpha.InsertKeyFrame(0, 0);
                alpha.InsertKeyFrame(0.1f / duration, 1);
                alpha.InsertKeyFrame(delay / duration, 1);
                alpha.InsertKeyFrame(1, 0);
                alpha.Duration = TimeSpan.FromSeconds(duration);

                _batch = compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
                _batch.Completed += _batchCompleted;

                _visual.CenterPoint = new Vector3(_image.ActualSize / 2, 0);
                _visual.StartAnimation("Scale", scale);
                _visual.StartAnimation("Opacity", alpha);

                _batch.End();
            }

            private void OnBatchCompleted(object sender, CompositionBatchCompletedEventArgs args)
            {
                if (_batch != null)
                {
                    _batch.Completed -= _batchCompleted;
                    _batch.Dispose();
                    _batch = null;
                }

                _owner.Remove(_id, _image);
            }
        }

        private readonly Dictionary<uint, ItemLayer> _itemLayers = [];
        private readonly CompositionVSync _displayLink = new(60);

        private uint _nextId;

        private long _timestamp;
        private long _physicsTimestamp;

        private bool _rendering;

        public StoryReactionStream()
        {
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            // Badges animate on the compositor whether or not this is in the tree, so one that comes
            // back with items still in flight has to pick the tick up again.
            UpdateRendering(_itemLayers.Count > 0);
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            UpdateRendering(false);
        }

        private void OnRendering(object sender, EventArgs e)
        {
            UpdatePhysics();
        }

        private void UpdateRendering(bool rendering)
        {
            if (_rendering == rendering)
            {
                return;
            }

            _rendering = rendering;

            if (rendering)
            {
                _physicsTimestamp = Stopwatch.GetTimestamp();
                _displayLink.Rendering += OnRendering;
            }
            else
            {
                _displayLink.Rendering -= OnRendering;
            }
        }

        public void Add(IClientService clientService, MessageSender senderId, long count)
        {
            var timestamp = Stopwatch.GetTimestamp();
            if (timestamp - _timestamp < Stopwatch.Frequency / 5)
            {
                return;
            }

            _timestamp = timestamp;

            var image = CreateBadge(clientService, senderId, count);

            void handler(object sender, object e)
            {
                image.Loaded -= handler;
                AddRenderedItem(image);
            }

            image.Loaded += handler;
            Children.Add(image);

            UpdateRendering(true);
        }

        private FrameworkElement CreateBadge(IClientService clientService, MessageSender senderId, long count)
        {
            var photo = new ProfilePicture
            {
                Source = ProfilePictureSource.MessageSender(clientService, senderId),
                Size = 16
            };

            var text = new TextBlock
            {
                Text = string.Format("{0} {1}", Icons.Premium, count),
                FontFamily = BootStrapper.Current.Resources["EmojiThemeFontFamilyWithSymbols"] as FontFamily,
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(4, 0, 2, 1)
            };

            Grid.SetColumn(text, 1);

            var root = new Grid
            {
                Background = new SolidColorBrush(Color.FromArgb(0xFF, 0xE8, 0xAB, 0x02)),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(2)
            };

            root.ColumnDefinitions.Add(1, GridUnitType.Auto);
            root.ColumnDefinitions.Add(1, GridUnitType.Auto);

            root.Children.Add(photo);
            root.Children.Add(text);

            return root;
        }

        private void AddRenderedItem(UIElement image)
        {
            var id = _nextId;
            _nextId += 1;

            if (image is FrameworkElement element)
            {
                element.Margin = new Thickness(0, 0, -element.ActualWidth, -20);
            }

            // The draws from this generator are order dependent: amplitude, period and vertical
            // velocity are pulled by the argument list, and the duration below has to come last.
            var random = new LokiRng(seed0: id, seed1: 1, seed2: 0);
            var itemX = -image.ActualSize.X - 8.0f + 20.0f * (LokiRng.Random(withSeed0: id, seed1: 0, seed2: 0) - 0.5f);
            var phaseOffset = random.Next();
            var itemLayer = new ItemLayer(this, id, image, 0.0f + random.Next() * 6.0f, 1.5f + random.Next() * 2.0f, phaseOffset, itemX, -(1.0f + random.Next() * 0.2f) * 90.0f);

            itemLayer.Position = new Vector2(itemX, -20 * 0.5f);
            _itemLayers[id] = itemLayer;

            itemLayer.Play(1.2f + random.Next() * 0.8f);
        }

        private void Remove(uint id, UIElement image)
        {
            _itemLayers.Remove(id);
            Children.Remove(image);

            if (Children.Empty())
            {
                UpdateRendering(false);
            }
        }

        private void UpdatePhysics()
        {
            // GetTickCount64 divided into a float was both too coarse to see a frame - it advances
            // on the ~15.6ms timer tick - and, past a couple of days of uptime, unable to represent
            // one at all: the float ulp at 2^19 seconds is already larger than the 1/30 ceiling.
            // The delta is taken in raw ticks and converted once, so neither depends on uptime.
            var timestamp = Stopwatch.GetTimestamp();
            var elapsed = (float)((timestamp - _physicsTimestamp) / (double)Stopwatch.Frequency);
            var dt = Math.Clamp(elapsed, 1.0f / 120.0f, 1.0f / 30.0f);

            _physicsTimestamp = timestamp;

            foreach (var itemLayer in _itemLayers.Values)
            {
                itemLayer.TimeValue += dt;
                var itemPhase = MathF.IEEERemainder((MathF.IEEERemainder(itemLayer.TimeValue, itemLayer.Period) / itemLayer.Period + itemLayer.PhaseOffset), 1.0f);
                var phaseAngle = itemPhase * MathF.PI * 2.0f;
                var phaseFraction = MathF.Sin(phaseAngle);

                var newX = itemLayer.BaseX + phaseFraction * itemLayer.Amplitude;
                var newY = itemLayer.Position.Y + itemLayer.VerticalVelocity * dt;
                itemLayer.Position = new Vector2(x: newX, y: newY);

                var horizontalVelocity = itemLayer.Amplitude * MathF.Cos(phaseAngle) * (MathF.PI * 2.0f / itemLayer.Period);
                var rotationAngle = MathF.Atan2(itemLayer.VerticalVelocity, horizontalVelocity) + MathF.PI * 0.5f;
                itemLayer.RotationAngle = rotationAngle;
            }
        }
    }
}
