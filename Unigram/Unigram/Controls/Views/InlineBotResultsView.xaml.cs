using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Telegram.Api.Helpers;
using Telegram.Api.TL;
using Unigram.Converters;
using Unigram.ViewModels;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236

namespace Unigram.Controls.Views
{
    public sealed partial class InlineBotResultsView : UserControl
    {
        public DialogViewModel ViewModel => DataContext as DialogViewModel;

        public InlineBotResultsView()
        {
            InitializeComponent();
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _panel = Items.ItemsPanelRoot as ItemsStackPanel;

            var scroll = Items.ScrollingHost;
            if (scroll != null)
            {
                scroll.ViewChanged += OnViewChanged;
            }
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            Bindings.StopTracking();
        }

        private void ListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            ItemClick?.Invoke(sender, e);
        }

        public event ItemClickEventHandler ItemClick;

        private Visibility ConvertBannedRights(ITLDialogWith with, bool invert)
        {
            if (with is TLChannel channel && channel.HasBannedRights && channel.BannedRights != null && channel.BannedRights.IsSendInline)
            {
                return invert ? Visibility.Collapsed : Visibility.Visible;
            }

            return invert ? Visibility.Visible : Visibility.Collapsed;
        }

        private string ConvertBannedRights(ITLDialogWith with)
        {
            if (with is TLChannel channel && channel.HasBannedRights && channel.BannedRights != null && channel.BannedRights.IsSendInline)
            {
                if (channel.BannedRights.IsForever())
                {
                    return Strings.Android.AttachInlineRestrictedForever;
                }
                else
                {
                    return string.Format(Strings.Android.AttachInlineRestricted, BindConvert.Current.BannedUntil(channel.BannedRights.UntilDate));
                }
            }

            return null;
        }

        #region Gifs

        private ItemsStackPanel _panel;

        private void OnViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
        {
            var index0 = _panel.FirstVisibleIndex;
            var index1 = _panel.LastVisibleIndex;

            if (index0 > -1 && index1 > -1 /*&& (index0 != _lastIndex0 || index1 != _lastIndex1)*/ && !e.IsIntermediate)
            {
                var messageIds = new TLVector<int>();
                var dialog = ViewModel.Dialog;

                var messages = new List<TLBotInlineResultBase>(index1 - index0);
                var auto = true;
                var news = new Dictionary<string, MediaPlayerItem>();

                for (int i = index0; i <= index1; i++)
                {
                    var container = Items.ContainerFromIndex(i) as GridViewItem;
                    if (container != null)
                    {
                        var item = Items.ItemFromContainer(container) as TLBotInlineResultBase;
                        if (item == null)
                        {
                            continue;
                        }

                        messages.Add(item);
                    }
                }

                Play(messages, auto);
            }
        }

        private Dictionary<string, MediaPlayerItem> _old = new Dictionary<string, MediaPlayerItem>();

        class MediaPlayerItem
        {
            public Grid Container { get; set; }
            public MediaPlayerView Presenter { get; set; }
            public bool Watermark { get; set; }
        }

        public void Play(IEnumerable<TLBotInlineResultBase> items, bool auto)
        {
            var news = new Dictionary<string, MediaPlayerItem>();

            foreach (var message in items)
            {
                var container = Items.ContainerFromItem(message) as GridViewItem;
                if (container == null)
                {
                    continue;
                }

                if (message is TLBotInlineMediaResult mediaResult && mediaResult.Type.Equals("gif", StringComparison.OrdinalIgnoreCase))
                {
                    var document = mediaResult.Document as TLDocument;
                    if (document == null || !TLMessage.IsGif(document))
                    {
                        continue;
                    }

                    var fileName = document.GetFileName();
                    if (File.Exists(FileUtils.GetTempFileName(fileName)))
                    {
                        var root = container.ContentTemplateRoot as FrameworkElement;
                        if (root is Grid final)
                        {
                            news[FileUtils.GetTempFileUrl(fileName)] = new MediaPlayerItem { Container = final, Watermark = false };
                        }
                    }
                }
                else if (message is TLBotInlineResult result && result.Type.Equals("gif", StringComparison.OrdinalIgnoreCase))
                {
                    var root = container.ContentTemplateRoot as FrameworkElement;
                    if (root is Grid final)
                    {
                        news[result.ContentUrl] = new MediaPlayerItem { Container = final, Watermark = false };
                    }
                }
            }

            foreach (var item in _old.Keys.Except(news.Keys).ToList())
            {
                var presenter = _old[item].Presenter;
                if (presenter != null && presenter.MediaPlayer != null)
                {
                    presenter.MediaPlayer.Source = null;
                    presenter.MediaPlayer.Dispose();
                    presenter.MediaPlayer = null;
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
                var container = news[item].Container;
                if (container != null && container.Children.Count < 5)
                {
                    var player = new MediaPlayer();
                    player.AutoPlay = true;
                    player.IsLoopingEnabled = true;
                    player.Source = MediaSource.CreateFromUri(new Uri(item));

                    var presenter = new MediaPlayerView();
                    presenter.MediaPlayer = player;
                    presenter.IsHitTestVisible = false;
                    presenter.Constraint = container.DataContext;

                    news[item].Presenter = presenter;
                    //container.Children.Insert(news[item].Watermark ? 3 : 3, presenter);
                    container.Children.Add(presenter);
                }

                _old.Add(item, news[item]);
            }
        }

        #endregion
    }
}
