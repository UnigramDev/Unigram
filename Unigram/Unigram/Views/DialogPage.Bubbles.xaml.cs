using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Api.Helpers;
using Telegram.Api.TL;
using Unigram.Converters;
using Unigram.Views;
using Unigram.ViewModels;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Globalization.DateTimeFormatting;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Pickers;
using Windows.Storage.Search;
using Windows.Storage.Streams;
using Windows.System;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Core;
using Windows.UI.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
using Windows.UI.Xaml.Shapes;
using Unigram.Controls;
using Telegram.Api;
using Windows.Media.Core;
using Windows.Media.Playback;
using Unigram.Common;

namespace Unigram.Views
{
    public partial class DialogPage : Page
    {
        private ItemsStackPanel _panel;

        private async void OnViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
        {
            if (Messages.ScrollingHost.ScrollableHeight - Messages.ScrollingHost.VerticalOffset < 120)
            {
                if (ViewModel.IsFirstSliceLoaded)
                {
                    ViewModel.UpdatingScrollMode = UpdatingScrollMode.KeepLastItemInView;
                }
                else
                {
                    ViewModel.UpdatingScrollMode = UpdatingScrollMode.ForceKeepItemsInView;
                }

                Arrow.Visibility = Visibility.Collapsed;
            }
            else
            {
                Arrow.Visibility = Visibility.Visible;
            }

            //if (ViewModel.Peer is TLInputPeerUser)
            //{
            //    lvDialogs.ScrollingHost.ViewChanged -= OnViewChanged;
            //    return;
            //}

            var index0 = _panel.FirstVisibleIndex;
            var index1 = _panel.LastVisibleIndex;

            var show = false;
            var date = DateTime.Now;
            var message0 = default(TLMessageCommonBase);

            if (index0 > -1 && index1 > -1 /*&& (index0 != _lastIndex0 || index1 != _lastIndex1)*/ && !e.IsIntermediate)
            {
                var container0 = Messages.ContainerFromIndex(index0);
                if (container0 != null)
                {
                    var item0 = Messages.ItemFromContainer(container0);
                    if (item0 != null)
                    {
                        message0 = item0 as TLMessageCommonBase;
                        var date0 = BindConvert.Current.DateTime(message0.Date);

                        var service0 = message0 as TLMessageService;
                        if (service0 != null)
                        {
                            show = !(service0.Action is TLMessageActionDate);
                        }
                        else
                        {
                            show = true;
                        }

                        date = date0.Date;
                    }
                }

                var messageIds = new TLVector<int>();
                var dialog = ViewModel.Dialog;

                var messages = new List<TLMessage>(index1 - index0);
                var auto = ApplicationSettings.Current.IsAutoPlayEnabled;
                var news = new Dictionary<string, MediaPlayerItem>();

                for (int i = index0; i <= index1; i++)
                {
                    var container = Messages.ContainerFromIndex(i) as ListViewItem;
                    if (container != null)
                    {
                        var item = Messages.ItemFromContainer(container);
                        if (item is TLMessageCommonBase commonMessage && !commonMessage.IsOut)
                        {
                            //if (commonMessage.IsUnread)
                            //{
                            //    Debug.WriteLine($"Messager {commonMessage.Id} is unread.");
                            //    ViewModel.MarkAsRead(commonMessage);
                            //}

                            if (commonMessage.IsMentioned && commonMessage.IsMediaUnread)
                            {
                                commonMessage.IsMediaUnread = false;
                                commonMessage.RaisePropertyChanged(() => commonMessage.IsMediaUnread);

                                if (dialog != null)
                                {
                                    dialog.UnreadMentionsCount = Math.Max(0, dialog.UnreadMentionsCount - 1);
                                    dialog.RaisePropertyChanged(() => dialog.UnreadMentionsCount);
                                }

                                messageIds.Add(commonMessage.Id);
                            }
                        }

                        var message = item as TLMessage;
                        if (message == null)
                        {
                            continue;
                        }

                        messages.Add(message);
                    }
                }

                Play(messages, auto);

                if (messageIds.Count > 0)
                {
                    if (ViewModel.With is TLChannel channel)
                    {
                        ViewModel.ProtoService.ReadMessageContentsAsync(channel.ToInputChannel(), messageIds, null);
                    }
                    else
                    {
                        ViewModel.ProtoService.ReadMessageContentsAsync(messageIds, null);
                    }
                }
            }

            if (show)
            {
                var paragraph = new Paragraph();
                paragraph.Inlines.Add(new Run { Text = DateTimeToFormatConverter.ConvertDayGrouping(date) });

                DateHeader.DataContext = message0;
                DateHeader.Visibility = Visibility.Visible;
                DateHeaderLabel.Blocks.Clear();
                DateHeaderLabel.Blocks.Add(paragraph);
            }
            else
            {
                DateHeader.Visibility = Visibility.Collapsed;
            }

            if (e.IsIntermediate == false)
            {
                await Task.Delay(4000);
                DateHeader.Visibility = Visibility.Collapsed;
            }
        }

        private Dictionary<string, MediaPlayerItem> _old = new Dictionary<string, MediaPlayerItem>();

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

            var fileName = document.GetFileName();
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

                    var media = root.FindName("MediaControl") as ContentControl;
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
                        news[fileName] = new MediaPlayerItem { Container = final, Watermark = message.Media is TLMessageMediaGame };
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
                    player.Source = MediaSource.CreateFromUri(FileUtils.GetTempFileUri(item));

                    var presenter = new MediaPlayerView();
                    presenter.MediaPlayer = player;
                    presenter.IsHitTestVisible = false;
                    presenter.Constraint = container.DataContext;

                    news[item].Presenter = presenter;
                    container.Children.Insert(news[item].Watermark ? 3 : 4, presenter);
                }

                _old.Add(item, news[item]);
            }
        }
    }
}
