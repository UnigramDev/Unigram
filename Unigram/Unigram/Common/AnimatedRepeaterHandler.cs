using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using Telegram.Td.Api;
using Unigram.Controls;
using Unigram.ViewModels.Drawers;

namespace Unigram.Common
{
    public class AnimatedRepeaterHandler<T>
    {
        private readonly ItemsRepeater _listView;
        private readonly ScrollViewer _scrollingHost;
        private readonly DispatcherTimer _debouncer;

        private readonly Dictionary<long, IPlayerView> _prev = new Dictionary<long, IPlayerView>();

        public AnimatedRepeaterHandler(ItemsRepeater listView, ScrollViewer scrollingHost)
        {
            _listView = listView;
            _listView.SizeChanged += OnSizeChanged;
            _listView.Unloaded += OnUnloaded;

            _scrollingHost = scrollingHost;
            _scrollingHost.ViewChanged += OnViewChanged;

            _debouncer = new DispatcherTimer();
            _debouncer.Interval = TimeSpan.FromMilliseconds(Constants.AnimatedThrottle);
            _debouncer.Tick += (s, args) =>
            {
                _debouncer.Stop();
                LoadVisibleItems(/*e.IsIntermediate*/ false);
            };
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

            var next = new Dictionary<long, IPlayerView>();

            for (int i = firstVisibleIndex; i <= lastVisibleIndex; i++)
            {
                var container = _listView.TryGetElement(i) as Button;
                if (container == null)
                {
                    continue;
                }

                File file = null;

                var item = container.DataContext;
                if (item is StickerViewModel viewModel && viewModel.IsAnimated)
                {
                    file = viewModel.StickerValue;
                }
                else if (item is StickerSetViewModel setViewModel && setViewModel.IsAnimated)
                {
                    file = setViewModel.Thumbnail?.File ?? setViewModel.Covers.FirstOrDefault()?.Thumbnail?.File;
                }
                else if (item is Sticker sticker && sticker.IsAnimated)
                {
                    file = sticker.StickerValue;
                }
                else if (item is StickerSetInfo set && set.IsAnimated)
                {
                    file = set.Thumbnail?.File ?? set.Covers.FirstOrDefault()?.Thumbnail?.File;
                }
                else if (item is Animation animation)
                {
                    file = animation.AnimationValue;
                }
                else if (item is InlineQueryResultAnimation inlineQueryResultAnimation)
                {
                    file = inlineQueryResultAnimation.Animation.AnimationValue;
                }
                else if (item is InlineQueryResultSticker inlineQueryResultSticker && inlineQueryResultSticker.Sticker.IsAnimated)
                {
                    file = inlineQueryResultSticker.Sticker.StickerValue;
                }

                if (file == null || !file.Local.IsDownloadingCompleted)
                {
                    continue;
                }

                var panel = container.ContentTemplateRoot;
                if (panel is FrameworkElement final)
                {
                    var lottie = final.FindName("Player") as IPlayerView;
                    if (lottie != null)
                    {
                        lottie.Tag = item;
                        next[item.GetHashCode()] = lottie;
                    }
                }
            }

            foreach (var item in _prev.Keys.Except(next.Keys).ToList())
            {
                var presenter = _prev[item];
                if (presenter != null)
                {
                    presenter.Pause();
                }

                _prev.Remove(item);
            }

            if (intermediate)
            {
                return;
            }

            foreach (var item in next)
            {
                //if (_prev.ContainsKey(item))
                //{
                //    continue;
                //}

                if (item.Value != null)
                {
                    item.Value.Play();
                }

                _prev[item.Key] = item.Value;
            }
        }

        public void UnloadVisibleItems()
        {
            foreach (var item in _prev.Values)
            {
                try
                {
                    item.Pause();
                }
                catch { }
            }

            _prev.Clear();
        }
    }
}
