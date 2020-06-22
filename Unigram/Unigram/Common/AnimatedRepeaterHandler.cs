using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using Telegram.Td.Api;
using Unigram.Controls;
using Unigram.ViewModels.Drawers;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Unigram.Common
{
    public class AnimatedRepeaterHandler<T>
    {
        private readonly ItemsRepeater _listView;
        private readonly ScrollViewer _scrollingHost;
        private readonly DispatcherTimer _throttler;

        public AnimatedRepeaterHandler(ItemsRepeater listView, ScrollViewer scrollingHost)
        {
            _listView = listView;
            _listView.SizeChanged += OnSizeChanged;
            _listView.Unloaded += OnUnloaded;

            _scrollingHost = scrollingHost;
            _scrollingHost.ViewChanged += OnViewChanged;

            _throttler = new DispatcherTimer();
            _throttler.Interval = TimeSpan.FromMilliseconds(Constants.AnimatedThrottle);
            _throttler.Tick += (s, args) =>
            {
                _throttler.Stop();
                LoadVisibleItems(/*e.IsIntermediate*/ false);
            };
        }

        public Action<int, T> DownloadFile { get; set; }

        public Action<FrameworkElement, LottieView> LoadView { get; set; }
        public Action<FrameworkElement, LottieView> UnloadView { get; set; }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            UnloadVisibleItems();
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (e.PreviousSize.Width < _listView.ActualWidth || e.PreviousSize.Height < _listView.ActualHeight)
            {
                _throttler.Stop();
                _throttler.Start();
            }
        }

        private void OnViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
        {
            LoadVisibleItems(true);

            _throttler.Stop();
            _throttler.Start();
            return;

            if (e.IsIntermediate)
            {
                _throttler.Start();
            }
            else
            {
                LoadVisibleItems(false);
            }

            //LoadVisibleItems(/*e.IsIntermediate*/ false);
        }

        public void ThrottleVisibleItems()
        {
            _throttler.Stop();
            _throttler.Start();
        }

        public void LoadVisibleItems(bool intermediate)
        {
            if (intermediate && _old.Count < 1)
            {
                return;
            }

            int lastVisibleIndex;
            int firstVisibleIndex;

            if (_listView.Layout is MosaicLayout mosaic)
            {
                lastVisibleIndex = mosaic.GetLastVisibleIndex(_scrollingHost);
                firstVisibleIndex = mosaic.GetFirstVisibleIndex(_scrollingHost);
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

            var animations = new List<(Button, T)>(lastVisibleIndex - firstVisibleIndex);

            for (int i = firstVisibleIndex; i <= lastVisibleIndex; i++)
            {
                var container = _listView.TryGetElement(i) as Button;
                if (container == null)
                {
                    continue;
                }

                var item = container.DataContext;
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
                else if (item is Animation animation)
                {
                    animations.Add((container, (T)(object)animation));
                }
                else if (item is InlineQueryResultAnimation inlineQueryResultAnimation)
                {
                    animations.Add((container, (T)(object)inlineQueryResultAnimation));
                }
            }

            if (animations.Count > 0)
            {
                Play(animations, !intermediate);
            }
        }

        public void UnloadVisibleItems()
        {
            foreach (var item in _old.Values)
            {
                var presenter = item.Presenter;
                if (presenter != null)
                {
                    try
                    {
                        presenter.Pause();
                    }
                    catch { }

                    try
                    {
                        item.Container.Children.Remove(presenter);
                    }
                    catch { }
                }
            }

            _old.Clear();
        }

        class MediaPlayerItem
        {
            public File File { get; set; }
            public Grid Container { get; set; }
            public AnimationView Presenter { get; set; }
        }

        private Dictionary<long, MediaPlayerItem> _old = new Dictionary<long, MediaPlayerItem>();

        private void Play(IEnumerable<(Button Contaner, T Sticker)> items, bool auto)
        {
            var news = new Dictionary<long, MediaPlayerItem>();

            foreach (var item in items)
            {
                File file;
                if (item.Sticker is StickerViewModel viewModel)
                {
                    file = viewModel.StickerValue;
                }
                else if (item.Sticker is StickerSetViewModel setViewModel)
                {
                    file = setViewModel.Thumbnail?.File ?? setViewModel.Covers.FirstOrDefault()?.Thumbnail?.File;
                }
                else if (item.Sticker is Sticker sticker)
                {
                    file = sticker.StickerValue;
                }
                else if (item.Sticker is StickerSetInfo set)
                {
                    file = set.Thumbnail?.File ?? set.Covers.FirstOrDefault()?.Thumbnail?.File;
                }
                else if (item.Sticker is Animation animation)
                {
                    file = animation.AnimationValue;
                }
                else if (item.Sticker is InlineQueryResultAnimation inlineQueryResultAnimation)
                {
                    file = inlineQueryResultAnimation.Animation.AnimationValue;
                }
                else
                {
                    continue;
                }

                if (file.Local.IsDownloadingCompleted)
                {
                    var panel = item.Contaner.ContentTemplateRoot as Grid;
                    if (panel is Grid final)
                    {
                        final.Tag = item.Sticker;
                        news[item.Sticker.GetHashCode()] = new MediaPlayerItem
                        {
                            File = file,
                            Container = final
                        };
                    }
                }
                else if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive)
                {
                    DownloadFile?.Invoke(file.Id, item.Sticker);
                }
            }

            foreach (var item in _old.Keys.Except(news.Keys).ToList())
            {
                var presenter = _old[item].Presenter;
                if (presenter != null)
                {
                    //presenter.Dispose();
                }

                var container = _old[item].Container;
                if (container != null && presenter != null)
                {
                    container.Children.Remove(presenter);
                }

                _old.Remove(item);
            }

            if (!auto)
            {
                return;
            }

            foreach (var item in news.Keys.Except(_old.Keys).ToList())
            {
                if (_old.ContainsKey(item))
                {
                    continue;
                }

                if (news.TryGetValue(item, out MediaPlayerItem data) && data.Container != null && data.Container.Children.Count < 2)
                {
                    var presenter = new AnimationView();
                    presenter.AutoPlay = true;
                    presenter.IsLoopingEnabled = true;
                    presenter.Source = UriEx.GetLocal(data.File.Local.Path);

                    data.Presenter = presenter;
                    data.Container.Children.Insert(1, presenter);
                }

                _old[item] = news[item];
            }
        }
    }
}
