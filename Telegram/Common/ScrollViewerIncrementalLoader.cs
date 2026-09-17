//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Threading;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Data;

namespace Telegram.Common
{
    /// <summary>
    /// Keeps the tail of a scrolling view loaded: while the view is near the end of what it has,
    /// and the source says there is more, it asks for more. The one-directional counterpart of
    /// <see cref="Controls.Chats.BidirectionalIncrementalLoader"/>, and unlike that one it knows
    /// nothing about chats, or about lists.
    /// </summary>
    /// <remarks>
    /// It drives a <see cref="ScrollViewer"/> rather than a list because the two are not always
    /// the same thing: a page that scrolls as a whole owns the scroll viewer, and the list inside
    /// it is one piece of content among several - and may be replaced. So the source is handed in
    /// through <see cref="ItemsSource"/> rather than read back off a list.
    ///
    /// A list left to itself only asks for more while its viewport is unfilled, which stops being
    /// true the moment one page covers it. The threshold below is a distance from the end rather
    /// than an edge, so the next page is already on its way by the time the end comes into view.
    /// </remarks>
    public partial class ScrollViewerIncrementalLoader
    {
        // A load that adds nothing while still reporting more would otherwise be asked forever:
        // there is nothing to scroll, so no view change ever comes along to stop the cycle.
        private const int MaxConsecutiveSizeChangedChecks = 20;

        private readonly ScrollViewer _scrollViewer;

        private readonly uint _pageSize;
        private readonly double _baseTriggerThreshold;
        private readonly double _minThresholdMultiplier;
        private readonly double _maxThresholdMultiplier;
        private readonly TimeSpan _checkInterval;

        // What the scroll viewer scrolls. Its height is the extent, so its SizeChanged is how a
        // page that arrived without making the view scrollable is noticed.
        private FrameworkElement _content;

        private object _itemsSource;
        private ISupportIncrementalLoading _source;

        private bool _isMonitoring;
        private CancellationTokenSource _cts;

        // Everything here runs on the UI thread - the events that drive it are raised there, and
        // the awaits resume there - so none of this counting needs to be interlocked.
        private int _activeLoadOperations;
        private int _consecutiveSizeChangedChecks;

        private DateTime _lastCheckTime = DateTime.MinValue;

        public ScrollViewerIncrementalLoader(
            ScrollViewer scrollViewer,
            uint pageSize = 20,
            double baseTriggerThreshold = 800.0,
            double minThresholdMultiplier = 0.5,
            double maxThresholdMultiplier = 2.0,
            TimeSpan? checkInterval = null)
        {
            _scrollViewer = scrollViewer ?? throw new ArgumentNullException(nameof(scrollViewer));

            _pageSize = pageSize;
            _baseTriggerThreshold = baseTriggerThreshold;
            _minThresholdMultiplier = minThresholdMultiplier;
            _maxThresholdMultiplier = maxThresholdMultiplier;
            _checkInterval = checkInterval ?? TimeSpan.FromMilliseconds(150);

            _scrollViewer.Loaded += OnLoaded;
            _scrollViewer.Unloaded += OnUnloaded;
        }

        /// <summary>
        /// What to ask for more of. Assign whatever the list is bound to, again whenever it
        /// changes; anything that is not <see cref="ISupportIncrementalLoading"/> — null
        /// included — leaves the loader idle until it is given something that is.
        /// </summary>
        public object ItemsSource
        {
            get => _itemsSource;
            set
            {
                // By reference: a binding re-pushes the same collection on any change, and the
                // shape of it is settled by the events below rather than by being told again.
                if (_itemsSource == value)
                {
                    return;
                }

                _itemsSource = value;
                _source = value as ISupportIncrementalLoading;

                // Whatever the source that left had in flight is no longer this view's business,
                // and the count goes with it: those loads will not report back.
                _cts?.Cancel();
                _cts = _isMonitoring ? new CancellationTokenSource() : null;

                _activeLoadOperations = 0;
                _consecutiveSizeChangedChecks = 0;

                if (_isMonitoring)
                {
                    CheckNonScrollableState();
                }
            }
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            // Loaded can come round again without an Unloaded in between, and subscribing a
            // second time would leave the first subscription in place.
            if (_isMonitoring)
            {
                return;
            }

            _isMonitoring = true;
            _content = _scrollViewer.Content as FrameworkElement;

            _cts = new CancellationTokenSource();

            _scrollViewer.ViewChanged += OnViewChanged;
            _scrollViewer.SizeChanged += OnSizeChanged;

            if (_content != null)
            {
                _content.SizeChanged += OnSizeChanged;
            }

            CheckNonScrollableState();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            if (!_isMonitoring)
            {
                return;
            }

            _isMonitoring = false;

            _cts?.Cancel();
            _cts = null;

            _scrollViewer.ViewChanged -= OnViewChanged;
            _scrollViewer.SizeChanged -= OnSizeChanged;

            if (_content != null)
            {
                _content.SizeChanged -= OnSizeChanged;
                _content = null;
            }

            _activeLoadOperations = 0;
            _consecutiveSizeChangedChecks = 0;
        }

        private void OnViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
        {
            _consecutiveSizeChangedChecks = 0;

            if (!e.IsIntermediate)
            {
                _lastCheckTime = DateTime.MinValue;
                CheckAndLoad();
            }
            else
            {
                // Throttled while the finger is still down: the check itself is cheap, but the
                // load it can start is not.
                var now = DateTime.UtcNow;
                if (now - _lastCheckTime >= _checkInterval)
                {
                    _lastCheckTime = now;
                    CheckAndLoad();
                }
            }
        }

        // Either the content grew, which is a page arriving, or the viewport did, which can leave
        // a view that was scrollable no longer so.
        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (Math.Abs(e.NewSize.Height - e.PreviousSize.Height) < 0.1)
            {
                return;
            }

            CheckNonScrollableState();
        }

        private void CheckNonScrollableState()
        {
            if (_activeLoadOperations > 0 || _source is not { HasMoreItems: true })
            {
                _consecutiveSizeChangedChecks = 0;
                return;
            }

            if (_scrollViewer.ScrollableHeight > 0)
            {
                _consecutiveSizeChangedChecks = 0;
                CheckAndLoad();
                return;
            }

            // Nothing to scroll yet, so no view change will ever ask for the next page. Keep
            // filling until there is something to scroll, or until the source has been given
            // enough chances to prove it is not adding anything.
            _consecutiveSizeChangedChecks++;

            if (_consecutiveSizeChangedChecks > MaxConsecutiveSizeChangedChecks)
            {
                _consecutiveSizeChangedChecks = 0;
                return;
            }

            LoadItems();
        }

        private void CheckAndLoad()
        {
            if (_source is not { HasMoreItems: true })
            {
                return;
            }

            var verticalOffset = _scrollViewer.VerticalOffset;
            var viewportHeight = _scrollViewer.ViewportHeight;
            var scrollableHeight = _scrollViewer.ScrollableHeight;

            if (scrollableHeight == 0)
            {
                return;
            }

            if (scrollableHeight - verticalOffset < CalculateDynamicThreshold(verticalOffset, viewportHeight, scrollableHeight))
            {
                LoadItems();
            }
        }

        /// <summary>
        /// How close to the end is close enough to start the next page. Grows with how much the
        /// view already holds, because a longer one is scrolled faster, and with how far down it
        /// is, because that is where it is heading. Never more than two viewports, or it would be
        /// loading the whole way down.
        /// </summary>
        private double CalculateDynamicThreshold(double verticalOffset, double viewportHeight, double scrollableHeight)
        {
            var relativePosition = scrollableHeight > 0
                ? verticalOffset / scrollableHeight
                : 0.5;

            var contentRatio = (scrollableHeight + viewportHeight) / viewportHeight;
            var sizeMultiplier = Math.Clamp(contentRatio / 5.0, _minThresholdMultiplier, _maxThresholdMultiplier);
            var positionMultiplier = 1.0 + relativePosition * 0.5;

            return Math.Min(_baseTriggerThreshold * sizeMultiplier * positionMultiplier, viewportHeight * 2.0);
        }

        private void LoadItems()
        {
            var source = _source;
            var cts = _cts;

            if (source == null || cts == null)
            {
                return;
            }

            Logger.Info($"offset: {_scrollViewer.VerticalOffset:F0}/{_scrollViewer.ScrollableHeight:F0}, pending: {_activeLoadOperations}");

            _activeLoadOperations++;

            _ = LoadItemsAsync(source, cts.Token);
        }

        private async Task LoadItemsAsync(ISupportIncrementalLoading source, CancellationToken cancellationToken)
        {
            try
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                await source.LoadMoreItemsAsync(_pageSize);

                // The page has to be measured before the offsets above mean anything.
                await _scrollViewer.UpdateLayoutAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // Unloaded, or handed a different source, while this was in flight.
            }
            catch (Exception ex)
            {
                // The source owns its own failures; all this needs is to stop counting the load.
                Logger.Error(ex);
            }
            finally
            {
                // A load belonging to a source this no longer has must not write back: its count
                // was already dropped when the source changed.
                if (_source == source)
                {
                    _activeLoadOperations--;

                    if (!cancellationToken.IsCancellationRequested)
                    {
                        CheckNonScrollableState();
                    }
                }
            }
        }
    }
}
