using System;
using System.Collections.Generic;
using System.Linq;
using Telegram.Td.Api;
using Unigram.Controls;
using Unigram.ViewModels.Drawers;
using Unigram.Views;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;

namespace Unigram.Common
{
    public class AnimatedListHandler<T>
    {
        private readonly ListViewBase _listView;
        private readonly DispatcherTimer _debouncer;

        public AnimatedListHandler(ListViewBase listView)
        {
            _listView = listView;
            _listView.Loaded += OnLoaded;
            _listView.Unloaded += OnUnloaded;

            _debouncer = new DispatcherTimer();
            _debouncer.Interval = TimeSpan.FromMilliseconds(Constants.AnimatedThrottle);
            _debouncer.Tick += (s, args) =>
            {
                _debouncer.Stop();
                LoadVisibleItems(/*e.IsIntermediate*/ false);
            };
        }

        public Action<int, T> DownloadFile { get; set; }

        public Action<FrameworkElement, LottieView> LoadView { get; set; }
        public Action<FrameworkElement, LottieView> UnloadView { get; set; }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
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

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            UnloadVisibleItems();
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (e.PreviousSize.Width < _listView.ActualWidth || e.PreviousSize.Height < _listView.ActualHeight)
            {
                _debouncer.Stop();
                _debouncer.Start();
            }
        }

        private void OnViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
        {
            LoadVisibleItems(true);

            _debouncer.Stop();
            _debouncer.Start();
            return;

            if (e.IsIntermediate)
            {
                _debouncer.Start();
            }
            else
            {
                LoadVisibleItems(false);
            }

            //LoadVisibleItems(/*e.IsIntermediate*/ false);
        }

        public void ThrottleVisibleItems()
        {
            _debouncer.Stop();
            _debouncer.Start();
        }

        public void LoadVisibleItems(bool intermediate)
        {
            if (intermediate && _prev.Count < 1)
            {
                return;
            }

            int lastVisibleIndex;
            int firstVisibleIndex;

            if (_listView.ItemsPanelRoot is ItemsStackPanel stack)
            {
                lastVisibleIndex = stack.LastVisibleIndex;
                firstVisibleIndex = stack.FirstVisibleIndex;
            }
            else if (_listView.ItemsPanelRoot is ItemsWrapGrid wrap)
            {
                lastVisibleIndex = wrap.LastVisibleIndex;
                firstVisibleIndex = wrap.FirstVisibleIndex;
            }
            else
            {
                return;
            }

            if (lastVisibleIndex < firstVisibleIndex || firstVisibleIndex < 0)
            {
                UnloadVisibleItems();
                return;
            }

            var animations = new List<(SelectorItem, T)>(lastVisibleIndex - firstVisibleIndex);

            for (int i = firstVisibleIndex; i <= lastVisibleIndex; i++)
            {
                var container = _listView.ContainerFromIndex(i) as SelectorItem;
                if (container == null)
                {
                    continue;
                }

                var item = _listView.ItemFromContainer(container);
                if (item is StickerViewModel viewModel && viewModel.IsAnimated)
                {
                    animations.Add((container, (T)(object)viewModel));
                }
                else if (item is StickerSetViewModel setViewModel && setViewModel.IsAnimated)
                {
                    animations.Add((container, (T)(object)setViewModel));
                }
                else if (item is Sticker sticker && sticker.IsAnimated)
                {
                    animations.Add((container, (T)(object)sticker));
                }
                else if (item is StickerSetInfo set && set.IsAnimated)
                {
                    animations.Add((container, (T)(object)set));
                }
            }

            if (animations.Count > 0)
            {
                Play(animations, !intermediate);
            }
        }

        public void UnloadVisibleItems()
        {
            foreach (var item in _prev.Values)
            {
                var presenter = item.Player;
                if (presenter != null)
                {
                    try
                    {
                        presenter.Pause();
                    }
                    catch { }
                }
            }

            _prev.Clear();
        }

        class MediaPlayerItem
        {
            public IPlayerView Player { get; set; }
        }

        private readonly Dictionary<long, MediaPlayerItem> _prev = new Dictionary<long, MediaPlayerItem>();

        private void Play(IEnumerable<(SelectorItem Contaner, T Sticker)> items, bool auto)
        {
            var next = new Dictionary<long, MediaPlayerItem>();

            foreach (var item in items)
            {
                File animation = null;
                if (item.Sticker is StickerViewModel viewModel)
                {
                    animation = viewModel.StickerValue;
                }
                else if (item.Sticker is StickerSetViewModel setViewModel)
                {
                    animation = setViewModel.Thumbnail?.File ?? setViewModel.Covers.FirstOrDefault()?.Thumbnail?.File;
                }
                else if (item.Sticker is Sticker sticker)
                {
                    animation = sticker.StickerValue;
                }
                else if (item.Sticker is StickerSetInfo set)
                {
                    animation = set.Thumbnail?.File ?? set.Covers.FirstOrDefault()?.Thumbnail?.File;
                }
                
                if (animation == null)
                {
                    continue;
                }

                if (animation.Local.IsDownloadingCompleted)
                {
                    var panel = item.Contaner.ContentTemplateRoot;
                    if (panel is FrameworkElement final)
                    {
                        var lottie = final.FindName("Player") as IPlayerView;
                        if (lottie != null)
                        {
                            lottie.Tag = item.Sticker;
                            next[item.Sticker.GetHashCode()] = new MediaPlayerItem { Player = lottie };
                        }
                    }
                }
            }

            foreach (var item in _prev.Keys.Except(next.Keys).ToList())
            {
                var presenter = _prev[item].Player;
                if (presenter != null)
                {
                    presenter.Pause();
                }

                _prev.Remove(item);
            }

            if (!auto)
            {
                return;
            }

            foreach (var item in next)
            {
                //if (_oldStickers.ContainsKey(item))
                //{
                //    continue;
                //}

                if (item.Value.Player != null)
                {
                    item.Value.Player.Play();
                }

                _prev[item.Key] = item.Value;
            }
        }
    }
}
