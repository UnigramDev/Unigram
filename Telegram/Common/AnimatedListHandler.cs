//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using Telegram.Controls;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;

namespace Telegram.Common
{
    public enum AnimatedListType
    {
        Stickers,
        Animations,
        Emoji,
        Other // Inline bots, chat list,
    }

    public enum AnimatedImageType
    {
        Sticker,
        Animation,
        Emoji,
        Other
    }

    public partial class AnimatedListHandler
    {
        private readonly ListViewBase _listView;
        private readonly DispatcherTimer _debouncer;

        private readonly AnimatedListType _type;

        private bool _paused;
        private bool _unloaded;

        public AnimatedListHandler(ListViewBase listView, AnimatedListType type)
        {
            _listView = listView;
            _listView.SizeChanged += OnSizeChanged;
            _listView.Unloaded += OnUnloaded;

            _debouncer = new DispatcherTimer();
            _debouncer.Interval = TimeSpan.FromMilliseconds(Constants.AnimatedThrottle);
            _debouncer.Tick += (s, args) =>
            {
                _debouncer.Stop();
                LoadVisibleItems();
            };

            _type = type;
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (sender is ListViewBase)
            {
                _listView.SizeChanged -= OnSizeChanged;
                _listView.Items.VectorChanged += OnVectorChanged;

                var scrollViewer = _listView.GetScrollViewer();
                if (scrollViewer != null)
                {
                    scrollViewer.ViewChanged += OnViewChanged;
                }

                var panel = _listView.ItemsPanelRoot;
                if (panel != null)
                {
                    panel.SizeChanged += OnSizeChanged;
                }
            }
            else if (e.PreviousSize.Width < _listView.ActualWidth || e.PreviousSize.Height < _listView.ActualHeight)
            {
                ThrottleVisibleItems();
            }
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            UnloadItems();
        }

        private void OnVectorChanged(IObservableVector<object> sender, IVectorChangedEventArgs e)
        {
            if (_unloaded)
            {
                return;
            }

            ThrottleVisibleItems();
        }

        // 1 while the content is moving up the screen (scrolling down), -1 the other way, 0 before
        // anything has moved - which is the case that matters, because it is a panel just opened.
        private int _direction;
        private double _verticalOffset;

        private void OnViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
        {
            if (sender is ScrollViewer scrollViewer)
            {
                var offset = scrollViewer.VerticalOffset;

                // Half a pixel is noise, not a direction: an unchanged offset leaves the last one
                // standing rather than resetting to neutral, so a pause mid-scroll keeps its order.
                if (offset > _verticalOffset + 0.5)
                {
                    _direction = 1;
                }
                else if (offset < _verticalOffset - 0.5)
                {
                    _direction = -1;
                }

                _verticalOffset = offset;
            }

            LoadVisibleItems();

            ThrottleVisibleItems();
        }

        //private bool _throttling;

        public void ThrottleVisibleItems()
        {
            //if (_throttling)
            //{
            //    return;
            //}

            //_throttling = true;
            //VisualUtilities.QueueCallbackForCompositionRendering(LoadVisibleItems);

            if (_debouncer.IsEnabled || _paused)
            {
                return;
            }

            _debouncer.Stop();
            _debouncer.Start();
        }

        public bool IsDisabledByPolicy
        {
            get => _type switch
            {
                AnimatedListType.Stickers => !PowerSavingPolicy.AutoPlayStickers,
                AnimatedListType.Animations => !PowerSavingPolicy.AutoPlayAnimations,
                AnimatedListType.Emoji => !PowerSavingPolicy.AutoPlayEmoji,
                _ => false
            };
        }

        public void Suspend()
        {
            _paused = true;
            UpdateVisibleItems(false);
        }

        public void Resume()
        {
            _paused = false;
            ThrottleVisibleItems();
        }

        public void LoadVisibleItems() => UpdateVisibleItems(true);

        public void UnloadVisibleItems() => UpdateVisibleItems(false);

        public void UnloadItems() => UpdateVisibleItems(false);

        public void UpdateVisibleItems(bool load)
        {
            //_throttling = false;

            if (_paused && load)
            {
                return;
            }

            int lastVisibleIndex;
            int firstVisibleIndex;
            int lastCacheIndex;
            int firstCacheIndex;

            if (_listView.ItemsPanelRoot is ItemsStackPanel stack)
            {
                lastCacheIndex = stack.LastCacheIndex;
                firstCacheIndex = stack.FirstCacheIndex;
                lastVisibleIndex = stack.LastVisibleIndex;
                firstVisibleIndex = stack.FirstVisibleIndex;
            }
            else if (_listView.ItemsPanelRoot is ItemsWrapGrid wrap)
            {
                lastCacheIndex = wrap.LastCacheIndex;
                firstCacheIndex = wrap.FirstCacheIndex;
                lastVisibleIndex = wrap.LastVisibleIndex;
                firstVisibleIndex = wrap.FirstVisibleIndex;
            }
            else
            {
                return;
            }

            if (lastCacheIndex < firstCacheIndex || firstCacheIndex < 0)
            {
                return;
            }

            // We do three passes to try to optimize download order.
            //
            // The visible pass follows the scroll: downwards it runs last to first, upwards - and
            // when nothing has moved yet, so on the first open - first to last. Note the loader's
            // work queue is LIFO, so whichever end is walked last is the end that loads first.
            var descending = _direction > 0;

            var start = descending ? lastVisibleIndex : firstVisibleIndex;
            var stop = descending ? firstVisibleIndex : lastVisibleIndex;
            var step = descending ? -1 : 1;

            for (int i = start; descending ? i >= stop : i <= stop; i += step)
            {
                var container = _listView.ContainerFromIndex(i) as SelectorItem;
                if (container == null || container.ContentTemplateRoot is not FrameworkElement content)
                {
                    continue;
                }

                var player = content as IPlayerView;
                player ??= content.FindName("Player") as IPlayerView;
                player?.ViewportChanged(load);
            }

            for (int i = firstCacheIndex; i < firstVisibleIndex; i++)
            {
                var container = _listView.ContainerFromIndex(i) as SelectorItem;
                if (container == null || container.ContentTemplateRoot is not FrameworkElement content)
                {
                    continue;
                }

                var player = content as IPlayerView;
                player ??= content.FindName("Player") as IPlayerView;
                player?.ViewportChanged(false);
            }

            for (int i = lastCacheIndex; i > lastVisibleIndex; i--)
            {
                var container = _listView.ContainerFromIndex(i) as SelectorItem;
                if (container == null || container.ContentTemplateRoot is not FrameworkElement content)
                {
                    continue;
                }

                var player = content as IPlayerView;
                player ??= content.FindName("Player") as IPlayerView;
                player?.ViewportChanged(false);
            }

            _unloaded = !load;
        }
    }
}
