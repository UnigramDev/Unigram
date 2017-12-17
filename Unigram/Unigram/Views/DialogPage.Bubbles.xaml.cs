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
using Unigram.Controls.Messages;
using LinqToVisualTree;

namespace Unigram.Views
{
    public partial class DialogPage : Page, IGifPlayback
    {
        private ItemsStackPanel _panel;
        private Dictionary<string, MediaPlayerItem> _old = new Dictionary<string, MediaPlayerItem>();

        private async void OnViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
        {
            if (Messages.ScrollingHost.ScrollableHeight - Messages.ScrollingHost.VerticalOffset < 120)
            {
                //if (ViewModel.IsFirstSliceLoaded)
                //{
                //    Messages.SetScrollMode(ItemsUpdatingScrollMode.KeepLastItemInView, false);
                //}
                //else
                //{
                //    Messages.SetScrollMode(ItemsUpdatingScrollMode.KeepItemsInView, true);
                //}

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

                var mentionIds = new TLVector<int>();
                var unreadId = 0;
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
                            //if (/*commonMessage.IsUnread ||*/ commonMessage.Id > dialog?.ReadInboxMaxId)
                            //{
                            //    commonMessage.SetUnread(false);

                            //    unreadId = commonMessage.Id > unreadId ? commonMessage.Id : unreadId;
                            //}

                            if (commonMessage.IsMentioned && commonMessage.IsMediaUnread)
                            {
                                commonMessage.IsMediaUnread = false;
                                commonMessage.RaisePropertyChanged(() => commonMessage.IsMediaUnread);

                                mentionIds.Add(commonMessage.Id);
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

                //if (unreadId > 0)
                //{
                //    if (dialog != null)
                //    {
                //        dialog.UnreadCount = Math.Max(0, dialog.UnreadCount - 1 - ViewModel.Items.Count(x => x.Id > dialog.ReadInboxMaxId && x.Id < unreadId));
                //        dialog.RaisePropertyChanged(() => dialog.UnreadCount);

                //        dialog.ReadInboxMaxId = unreadId;
                //    }

                //    var container = Messages.ContainerFromItem(ViewModel.Items.FirstOrDefault(x => x.Id == unreadId)) as ListViewItem;
                //    var bubble = container.Descendants<MessageBubble>().FirstOrDefault() as MessageBubble;
                //    if (bubble != null)
                //    {
                //        bubble.Highlight();
                //    }

                //    if (ViewModel.With is TLChannel channel)
                //    {
                //        ViewModel.ProtoService.ReadHistoryAsync(channel, unreadId, null);
                //    }
                //    else
                //    {
                //        ViewModel.ProtoService.ReadHistoryAsync(ViewModel.Peer, unreadId, 0, null);
                //    }
                //}

                if (mentionIds.Count > 0)
                {
                    if (dialog != null)
                    {
                        dialog.UnreadMentionsCount = Math.Max(0, dialog.UnreadMentionsCount - mentionIds.Count);
                        dialog.RaisePropertyChanged(() => dialog.UnreadMentionsCount);
                    }

                    if (ViewModel.With is TLChannel channel)
                    {
                        ViewModel.ProtoService.ReadMessageContentsAsync(channel.ToInputChannel(), mentionIds, null);
                    }
                    else
                    {
                        ViewModel.ProtoService.ReadMessageContentsAsync(mentionIds, null);
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
                if (document == null || !(TLMessage.IsGif(document) /*|| TLMessage.IsRoundVideo(document)*/))
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
                    //else if (message.IsRoundVideo())
                    //{
                    //    panel = panel.FindName("Inner") as FrameworkElement;
                    //}

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














        private Dictionary<string, DataTemplate> _typeToTemplateMapping = new Dictionary<string, DataTemplate>();
        private Dictionary<string, HashSet<SelectorItem>> _typeToItemHashSetMapping = new Dictionary<string, HashSet<SelectorItem>>();

        private void OnChoosingItemContainer(ListViewBase sender, ChoosingItemContainerEventArgs args)
        {
            var typeName = SelectTemplateCore(args.Item);

            Debug.Assert(_typeToItemHashSetMapping.ContainsKey(typeName), "The type of the item used with DataTemplateSelectorBehavior must have a DataTemplate mapping");
            var relevantHashSet = _typeToItemHashSetMapping[typeName];

            // args.ItemContainer is used to indicate whether the ListView is proposing an
            // ItemContainer (ListViewItem) to use. If args.Itemcontainer != null, then there was a
            // recycled ItemContainer available to be reused.
            if (args.ItemContainer != null)
            {
                if (args.ItemContainer.Tag.Equals(typeName))
                {
                    // Suggestion matches what we want, so remove it from the recycle queue
                    relevantHashSet.Remove(args.ItemContainer);
#if ENABLE_DEBUG_SPEW
                    Debug.WriteLine($"Removing (suggested) {args.ItemContainer.GetHashCode()} from {typeName}");
#endif // ENABLE_DEBUG_SPEW
                }
                else
                {
                    // The ItemContainer's datatemplate does not match the needed
                    // datatemplate.
                    // Don't remove it from the recycle queue, since XAML will resuggest it later
                    args.ItemContainer = null;
                }
            }

            // If there was no suggested container or XAML's suggestion was a miss, pick one up from the recycle queue
            // or create a new one
            if (args.ItemContainer == null)
            {
                // See if we can fetch from the correct list.
                if (relevantHashSet.Count > 0)
                {
                    // Unfortunately have to resort to LINQ here. There's no efficient way of getting an arbitrary
                    // item from a hashset without knowing the item. Queue isn't usable for this scenario
                    // because you can't remove a specific element (which is needed in the block above).
                    args.ItemContainer = relevantHashSet.First();
                    relevantHashSet.Remove(args.ItemContainer);
#if ENABLE_DEBUG_SPEW
                    Debug.WriteLine($"Removing (reused) {args.ItemContainer.GetHashCode()} from {typeName}");
#endif // ENABLE_DEBUG_SPEW
                }
                else
                {
                    // There aren't any (recycled) ItemContainers available. So a new one
                    // needs to be created.
                    var item = CreateSelectorItem(typeName);
                    item.Style = Messages.ItemContainerStyleSelector.SelectStyle(args.Item, item);
                    args.ItemContainer = item;
#if ENABLE_DEBUG_SPEW
                    Debug.WriteLine($"Creating {args.ItemContainer.GetHashCode()} for {typeName}");
#endif // ENABLE_DEBUG_SPEW
                }
            }

            // Indicate to XAML that we picked a container for it
            args.IsContainerPrepared = true;
        }

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue == true)
            {
                // XAML has indicated that the item is no longer being shown, so add it to the recycle queue
                var tag = args.ItemContainer.Tag as string;

#if ENABLE_DEBUG_SPEW
                Debug.WriteLine($"Adding {args.ItemContainer.GetHashCode()} to {tag}");
#endif // ENABLE_DEBUG_SPEW

                var added = _typeToItemHashSetMapping[tag].Add(args.ItemContainer);

#if ENABLE_DEBUG_SPEW
                Debug.Assert(added == true, "Recycle queue should never have dupes. If so, we may be incorrectly reusing a container that is already in use!");
#endif // ENABLE_DEBUG_SPEW
            }
        }

        private SelectorItem CreateSelectorItem(string typeName)
        {
            SelectorItem item = new ListViewItem();
            //item.ContentTemplate = _typeToTemplateMapping[typeName];
            item.ContentTemplate = Resources[typeName] as DataTemplate;
            item.Tag = typeName;
            return item;
        }

        private string SelectTemplateCore(object item)
        {
            var messageBase = item as TLMessageBase;
            if (messageBase == null || messageBase is TLMessageEmpty)
            {
                return "EmptyMessageTemplate";
            }
            else if (messageBase is TLMessage message)
            {
                if (message.Media is TLMessageMediaPhoto photoMedia && photoMedia.HasTTLSeconds && (photoMedia.Photo is TLPhotoEmpty || !photoMedia.HasPhoto))
                {
                    return "ServiceMessageTemplate";
                }
                else if (message.Media is TLMessageMediaDocument documentMedia && documentMedia.HasTTLSeconds && (documentMedia.Document is TLDocumentEmpty || !documentMedia.HasDocument))
                {
                    return "ServiceMessageTemplate";
                }

                if (message.IsSaved())
                {
                    return "ChatFriendMessageTemplate";
                }

                if (message.IsOut && !message.IsPost)
                {
                    return "UserMessageTemplate";
                }
                else if (message.ToId is TLPeerChat || (message.ToId is TLPeerChannel && !message.IsPost))
                {
                    return "ChatFriendMessageTemplate";
                }

                return "FriendMessageTemplate";
            }
            else if (messageBase is TLMessageService serviceMessage)
            {
                if (serviceMessage.Action is TLMessageActionChatEditPhoto)
                {
                    return "ServiceMessagePhotoTemplate";
                }
                else if (serviceMessage.Action is TLMessageActionHistoryClear)
                {
                    return "EmptyMessageTemplate";
                }
                //else if (serviceMessage.Action is TLMessageActionDate)
                //{
                //    return "ServiceMessageDateTemplate";
                //}
                //else if (serviceMessage.Action is TLMessageActionUnreadMessages)
                //{
                //    //return ServiceMessageUnreadTemplate;
                //    return "ServiceMessageLocalTemplate";
                //}
                else if (serviceMessage.Action is TLMessageActionPhoneCall)
                {
                    return serviceMessage.IsOut ? "ServiceUserCallMessageTemplate" : "ServiceFriendCallMessageTemplate";
                }

                return "ServiceMessageTemplate";
            }

            return "EmptyMessageTemplate";
        }
    }

    public interface IGifPlayback
    {
        void Play(TLMessage message);
        void Play(IEnumerable<TLMessage> items, bool auto);
    }
}
