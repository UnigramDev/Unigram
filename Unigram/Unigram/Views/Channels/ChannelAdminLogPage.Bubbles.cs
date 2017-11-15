using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Helpers;
using Telegram.Api.TL;
using Unigram.Common;
using Unigram.Controls;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Unigram.Views.Channels
{
    public partial class ChannelAdminLogPage : Page, IGifPlayback
    {
        private ItemsStackPanel _panel;
        private Dictionary<string, MediaPlayerItem> _old = new Dictionary<string, MediaPlayerItem>();

        private void OnViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
        {
            var index0 = _panel.FirstVisibleIndex;
            var index1 = _panel.LastVisibleIndex;

            if (_panel.FirstVisibleIndex > -1 && _panel.LastVisibleIndex > -1 && !e.IsIntermediate)
            {
                var messages = new List<TLMessage>(_panel.LastVisibleIndex - _panel.FirstVisibleIndex);
                var auto = ApplicationSettings.Current.IsAutoPlayEnabled;
                var news = new Dictionary<string, MediaPlayerItem>();

                for (int i = index0; i <= index1; i++)
                {
                    var container = Messages.ContainerFromIndex(i) as ListViewItem;
                    if (container != null)
                    {
                        var message = Messages.ItemFromContainer(container) as TLMessage;
                        if (message == null)
                        {
                            continue;
                        }

                        messages.Add(message);
                    }
                }

                Play(messages, auto);
            }
        }

        class MediaPlayerItem
        {
            public Grid Container { get; set; }
            public MediaPlayerView Presenter { get; set; }
            public bool Watermark { get; set; }
        }

        public void Play(TLMessage message)
        {
            var document = message.GetDocument();
            if (document == null || !TLMessage.IsGif(document))
            {
                return;
            }

            var fileName = FileUtils.GetTempFileUrl(document.GetFileName());
            if (_old.ContainsKey(fileName))
            {
                Play(new TLMessage[0], false);
            }
            else
            {
                Play(new[] { message }, true);
            }
        }

        public void Play(IEnumerable<TLMessage> items, bool auto)
        {
            var news = new Dictionary<string, MediaPlayerItem>();

            foreach (var message in items)
            {
                var container = Messages.ContainerFromItem(message) as ListViewItem;
                if (container == null)
                {
                    continue;
                }

                var document = message.GetDocument();
                if (document == null || !TLMessage.IsGif(document))
                {
                    continue;
                }

                var fileName = document.GetFileName();
                if (File.Exists(FileUtils.GetTempFileName(fileName)))
                {
                    var root = container.ContentTemplateRoot as FrameworkElement;
                    if (root is Grid grid)
                    {
                        root = grid.FindName("Bubble") as FrameworkElement;
                    }

                    var media = root.FindName("Media") as ContentControl;
                    var panel = media.ContentTemplateRoot as FrameworkElement;

                    if (message.Media is TLMessageMediaWebPage)
                    {
                        media = panel.FindName("Media") as ContentControl;
                        panel = media.ContentTemplateRoot as FrameworkElement;
                    }
                    else if (message.Media is TLMessageMediaGame)
                    {
                        panel = panel.FindName("Media") as FrameworkElement;
                    }

                    if (panel is Grid final)
                    {
                        news[FileUtils.GetTempFileUrl(fileName)] = new MediaPlayerItem { Container = final, Watermark = message.Media is TLMessageMediaGame };
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
                    player.IsMuted = true;
                    player.IsLoopingEnabled = true;
                    player.CommandManager.IsEnabled = false;
                    player.Source = MediaSource.CreateFromUri(new Uri(item));

                    var presenter = new MediaPlayerView();
                    presenter.MediaPlayer = player;
                    presenter.IsHitTestVisible = false;
                    presenter.Constraint = container.DataContext;

                    news[item].Presenter = presenter;
                    container.Children.Insert(news[item].Watermark ? 2 : 2, presenter);
                }

                _old.Add(item, news[item]);
            }
        }
    }
}
