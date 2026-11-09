//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Native;
using Telegram.Services;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace Telegram.Views
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class DebugPage : Page
    {
        private DispatcherTimer _memoryUsageTimer;
        private double _memoryUsage;
        private double _sharedCommitUsage;
        private double _videoMemoryUsage;
        private double _sharedMemoryUsage;
        private uint _handles;

        // Leak watch. A step this big in the SETTLED heap is worth interrupting for; smaller ones
        // are indistinguishable from a big chat being opened.
        private const long MemoryStepThreshold = 30 * 1024 * 1024;
        private long _memoryFloor;
        private int _memoryGen2 = -1;
        private bool _memorySettling;

        public DebugPage()
        {
            this.InitializeComponent();

            var butt = new RepeatButton();
            butt.HorizontalAlignment = HorizontalAlignment.Center;
            butt.Content = "Force GC";
            butt.Click += (s, args) =>
            {
                CollectAndAnalyze();
            };

            //var swit = new Button();
            //swit.HorizontalAlignment = HorizontalAlignment.Center;
            //swit.Content = "SW";
            //swit.Click += async (s, args) =>
            //{
            //    for (int i = 0; i < 10; i++)
            //    {
            //        Switch(_lifetime.ActiveItem);
            //        await Task.Delay(2000);
            //    }

            //    TestDestroy();
            //};

            //var clin = new Button();
            //clin.HorizontalAlignment = HorizontalAlignment.Center;
            //clin.Content = "CL";
            //clin.Click += (s, args) =>
            //{
            //    var butt = new RepeatButton();
            //    butt.HorizontalAlignment = HorizontalAlignment.Center;
            //    butt.Content = "GC";
            //    butt.Click += (s, args) =>
            //    {
            //        GC.Collect();
            //        GC.WaitForPendingFinalizers();
            //        GC.Collect();
            //    };

            //    Window.Content = butt;
            //};

            //var rest = new Button();
            //rest.HorizontalAlignment = HorizontalAlignment.Center;
            //rest.Content = "RE";
            //rest.Click += (s, args) =>
            //{
            //    Switch(_lifetime.ActiveItem);
            //};

            LayoutRoot.Children.Add(butt);
            //LayoutRoot.Children.Add(swit);
            //LayoutRoot.Children.Add(clin);
            //LayoutRoot.Children.Add(rest);

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private static Task CollectAsync()
        {
            // Off the view thread, or this deadlocks. A finalizing RCW releases through
            // IContextCallback back into the apartment that created it, and an ASTA will not
            // dispatch an incoming call while it is blocked in a wait - so waiting for the
            // finalizers here would block the one thread the finalizer needs.
            return Task.Run(static () =>
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
            });
        }

        private async void CollectAndAnalyze()
        {
            await CollectAsync();

            // After the collection, so the surfaces it finalized are released first. DComp keeps a
            // released surface's atlas region, and with it the page it sits in, until its graphics
            // device is trimmed, and Direct3D what it released until its context is flushed.
            Direct2D.Current.Device.Trim();
            Direct2D.Current.Trim();

            Logger.Info(WindowContent.DebugCounters());

#if INSTRUMENTATION
            Logger.Info(await DebugAnalyzeOrphans());
            Profiler.Report();
#endif

#if !NET9_0_OR_GREATER
            GarbageCollectionMonitor.DisconnectUnusedReferenceSources();
#endif
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (AppSettings.Diagnostics.ShowMemoryUsage)
            {
                _memoryUsageTimer = new DispatcherTimer();
                _memoryUsageTimer.Interval = TimeSpan.FromSeconds(1);
                _memoryUsageTimer.Tick += MemoryUsageTimer_Tick;
                _memoryUsageTimer.Start();

                MemoryUsageTimer_Tick(null, null);
            }
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            _memoryUsageTimer?.Tick -= MemoryUsageTimer_Tick;
            _memoryUsageTimer?.Stop();
        }

        private readonly int[] _lastCollectionCount = new int[3]
        {
            GC.CollectionCount(0),
            GC.CollectionCount(1),
            GC.CollectionCount(2)
        };

#if NET9_0_OR_GREATER
        private TimeSpan _lastPauseDuration = GC.GetTotalPauseDuration();
        private long _lastCollectionIndex = GC.GetGCMemoryInfo().Index;
#endif

        /// <summary>
        /// How long the process spent suspended for collection since the last tick, and how long
        /// in total. A collection suspends every managed thread, so this is the UI thread's share
        /// of it as much as anyone's - what a frame that never rendered was doing.
        /// </summary>
        /// <remarks>
        /// Four nanoseconds to ask, against the twenty-seven GetTotalMemory above it costs, so at
        /// one hertz it is free. Not on .NET Native, where neither this nor GCMemoryInfo exists -
        /// the collection counts in PollGC are what that build has.
        /// </remarks>
        private string PollPause()
        {
#if NET9_0_OR_GREATER
            var total = GC.GetTotalPauseDuration();
            var elapsed = total - _lastPauseDuration;

            _lastPauseDuration = total;

            if (elapsed > TimeSpan.Zero)
            {
                // Which collection it was, and whether it stopped the world: a background gen2
                // suspends twice for a moment, where a blocking one is the whole pause. Only the
                // last collection is described, so a tick that saw two reports the later one.
                var info = GC.GetGCMemoryInfo();
                var collections = info.Index - _lastCollectionIndex;

                _lastCollectionIndex = info.Index;

                return string.Format(", {0:N0}/{1:N0} ms GC ({2}gen{3}{4})",
                    elapsed.TotalMilliseconds,
                    total.TotalMilliseconds,
                    collections > 1 ? collections + "x " : string.Empty,
                    info.Generation,
                    info.Concurrent ? string.Empty : " blocking");
            }

            return string.Format(", {0:N0} ms GC", total.TotalMilliseconds);
#else
            return string.Empty;
#endif
        }

        private string PollGC()
        {
#if WINUI3
            // GarbageCollectionMonitor is not built for this flavour: it tracks CoreWindow
            // activation and the UWP suspend/resume events, and has neither here. The other two
            // call sites are already behind #if !NET9_0_OR_GREATER, so this is the only one.
            var occurred = string.Empty;
#else
            var occurred = GarbageCollectionMonitor.Debug();
#endif
            var first = true;

            for (int i = 0; i <= 2; i++)
            {
                int count = GC.CollectionCount(i);
                if (count != _lastCollectionCount[i])
                {
                    _lastCollectionCount[i] = count;

                    if (first)
                        occurred += " (";

                    first = false;
                    occurred += i.ToString();
                }
            }

            if (!first)
                occurred += ")";

            return occurred;
        }

        private void MemoryUsageTimer_Tick(object sender, object e)
        {
            // AppMemoryUsage, the two commits summed, cannot tell a composition surface leak from a
            // heap one: surfaces are shared with DWM, so they land in the shared commit, each with a
            // handle.
            NativeUtils.GetMemoryUsage(out ulong privateCommit, out ulong sharedCommit, out ulong dedicatedVideo, out ulong sharedVideo, out uint handles);

            var memoryUsage = Math.Round(privateCommit / 1024.0 / 1024.0);
            var sharedCommitUsage = Math.Round(sharedCommit / 1024.0 / 1024.0);
            var videoMemoryUsage = Math.Round(dedicatedVideo / 1024.0 / 1024.0);
            var sharedMemoryUsage = Math.Round(sharedVideo / 1024.0 / 1024.0);
            //var occurred = PollGC();
            var paused = PollPause();

            //double unmanaged = currentProcess.NativeHeap / 1024.0 / 1024.0;
            double managed = GC.GetTotalMemory(false) / 1024.0 / 1024.0; // currentProcess.ManagedHeap / 1024.0 / 1024.0;

            if (memoryUsage != _memoryUsage || sharedCommitUsage != _sharedCommitUsage || videoMemoryUsage != _videoMemoryUsage || sharedMemoryUsage != _sharedMemoryUsage || handles != _handles)
            {
                MemoryLabel.Text = $"- {memoryUsage + sharedCommitUsage:F0} MB ({memoryUsage:F0} private, {sharedCommitUsage:F0} shared), resident {videoMemoryUsage:F0} VRAM + {sharedMemoryUsage:F0} RAM, {handles} handles, {managed:F0} MB" + paused /*+ occurred*/ + WindowContent.DebugInline();
            }

            _memoryUsage = memoryUsage;
            _sharedCommitUsage = sharedCommitUsage;
            _handles = handles;
            _videoMemoryUsage = videoMemoryUsage;
            _sharedMemoryUsage = sharedMemoryUsage;

            DetectMemoryStep();
        }

        /// <summary>
        /// Notices the managed heap stepping up and staying there, which is the shape of a leak
        /// rather than of garbage. GetTotalMemory(false) once a second is mostly uncollected
        /// garbage and a step in it means nothing, so this samples only after a gen2 collection -
        /// a settled number, for free, without forcing anything.
        /// </summary>
        private void DetectMemoryStep()
        {
            var gen2 = GC.CollectionCount(2);
            if (gen2 != _memoryGen2)
            {
                // Background GC bumps the count when the collection STARTS, so the heap is only
                // settled a tick later.
                _memoryGen2 = gen2;
                _memorySettling = true;
                return;
            }

            if (!_memorySettling)
            {
                return;
            }

            _memorySettling = false;

            var settled = GC.GetTotalMemory(false);

            if (_memoryFloor == 0 || settled < _memoryFloor)
            {
                _memoryFloor = settled;
                return;
            }

            if (settled - _memoryFloor < MemoryStepThreshold)
            {
                return;
            }

            // The page is the only part of this worth reading: the timestamp says WHEN, and this
            // says where the app was when it happened.
            var message = string.Format("Managed heap {0:F0} MB -> {1:F0} MB over {2} gen2 collections, on {3}",
                _memoryFloor / 1024d / 1024d,
                settled / 1024d / 1024d,
                gen2, "no page");

            // Re-arm at the new level, so the NEXT step of the same size reports too instead of
            // every tick from here on.
            _memoryFloor = settled;

            if (_memoryFloor / 1024d / 1024d > 60)
            {
                // Logger.Error and not TrackError: nothing crashed, and a crash report would say
                // otherwise. This exists to put a timestamp in the log that the tail can be read
                // around - a timer tick's stack would say nothing.
                Logger.Error(message);
                ToastPopup.Show(XamlRoot, message, ToastPopupIcon.Error);
            }
        }

    }
}
