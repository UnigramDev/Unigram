//
// Copyright Fela Ameghino 2015-2023
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

    public class AnimatedListHandler
    {
        private readonly ListViewBase _listView;
        private readonly DispatcherTimer _debouncer;

        private readonly AnimatedListType _type;

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

        private void OnViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
        {
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

            if (_debouncer.IsEnabled)
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

        public void LoadVisibleItems() => UpdateVisibleItems(true);

        public void UnloadVisibleItems() => UpdateVisibleItems(false);

        public void UnloadItems() => UpdateVisibleItems(false);

        public void UpdateVisibleItems(bool load)
        {
            //_throttling = false;

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

            for (int i = firstCacheIndex; i <= lastCacheIndex; i++)
            {
                var container = _listView.ContainerFromIndex(i) as SelectorItem;
                if (container == null || container.ContentTemplateRoot is not FrameworkElement content)
                {
                    continue;
                }

                var within = load && i >= firstVisibleIndex && i <= lastVisibleIndex;

                var player = content as IPlayerView;
                player ??= content.FindName("Player") as IPlayerView;
                player?.ViewportChanged(within);
            }

            _unloaded = !load;
        }
    }
}
