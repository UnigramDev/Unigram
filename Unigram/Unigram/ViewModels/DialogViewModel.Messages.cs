using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api;
using Telegram.Api.Helpers;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache.EventArgs;
using Telegram.Api.TL;
using Telegram.Api.TL.Messages;
using Telegram.Helpers;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Controls.Views;
using Unigram.Converters;
using Unigram.Helpers;
using Unigram.Native;
using Unigram.Services;
using Unigram.Views;
using Unigram.Views.Payments;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using Windows.System;
using Windows.UI.Popups;
using Windows.UI.Xaml.Controls;

namespace Unigram.ViewModels
{
    public partial class DialogViewModel
    {
        #region Reply

        public RelayCommand<TLMessageBase> MessageReplyCommand { get; }
        private void MessageReplyExecute(TLMessageBase message)
        {
            Search = null;

            if (message == null)
            {
                return;
            }

            var serviceMessage = message as TLMessageService;
            if (serviceMessage != null)
            {
                var action = serviceMessage.Action;
                // TODO: 
                //if (action is TLMessageActionEmpty || action is TLMessageActionUnreadMessages)
                //{
                //    return;
                //}
            }

            var message31 = message as TLMessage;
            if (message31 != null && message31.Media is TLMessageMediaGroup groupMedia)
            {
                message = groupMedia.Layout.Messages.FirstOrDefault();
                message31 = message as TLMessage;
            }

            if (message.Id <= 0) return;

            if (message31 != null && !message31.IsOut && message31.HasFromId)
            {
                var fromId = message31.FromId.Value;
                var user = CacheService.GetUser(fromId) as TLUser;
                if (user != null && user.IsBot)
                {
                    SetReplyMarkup(message31);
                }
            }

            Reply = message;
            TextField.Focus(Windows.UI.Xaml.FocusState.Keyboard);
        }

        #endregion

        #region Delete

        public RelayCommand<TLMessageBase> MessageDeleteCommand { get; }
        private async void MessageDeleteExecute(TLMessageBase messageBase)
        {
            if (messageBase == null)
            {
                return;
            }

            var message = messageBase as TLMessage;
            if (message != null && message.Media is TLMessageMediaGroup groupMedia)
            {
                ExpandSelection(new[] { message });
                MessagesDeleteExecute();
                return;
            }

            if (message != null && !message.IsOut && !message.IsPost && Peer is TLInputPeerChannel)
            {
                var dialog = new DeleteChannelMessageDialog(1, message.From?.FullName);

                var result = await dialog.ShowQueuedAsync();
                if (result == ContentDialogResult.Primary)
                {
                    var channel = With as TLChannel;

                    if (dialog.DeleteAll)
                    {
                        var response = await DeleteUserHistoryAsync(channel, message.From.ToInputUser());
                        if (response.IsSucceeded)
                        {
                            CacheService.DeleteUserHistory(new TLPeerChannel { ChannelId = channel.Id }, new TLPeerUser { UserId = message.From.Id });
                        }

                        for (int i = 0; i < Items.Count; i++)
                        {
                            if (Items[i] is TLMessageCommonBase messageCommon && messageCommon.ToId is TLPeerChannel && messageCommon.FromId.Value == message.From.Id)
                            {
                                if (messageCommon.Id == 1 && messageCommon is TLMessageService serviceMessage && serviceMessage.Action is TLMessageActionChannelMigrateFrom)
                                {
                                    continue;
                                }

                                Items.RemoveAt(i--);
                            }
                        }
                    }
                    else
                    {
                        var messages = new List<TLMessageBase>() { messageBase };
                        if (messageBase.Id == 0 && messageBase.RandomId != 0L)
                        {
                            DeleteMessagesInternal(null, messages);
                            return;
                        }

                        DeleteMessages(null, null, messages, true, null, DeleteMessagesInternal);
                    }

                    if (dialog.BanUser)
                    {
                        var response = await ProtoService.EditBannedAsync(channel, message.From.ToInputUser(), new TLChannelBannedRights { IsEmbedLinks = true, IsSendGames = true, IsSendGifs = true, IsSendInline = true, IsSendMedia = true, IsSendMessages = true, IsSendStickers = true, IsViewMessages = true });
                        if (response.IsSucceeded)
                        {
                            var updates = response.Result as TLUpdates;
                            if (updates != null)
                            {
                                var newChannelMessageUpdate = updates.Updates.OfType<TLUpdateNewChannelMessage>().FirstOrDefault();
                                if (newChannelMessageUpdate != null)
                                {
                                    Aggregator.Publish(newChannelMessageUpdate.Message);
                                }
                            }
                        }
                    }

                    if (dialog.ReportSpam)
                    {
                        var response = await ProtoService.ReportSpamAsync(channel.ToInputChannel(), message.From.ToInputUser(), new TLVector<int> { message.Id });
                    }
                }
            }
            else
            {
                var dialog = new TLMessageDialog();
                dialog.Title = Strings.Android.Message;
                dialog.Message = string.Format(Strings.Android.AreYouSureDeleteMessages, LocaleHelper.Declension("Messages", 1));
                dialog.PrimaryButtonText = Strings.Android.OK;
                dialog.SecondaryButtonText = Strings.Android.Cancel;

                var chat = With as TLChat;

                if (message != null && (message.IsOut || (chat != null && (chat.IsCreator || chat.IsAdmin))) && message.ToId.Id != SettingsHelper.UserId && (Peer is TLInputPeerUser || Peer is TLInputPeerChat))
                {
                    var date = TLUtils.DateToUniversalTimeTLInt(ProtoService.ClientTicksDelta, DateTime.Now);
                    var config = CacheService.GetConfig();
                    if (config != null && message.Date + config.EditTimeLimit > date)
                    {
                        var user = With as TLUser;
                        if (user != null && !user.IsBot)
                        {
                            dialog.CheckBoxLabel = string.Format(Strings.Android.DeleteForUser, user.FullName);
                        }

                        //var chat = With as TLChat;
                        if (chat != null)
                        {
                            dialog.CheckBoxLabel = Strings.Android.DeleteForAll;
                        }
                    }
                }
                //else if (Peer is TLInputPeerUser && With is TLUser user && !user.IsSelf)
                //{
                //    dialog.Message += "\r\n\r\nThis will delete it just for you.";
                //}
                //else if (Peer is TLInputPeerChat)
                //{
                //    dialog.Message += "\r\n\r\nThis will delete it just for you, not for other participants of the chat.";
                //}
                //else if (Peer is TLInputPeerChannel)
                //{
                //    dialog.Message += "\r\n\r\nThis will delete it for everyone in this chat.";
                //}

                var result = await dialog.ShowQueuedAsync();
                if (result == ContentDialogResult.Primary)
                {
                    var revoke = dialog.IsChecked == true;

                    var messages = new List<TLMessageBase>() { messageBase };
                    if (messageBase.Id == 0 && messageBase.RandomId != 0L)
                    {
                        await TLMessageDialog.ShowAsync("This message has no ID, so it will be deleted locally only.", "Warning", "OK");

                        DeleteMessagesInternal(null, messages);
                        return;
                    }

                    DeleteMessages(null, null, messages, revoke, null, DeleteMessagesInternal);
                }
            }
        }

        private async Task<MTProtoResponse<TLMessagesAffectedHistory>> DeleteUserHistoryAsync(TLChannel channel, TLInputUserBase userId)
        {
            var response = await ProtoService.DeleteUserHistoryAsync(channel, userId);
            if (response.IsSucceeded)
            {
                if (response.Result.Offset > 0)
                {
                    return await DeleteUserHistoryAsync(channel, userId);
                }
            }
            else
            {
                Telegram.Api.Helpers.Execute.ShowDebugMessage("channels.deleteUserHistory error " + response.Error);
            }

            return response;
        }

        private void DeleteMessagesInternal(TLMessageBase lastMessage, IList<TLMessageBase> messages)
        {
            var cachedMessages = new TLVector<long>();
            var remoteMessages = new TLVector<int>();
            for (int i = 0; i < messages.Count; i++)
            {
                if (messages[i].RandomId.HasValue && messages[i].RandomId != 0L)
                {
                    cachedMessages.Add(messages[i].RandomId.Value);
                }
                if (messages[i].Id > 0)
                {
                    remoteMessages.Add(messages[i].Id);
                }
            }

            CacheService.DeleteMessages(Peer.ToPeer(), lastMessage, remoteMessages);
            CacheService.DeleteMessages(cachedMessages);

            BeginOnUIThread(() =>
            {
                var groups = new Dictionary<long, Tuple<TLMessage, GroupedMessages>>();

                for (int j = 0; j < messages.Count; j++)
                {
                    if (messages[j] is TLMessage grouped && grouped.HasGroupedId && grouped.GroupedId is long groupedId && _groupedMessages.TryGetValue(groupedId, out TLMessage group) && group.Media is TLMessageMediaGroup groupMedia)
                    {
                        groupMedia.Layout.Messages.Remove(grouped);
                        groups[groupedId] = Tuple.Create(group, groupMedia.Layout);
                    }
                }

                foreach (var group in groups.Values)
                {
                    if (group.Item2.Messages.Count > 0)
                    {
                        group.Item2.Calculate();
                        group.Item1.RaisePropertyChanged(() => group.Item1.Self);
                    }
                    else
                    {
                        _groupedMessages.TryRemove(group.Item2.GroupedId, out TLMessage removed);
                        Items.Remove(group.Item1);
                    }
                }

                for (int j = 0; j < messages.Count; j++)
                {
                    if (EditedMessage?.Id == messages[j].Id)
                    {
                        ClearReplyCommand.Execute();
                    }
                    else if (ReplyInfo?.ReplyToMsgId == messages[j].Id)
                    {
                        ClearReplyCommand.Execute();
                    }

                    if (PinnedMessage?.Id == messages[j].Id)
                    {
                        PinnedMessage = null;
                    }

                    if (Full is TLChannelFull channelFull && channelFull.PinnedMsgId == messages[j].Id)
                    {
                        channelFull.PinnedMsgId = null;
                        channelFull.HasPinnedMsgId = false;
                    }

                    Items.Remove(messages[j]);
                }

                RaisePropertyChanged(() => With);
                SelectionMode = ListViewSelectionMode.None;

                //this.IsEmptyDialog = (this.Items.get_Count() == 0 && this.LazyItems.get_Count() == 0);
                //this.NotifyOfPropertyChange<TLObject>(() => this.With);
            });
        }

        public async void DeleteMessages(TLMessageBase lastItem, IList<TLMessageBase> localMessages, IList<TLMessageBase> remoteMessages, bool revoke, Action<TLMessageBase, IList<TLMessageBase>> localCallback = null, Action<TLMessageBase, IList<TLMessageBase>> remoteCallback = null)
        {
            if (localMessages != null && localMessages.Count > 0)
            {
                localCallback?.Invoke(lastItem, localMessages);
            }
            if (remoteMessages != null && remoteMessages.Count > 0)
            {
                var messages = new TLVector<int>(remoteMessages.Select(x => x.Id).ToList());

                Task<MTProtoResponse<TLMessagesAffectedMessages>> task;

                if (Peer is TLInputPeerChannel)
                {
                    task = ProtoService.DeleteMessagesAsync(new TLInputChannel { ChannelId = ((TLInputPeerChannel)Peer).ChannelId, AccessHash = ((TLInputPeerChannel)Peer).AccessHash }, messages);
                }
                else
                {
                    task = ProtoService.DeleteMessagesAsync(messages, revoke);
                }

                var response = await task;
                if (response.IsSucceeded)
                {
                    remoteCallback?.Invoke(lastItem, remoteMessages);
                }
            }
        }

        #endregion

        #region Forward

        public RelayCommand<TLMessageBase> MessageForwardCommand { get; }
        private async void MessageForwardExecute(TLMessageBase messageBase)
        {
            if (messageBase is TLMessage message)
            {
                if (message.Media is TLMessageMediaGroup groupMedia)
                {
                    ExpandSelection(new[] { message });
                    MessagesForwardExecute();
                    return;
                }

                Search = null;
                SelectionMode = ListViewSelectionMode.None;

                await ShareView.Current.ShowAsync(message);
            }
        }

        #endregion

        #region Share

        public RelayCommand<TLMessage> MessageShareCommand { get; }
        private async void MessageShareExecute(TLMessage message)
        {
            await ShareView.Current.ShowAsync(message);
        }

        #endregion

        #region Multiple Delete

        public RelayCommand MessagesDeleteCommand { get; }
        private async void MessagesDeleteExecute()
        {
            var messages = new List<TLMessageCommonBase>(SelectedItems);

            for (int i = 0; i < messages.Count; i++)
            {
                if (messages[i] is TLMessage message && message.Media is TLMessageMediaGroup groupMedia)
                {
                    messages.RemoveAt(i);

                    for (int j = 0; j < groupMedia.Layout.Messages.Count; j++)
                    {
                        messages.Insert(i, groupMedia.Layout.Messages[j]);
                        i++;
                    }

                    i--;
                }
            }

            //if (messageBase == null) return;

            //var message = messageBase as TLMessage;
            //if (message != null && !message.IsOut && !message.IsPost && Peer is TLInputPeerChannel)
            //{
            //    var dialog = new DeleteChannelMessageDialog();

            //    var result = await dialog.ShowAsync();
            //    if (result == ContentDialogResult.Primary)
            //    {
            //        var channel = With as TLChannel;

            //        if (dialog.DeleteAll)
            //        {
            //            // TODO
            //        }
            //        else
            //        {
            //            var messages = new List<TLMessageBase>() { messageBase };
            //            if (messageBase.Id == 0 && messageBase.RandomId != 0L)
            //            {
            //                DeleteMessagesInternal(null, messages);
            //                return;
            //            }

            //            DeleteMessages(null, null, messages, true, null, DeleteMessagesInternal);
            //        }

            //        if (dialog.BanUser)
            //        {
            //            var response = await ProtoService.KickFromChannelAsync(channel, message.From.ToInputUser(), true);
            //            if (response.IsSucceeded)
            //            {
            //                var updates = response.Result as TLUpdates;
            //                if (updates != null)
            //                {
            //                    var newChannelMessageUpdate = updates.Updates.OfType<TLUpdateNewChannelMessage>().FirstOrDefault();
            //                    if (newChannelMessageUpdate != null)
            //                    {
            //                        Aggregator.Publish(newChannelMessageUpdate.Message);
            //                    }
            //                }
            //            }
            //        }

            //        if (dialog.ReportSpam)
            //        {
            //            var response = await ProtoService.ReportSpamAsync(channel.ToInputChannel(), message.From.ToInputUser(), new TLVector<int> { message.Id });
            //        }
            //    }
            //}
            //else
            {
                var dialog = new TLMessageDialog();
                dialog.Title = Strings.Android.Message;
                dialog.Message = string.Format(Strings.Android.AreYouSureDeleteMessages, LocaleHelper.Declension("Messages", messages.Count));
                dialog.PrimaryButtonText = Strings.Android.OK;
                dialog.SecondaryButtonText = Strings.Android.Cancel;

                var chat = With as TLChat;

                var isOut = messages.All(x => x.IsOut);
                var toId = messages.FirstOrDefault().ToId;
                var minDate = messages.OrderBy(x => x.Date).FirstOrDefault().Date;
                var maxDate = messages.OrderByDescending(x => x.Date).FirstOrDefault().Date;

                if ((isOut || (chat != null && (chat.IsCreator || chat.IsAdmin))) && toId.Id != SettingsHelper.UserId && (Peer is TLInputPeerUser || Peer is TLInputPeerChat))
                {
                    var date = TLUtils.DateToUniversalTimeTLInt(ProtoService.ClientTicksDelta, DateTime.Now);
                    var config = CacheService.GetConfig();
                    if (config != null && minDate + config.EditTimeLimit > date && maxDate + config.EditTimeLimit > date)
                    {
                        var user = With as TLUser;
                        if (user != null && !user.IsBot)
                        {
                            dialog.CheckBoxLabel = string.Format(Strings.Android.DeleteForUser, user.FullName);
                        }

                        //var chat = With as TLChat;
                        if (chat != null)
                        {
                            dialog.CheckBoxLabel = Strings.Android.DeleteForAll;
                        }
                    }
                }
                //else if (Peer is TLInputPeerUser && With is TLUser user && !user.IsSelf)
                //{
                //    dialog.Message += "\r\n\r\nThis will delete it just for you.";
                //}
                //else if (Peer is TLInputPeerChat)
                //{
                //    dialog.Message += "\r\n\r\nThis will delete it just for you, not for other participants of the chat.";
                //}
                //else if (Peer is TLInputPeerChannel)
                //{
                //    dialog.Message += "\r\n\r\nThis will delete it for everyone in this chat.";
                //}

                var result = await dialog.ShowQueuedAsync();
                if (result == ContentDialogResult.Primary)
                {
                    var revoke = dialog.IsChecked == true;

                    var localMessages = new List<TLMessageBase>();
                    var remoteMessages = new List<TLMessageBase>();
                    for (int i = 0; i < messages.Count; i++)
                    {
                        var message = messages[i];
                        if (message.Id == 0 && message.RandomId != 0L)
                        {
                            localMessages.Add(message);
                        }
                        else if (message.Id != 0)
                        {
                            remoteMessages.Add(message);
                        }
                    }

                    DeleteMessages(null, localMessages, remoteMessages, revoke, DeleteMessagesInternal, DeleteMessagesInternal);
                }
            }
        }

        private bool MessagesDeleteCanExecute()
        {
            return SelectedItems.Count > 0 && SelectedItems.All(messageCommon =>
            {
                var channel = _with as TLChannel;
                if (channel != null)
                {
                    if (messageCommon.Id == 1 && messageCommon.ToId is TLPeerChannel)
                    {
                        return false;
                    }

                    if (messageCommon.IsOut || channel.IsCreator || (channel.HasAdminRights && channel.AdminRights.IsDeleteMessages))
                    {
                        return true;
                    }

                    return false;
                }

                return true;
            });
        }

        #endregion

        #region Multiple Forward

        public RelayCommand MessagesForwardCommand { get; }
        private async void MessagesForwardExecute()
        {
            var messages = SelectedItems.OfType<TLMessage>().Where(x => x.RandomId == null).OrderBy(x => x.Id).ToList();
            if (messages.Count > 0)
            {
                Search = null;
                SelectionMode = ListViewSelectionMode.None;

                await ShareView.Current.ShowAsync(messages);
            }
        }

        private bool MessagesForwardCanExecute()
        {
            return SelectedItems.Count > 0 && SelectedItems.All(x =>
            {
                if (x is TLMessage message)
                {
                    if (message.Media is TLMessageMediaPhoto photoMedia)
                    {
                        return !photoMedia.HasTTLSeconds;
                    }
                    else if (message.Media is TLMessageMediaDocument documentMedia)
                    {
                        return !documentMedia.HasTTLSeconds;
                    }

                    return true;
                }

                return false;
            });
        }

        #endregion

        #region Multiple Copy

        public RelayCommand MessagesCopyCommand { get; }
        private void MessagesCopyExecute()
        {
            var messages = SelectedItems.OfType<TLMessage>().Where(x => x.Id != 0).OrderBy(x => x.Id).ToList();
            if (messages.Count > 0)
            {
                var builder = new StringBuilder();
                SelectionMode = ListViewSelectionMode.None;

                var broadcast = With is TLChannel check && check.IsBroadcast;

                foreach (var message in messages)
                {
                    var date = BindConvert.Current.DateTime(message.Date);
                    builder.AppendLine(string.Format("{0}, [{1} {2}]", broadcast ? message.Parent.DisplayName : message.From.FullName, BindConvert.Current.ShortDate.Format(date), BindConvert.Current.ShortTime.Format(date)));

                    if (message.HasFwdFrom && message.FwdFrom != null)
                    {
                        if (message.FwdFromChannel is TLChannel channel)
                        {
                            builder.AppendLine($"[{Strings.Android.ForwardedMessage}]");
                            builder.AppendLine($"[{Strings.Android.From} {channel.Title}]");
                        }
                        else if (message.FwdFromUser is TLUser user)
                        {
                            builder.AppendLine($"[{Strings.Android.ForwardedMessage}]");
                            builder.AppendLine($"[{Strings.Android.From} {user.FullName}]");
                        }
                    }

                    if (string.IsNullOrEmpty(message.Message))
                    {
                        if (message.Media is TLMessageMediaPhoto photoMedia)
                        {
                            builder.Append($"[{Strings.Android.AttachPhoto}]");

                            if (string.IsNullOrEmpty(photoMedia.Caption)) { }
                            else
                            {
                                builder.AppendLine();
                                builder.Append(photoMedia.Caption);
                            }
                        }
                        else if (message.Media is TLMessageMediaDocument documentMedia && documentMedia.Document is TLDocument document)
                        {
                            if (TLMessage.IsVoice(document))
                            {
                                builder.Append($"[{Strings.Android.AttachAudio}]");
                            }
                            else if (TLMessage.IsVideo(document))
                            {
                                builder.Append($"[{Strings.Android.AttachVideo}]");
                            }
                            else if (TLMessage.IsRoundVideo(document))
                            {
                                builder.Append($"[{Strings.Android.AttachRound}]");
                            }
                            else if (TLMessage.IsGif(document))
                            {
                                builder.Append($"[{Strings.Android.AttachGif}]");
                            }
                            else if (TLMessage.IsSticker(document))
                            {
                                var attribute = document.Attributes.OfType<TLDocumentAttributeSticker>().FirstOrDefault();
                                if (attribute != null)
                                {
                                    builder.Append($"[{attribute.Alt} {Strings.Android.AttachSticker}]");
                                }
                                else
                                {
                                    builder.Append($"[{Strings.Android.AttachSticker}]");
                                }
                            }
                            else if (TLMessage.IsMusic(document))
                            {
                                builder.Append($"[{Strings.Android.AttachMusic}]");
                            }

                            if (string.IsNullOrEmpty(documentMedia.Caption)) { }
                            else
                            {
                                builder.AppendLine();
                                builder.Append(documentMedia.Caption);
                            }
                        }
                        else if (message.Media is TLMessageMediaGeo geoMedia)
                        {
                            if (geoMedia.Geo is TLGeoPoint geo)
                            {
                                builder.AppendLine($"[{Strings.Android.AttachLocation}]");
                                builder.Append(string.Format(CultureInfo.InvariantCulture, "https://www.bing.com/maps/?pc=W8AP&FORM=MAPXSH&where1=44.312783,9.33426&locsearch=1", geo.Lat, geo.Long));
                            }
                        }
                        else if (message.Media is TLMessageMediaVenue venueMedia)
                        {
                            if (venueMedia.Geo is TLGeoPoint geo)
                            {
                                builder.AppendLine($"[{Strings.Android.AttachLocation}]");
                                builder.AppendLine(venueMedia.Title);
                                builder.AppendLine(venueMedia.Address);
                                builder.Append(string.Format(CultureInfo.InvariantCulture, "https://www.bing.com/maps/?pc=W8AP&FORM=MAPXSH&where1=44.312783,9.33426&locsearch=1", geo.Lat, geo.Long));
                            }
                        }
                    }
                    else
                    {
                        builder.Append(message.Message);
                    }

                    if (message != messages.Last())
                    {
                        builder.AppendLine();
                        builder.AppendLine();
                    }
                }

                var dataPackage = new DataPackage();
                dataPackage.SetText(builder.ToString());
                ClipboardEx.TrySetContent(dataPackage);
            }
        }

        private bool MessagesCopyCanExecute()
        {
            return SelectedItems.Count > 0;
        }

        #endregion

        #region Select

        public RelayCommand<TLMessageBase> MessageSelectCommand { get; }
        private void MessageSelectExecute(TLMessageBase message)
        {
            Search = null;

            var messageCommon = message as TLMessageCommonBase;
            if (messageCommon == null)
            {
                return;
            }

            SelectionMode = ListViewSelectionMode.Multiple;
            ListField.SelectedItems.Add(message);

            ExpandSelection(new[] { messageCommon });
        }

        #endregion

        #region Copy

        public RelayCommand<TLMessage> MessageCopyCommand { get; }
        private void MessageCopyExecute(TLMessage message)
        {
            if (message == null)
            {
                return;
            }

            string text = null;

            var media = message.Media as ITLMessageMediaCaption;
            if (media != null && !string.IsNullOrWhiteSpace(media.Caption))
            {
                text = media.Caption;
            }
            else if (!string.IsNullOrWhiteSpace(message.Message))
            {
                text = message.Message;
            }

            if (text != null)
            {
                var dataPackage = new DataPackage();
                dataPackage.SetText(text);
                ClipboardEx.TrySetContent(dataPackage);
            }
        }

        #endregion

        #region Copy media

        public RelayCommand<TLMessage> MessageCopyMediaCommand { get; }
        private async void MessageCopyMediaExecute(TLMessage message)
        {
            var photo = message.GetPhoto();
            var photoSize = photo?.Full as TLPhotoSize;
            if (photoSize == null)
            {
                return;
            }

            var location = photoSize.Location;
            var fileName = string.Format("{0}_{1}_{2}.jpg", location.VolumeId, location.LocalId, location.Secret);
            if (File.Exists(FileUtils.GetTempFileName(fileName)))
            {
                var result = await FileUtils.GetTempFileAsync(fileName);

                try
                {
                    var dataPackage = new DataPackage();
                    dataPackage.SetBitmap(RandomAccessStreamReference.CreateFromFile(result));
                    ClipboardEx.TrySetContent(dataPackage);
                }
                catch { }
            }
        }

        #endregion

        #region Copy link

        public RelayCommand<TLMessageCommonBase> MessageCopyLinkCommand { get; }
        private void MessageCopyLinkExecute(TLMessageCommonBase messageCommon)
        {
            if (messageCommon == null)
            {
                return;
            }

            var channel = With as TLChannel;
            if (channel == null)
            {
                return;
            }

            var link = $"{channel.Username}/{messageCommon.Id}";

            if (messageCommon is TLMessage message && message.IsRoundVideo())
            {
                link = $"https://telesco.pe/{link}";
            }
            else
            {
                link = MeUrlPrefixConverter.Convert(link);
            }

            var dataPackage = new DataPackage();
            dataPackage.SetText(link);
            ClipboardEx.TrySetContent(dataPackage);
        }

        #endregion

        #region Edit

        public RelayCommand MessageEditLastCommand { get; }
        private void MessageEditLastExecute()
        {
            var last = Items.LastOrDefault(x => x is TLMessage message && message.IsOut);
            if (last != null)
            {
                MessageEditCommand.Execute(last);
            }
        }

        public RelayCommand<TLMessage> MessageEditCommand { get; }
        private async void MessageEditExecute(TLMessage message)
        {
            Search = null;

            if (message?.Media is TLMessageMediaGroup groupMedia)
            {
                message = groupMedia.Layout.Messages.FirstOrDefault();
            }

            if (message == null)
            {
                return;
            }

            var response = await ProtoService.GetMessageEditDataAsync(Peer, message.Id);
            if (response.IsSucceeded)
            {
                BeginOnUIThread(() =>
                {
                    var messageEditText = GetMessageEditText(response.Result, message);
                    StartEditMessage(messageEditText, message);
                });
            }
            else
            {
                BeginOnUIThread(() =>
                {
                    //this.IsWorking = false;
                    //if (error.CodeEquals(ErrorCode.BAD_REQUEST) && error.TypeEquals(ErrorType.MESSAGE_ID_INVALID))
                    //{
                    //    MessageBox.Show(Strings.Resources.EditMessageError, Strings.Resources.Error, 0);
                    //    return;
                    //}
                    Execute.ShowDebugMessage("messages.getMessageEditData error " + response.Error);
                });
            }
        }

        public void StartEditMessage(string text, TLMessage message)
        {
            if (text == null)
            {
                return;
            }
            if (message == null)
            {
                return;
            }

            var config = CacheService.GetConfig();
            var editUntil = (config != null) ? (message.Date + config.EditTimeLimit + 300) : 0;
            if (message.FromId != null && message.ToId is TLPeerUser && message.FromId.Value == message.ToId.Id)
            {
                editUntil = 0;
            }

            Reply = new TLMessagesContainter
            {
                EditMessage = message,
                EditUntil = editUntil,
                // TODO: setup original content
                PreviousMessage = new TLMessage
                {
                    ToId = message.ToId,
                    FromId = message.FromId,
                    IsOut = message.IsOut
                }
            };

            SetText(text, message.Entities, true);

            //if (this._editMessageTimer == null)
            //{
            //    this._editMessageTimer = new DispatcherTimer();
            //    this._editMessageTimer.add_Tick(new EventHandler(this.OnEditMessageTimerTick));
            //    this._editMessageTimer.set_Interval(System.TimeSpan.FromSeconds(1.0));
            //}
            //this._editMessageTimer.Start();
            //this.IsEditingEnabled = true;
            //this.Text = text.ToString();

            CurrentInlineBot = null;

            //this.ClearStickerHints();
            //this.ClearInlineBotResults();
            //this.ClearUsernameHints();
            //this.ClearHashtagHints();
            //this.ClearCommandHints();
        }

        private string GetMessageEditText(TLMessagesMessageEditData editData, TLMessage message)
        {
            if (editData.IsCaption)
            {
                var mediaCaption = message.Media as ITLMessageMediaCaption;
                if (mediaCaption != null)
                {
                    return mediaCaption.Caption ?? string.Empty;
                }
            }
            else
            {
                return message.Message;
            }

            return null;

            //if (!editData.IsCaption)
            //{
            //    var text = message.Message.ToString();
            //    var stringBuilder = new StringBuilder();

            //    if (message != null && message.Entities != null && message.Entities.Count > 0)
            //    {
            //        //this.ClearMentions();

            //        if (message.Entities.FirstOrDefault(x => !(x is TLMessageEntityMentionName) && !(x is TLInputMessageEntityMentionName)) == null)
            //        {
            //            for (int i = 0; i < message.Entities.Count; i++)
            //            {
            //                int num = (i == 0) ? 0 : (message.Entities[i - 1].Offset + message.Entities[i - 1].Length);
            //                int num2 = (i == 0) ? message.Entities[i].Offset : (message.Entities[i].Offset - num);

            //                stringBuilder.Append(text.Substring(num, num2));

            //                var entityMentionName = message.Entities[i] as TLMessageEntityMentionName;
            //                if (entityMentionName != null)
            //                {
            //                    var user = CacheService.GetUser(entityMentionName.UserId);
            //                    if (user != null)
            //                    {
            //                        //this.AddMention(user);
            //                        string text2 = text.Substring(message.Entities[i].Offset, message.Entities[i].Length);
            //                        stringBuilder.Append(string.Format("@({0})", text2));
            //                    }
            //                }
            //                else
            //                {
            //                    var entityInputMentionName = message.Entities[i] as TLInputMessageEntityMentionName;
            //                    if (entityInputMentionName != null)
            //                    {
            //                        var inputUser = entityInputMentionName.UserId as TLInputUser;
            //                        if (inputUser != null)
            //                        {
            //                            TLUserBase user2 = this.CacheService.GetUser(inputUser.UserId);
            //                            if (user2 != null)
            //                            {
            //                                //this.AddMention(user2);
            //                                string text3 = text.Substring(message.Entities[i].Offset, message.Entities[i].Length);
            //                                stringBuilder.Append(string.Format("@({0})", text3));
            //                            }
            //                        }
            //                    }
            //                    else
            //                    {
            //                        num = message.Entities[i].Offset;
            //                        num2 = message.Entities[i].Length;
            //                        stringBuilder.Append(text.Substring(num, num2));
            //                    }
            //                }
            //            }

            //            var baseEntity = message.Entities[message.Entities.Count - 1];
            //            if (baseEntity != null)
            //            {
            //                stringBuilder.Append(text.Substring(baseEntity.Offset + baseEntity.Length));
            //            }
            //        }
            //        else
            //        {
            //            stringBuilder.Append(text);
            //        }
            //    }
            //    else
            //    {
            //        stringBuilder.Append(text);
            //    }

            //    return stringBuilder.ToString();
            //}

            //var mediaCaption = message.Media as ITLMediaCaption;
            //if (mediaCaption != null)
            //{
            //    return mediaCaption.Caption;
            //}

            //return null;
        }

        #endregion

        #region Pin

        public RelayCommand<TLMessageBase> MessagePinCommand { get; }
        private async void MessagePinExecute(TLMessageBase message)
        {
            if (PinnedMessage?.Id == message.Id)
            {
                var dialog = new TLMessageDialog();
                dialog.Title = Strings.Android.AppName;
                dialog.Message = Strings.Android.UnpinMessageAlert;
                dialog.PrimaryButtonText = Strings.Android.OK;
                dialog.SecondaryButtonText = Strings.Android.Cancel;

                var dialogResult = await dialog.ShowQueuedAsync();
                if (dialogResult == ContentDialogResult.Primary)
                {
                    var channel = Peer as TLInputPeerChannel;
                    var inputChannel = new TLInputChannel { ChannelId = channel.ChannelId, AccessHash = channel.AccessHash };

                    var result = await ProtoService.UpdatePinnedMessageAsync(false, inputChannel, 0);
                    if (result.IsSucceeded)
                    {
                        PinnedMessage = null;
                    }
                }
            }
            else
            {
                var channel = With as TLChannel;
                var dialog = new TLMessageDialog();
                dialog.Title = Strings.Android.AppName;
                dialog.Message = channel.IsBroadcast ? Strings.Android.PinMessageAlertChannel : Strings.Android.PinMessageAlert;
                dialog.PrimaryButtonText = Strings.Android.OK;
                dialog.SecondaryButtonText = Strings.Android.Cancel;

                if (channel.IsMegaGroup)
                {
                    dialog.CheckBoxLabel = Strings.Android.PinNotify;
                    dialog.IsChecked = true;
                }

                var dialogResult = await dialog.ShowQueuedAsync();
                if (dialogResult == ContentDialogResult.Primary)
                {
                    var inputChannel = channel.ToInputChannel();

                    var silent = dialog.IsChecked == false;
                    var result = await ProtoService.UpdatePinnedMessageAsync(silent, inputChannel, message.Id);
                    if (result.IsSucceeded)
                    {
                        var updates = result.Result as TLUpdates;
                        if (updates != null)
                        {
                            var newChannelMessageUpdate = updates.Updates.OfType<TLUpdateNewChannelMessage>().FirstOrDefault();
                            if (newChannelMessageUpdate != null)
                            {
                                Handle(newChannelMessageUpdate.Message as TLMessageCommonBase);
                                Aggregator.Publish(new TopMessageUpdatedEventArgs(_dialog, newChannelMessageUpdate.Message));
                            }
                        }

                        PinnedMessage = message;
                    }
                }
            }
        }

        #endregion

        #region Keyboard button

        private TLMessage _replyMarkupMessage;
        private TLReplyMarkupBase _replyMarkup;

        public TLMessage EditedMessage
        {
            get
            {
                if (Reply is TLMessagesContainter container)
                {
                    return container.EditMessage;
                }

                return null;
            }
        }

        public TLReplyMarkupBase ReplyMarkup
        {
            get
            {
                return _replyMarkup;
            }
            set
            {
                Set(ref _replyMarkup, value);
            }
        }

        private void SetReplyMarkup(TLMessage message)
        {
            if (Reply != null && message != null)
            {
                return;
            }

            if (message != null && message.ReplyMarkup != null)
            {
                if (message.ReplyMarkup is TLReplyInlineMarkup)
                {
                    return;
                }

                //var keyboardMarkup = message.ReplyMarkup as TLReplyKeyboardMarkup;
                //if (keyboardMarkup != null && keyboardMarkup.IsPersonal && !message.IsMention)
                //{
                //    return;
                //}

                var keyboardHide = message.ReplyMarkup as TLReplyKeyboardHide;
                if (keyboardHide != null && _replyMarkupMessage != null && _replyMarkupMessage.FromId.Value != message.FromId.Value)
                {
                    return;
                }

                var keyboardForceReply = message.ReplyMarkup as TLReplyKeyboardForceReply;
                if (keyboardForceReply != null /*&& !keyboardForceReply.HasResponse*/)
                {
                    _replyMarkupMessage = null;
                    ReplyMarkup = null;
                    Reply = message;
                    return;
                }

            }

            if (_replyMarkupMessage != null && _replyMarkupMessage.Id > message.Id)
            {
                return;
            }

            //this.SuppressOpenCommandsKeyboard = (message != null && message.ReplyMarkup != null && suppressOpenKeyboard);

            _replyMarkupMessage = message;
            ReplyMarkup = message.ReplyMarkup;
        }

        //public RelayCommand<TLKeyboardButtonBase> KeyboardButtonCommand { get; }
        public async void KeyboardButtonExecute(TLKeyboardButtonBase button, TLMessage message)
        {
            if (button is TLKeyboardButtonBuy buyButton)
            {
                if (message.Media is TLMessageMediaInvoice invoiceMedia && invoiceMedia.HasReceiptMsgId)
                {
                    var response = await ProtoService.GetPaymentReceiptAsync(invoiceMedia.ReceiptMsgId.Value);
                    if (response.IsSucceeded)
                    {
                        NavigationService.Navigate(typeof(PaymentReceiptPage), TLTuple.Create(message, response.Result));
                    }
                }
                else
                {
                    var response = await ProtoService.GetPaymentFormAsync(message.Id);
                    if (response.IsSucceeded)
                    {
                        if (response.Result.Invoice.IsEmailRequested || response.Result.Invoice.IsNameRequested || response.Result.Invoice.IsPhoneRequested || response.Result.Invoice.IsShippingAddressRequested)
                        {
                            NavigationService.NavigateToPaymentFormStep1(message, response.Result);
                        }
                        else if (response.Result.HasSavedCredentials)
                        {
                            if (ApplicationSettings.Current.TmpPassword != null)
                            {
                                if (ApplicationSettings.Current.TmpPassword.ValidUntil < TLUtils.Now + 60)
                                {
                                    ApplicationSettings.Current.TmpPassword = null;
                                }
                            }

                            if (ApplicationSettings.Current.TmpPassword != null)
                            {
                                NavigationService.NavigateToPaymentFormStep5(message, response.Result, null, null, null, null, null, true);
                            }
                            else
                            {
                                NavigationService.NavigateToPaymentFormStep4(message, response.Result, null, null, null);
                            }
                        }
                        else
                        {
                            NavigationService.NavigateToPaymentFormStep3(message, response.Result, null, null, null);
                        }
                    }
                }
            }
            else if (button is TLKeyboardButtonSwitchInline switchInlineButton)
            {
                var bot = GetBot(message);
                if (bot != null)
                {
                    if (switchInlineButton.IsSamePeer)
                    {
                        SetText(string.Format("@{0} {1}", bot.Username, switchInlineButton.Query), focus: true);
                        ResolveInlineBot(bot.Username, switchInlineButton.Query);

                        if (With is TLChatBase)
                        {
                            Reply = message;
                        }
                    }
                    else
                    {
                        await ForwardView.Current.ShowAsync(switchInlineButton, bot);
                    }
                }
            }
            else if (button is TLKeyboardButtonUrl urlButton)
            {
                var url = urlButton.Url;
                if (url.StartsWith("http") == false)
                {
                    url = "http://" + url;
                }

                if (Uri.TryCreate(url, UriKind.Absolute, out Uri uri))
                {
                    if (MessageHelper.IsTelegramUrl(uri))
                    {
                        MessageHelper.HandleTelegramUrl(urlButton.Url);
                    }
                    else
                    {
                        var confirm = await TLMessageDialog.ShowAsync(string.Format(Strings.Android.OpenUrlAlert, urlButton.Url), Strings.Android.AppName, Strings.Android.OK, Strings.Android.Cancel);
                        if (confirm != ContentDialogResult.Primary)
                        {
                            return;
                        }

                        await Launcher.LaunchUriAsync(uri);
                    }
                }
            }
            else if (button is TLKeyboardButtonCallback callbackButton)
            {
                var response = await ProtoService.GetBotCallbackAnswerAsync(Peer, message.Id, callbackButton.Data, false);
                if (response.IsSucceeded)
                {
                    if (response.Result.HasMessage)
                    {
                        if (response.Result.IsAlert)
                        {
                            await new TLMessageDialog(response.Result.Message).ShowQueuedAsync();
                        }
                        else
                        {
                            var date = TLUtils.DateToUniversalTimeTLInt(ProtoService.ClientTicksDelta, DateTime.Now);

                            var bot = GetBot(message);
                            if (bot == null)
                            {
                                // TODO:
                                await new TLMessageDialog(response.Result.Message).ShowQueuedAsync();
                                return;
                            }

                            InformativeMessage = TLUtils.GetShortMessage(0, bot.Id, Peer.ToPeer(), date, response.Result.Message);
                        }
                    }
                    else if (response.Result.HasUrl && response.Result.IsHasUrl /* ??? */)
                    {
                        var url = response.Result.Url;
                        if (url.StartsWith("http") == false)
                        {
                            url = "http://" + url;
                        }

                        if (Uri.TryCreate(url, UriKind.Absolute, out Uri uri))
                        {
                            if (MessageHelper.IsTelegramUrl(uri))
                            {
                                MessageHelper.HandleTelegramUrl(response.Result.Url);
                            }
                            else
                            {
                                //var dialog = new TLMessageDialog(response.Result.Url, "Open this link?");
                                //dialog.PrimaryButtonText = "OK";
                                //dialog.SecondaryButtonText = "Cancel";

                                //var result = await dialog.ShowQueuedAsync();
                                //if (result != ContentDialogResult.Primary)
                                //{
                                //    return;
                                //}

                                await Launcher.LaunchUriAsync(uri);
                            }
                        }
                    }
                }
            }
            else if (button is TLKeyboardButtonGame gameButton)
            {
                var gameMedia = message.Media as TLMessageMediaGame;
                if (gameMedia != null)
                {
                    var response = await ProtoService.GetBotCallbackAnswerAsync(Peer, message.Id, null, true);
                    if (response.IsSucceeded && response.Result.IsHasUrl && response.Result.HasUrl)
                    {
                        if (CacheService.GetUser(message.ViaBotId) is TLUser user)
                        {
                            NavigationService.Navigate(typeof(GamePage), new TLTuple<string, string, string, TLMessage>(gameMedia.Game.Title, user.Username, response.Result.Url, message));
                        }
                        else
                        {
                            NavigationService.Navigate(typeof(GamePage), new TLTuple<string, string, string, TLMessage>(gameMedia.Game.Title, string.Empty, response.Result.Url, message));
                        }
                    }
                }
            }
            else if (button is TLKeyboardButtonRequestPhone requestPhoneButton)
            {
                if (CacheService.GetUser(SettingsHelper.UserId) is TLUser cached)
                {
                    var content = Strings.Android.AreYouSureShareMyContactInfo;
                    if (With is TLUser withUser)
                    {
                        content = withUser.IsBot ? Strings.Android.AreYouSureShareMyContactInfoBot : string.Format(Strings.Android.AreYouSureShareMyContactInfoUser, PhoneNumber.Format(cached.Phone), withUser.FullName);
                    }

                    var confirm = await TLMessageDialog.ShowAsync(content, Strings.Android.ShareYouPhoneNumberTitle, Strings.Android.OK, Strings.Android.Cancel);
                    if (confirm == ContentDialogResult.Primary)
                    {
                        await SendContactAsync(cached);
                    }
                }
            }
            else if (button is TLKeyboardButtonRequestGeoLocation requestGeoButton)
            {
                var confirm = await TLMessageDialog.ShowAsync(Strings.Android.ShareYouLocationInfo, Strings.Android.ShareYouLocationTitle, Strings.Android.OK, Strings.Android.Cancel);
                if (confirm == ContentDialogResult.Primary)
                {
                    var location = await _locationService.GetPositionAsync();
                    if (location != null)
                    {
                        await SendGeoAsync(location.Point.Position.Latitude, location.Point.Position.Longitude);
                    }
                }
            }
            else if (button is TLKeyboardButton keyboardButton)
            {
                await SendMessageAsync(keyboardButton.Text, null, true);
            }
        }

        #endregion

        #region Open reply

        public RelayCommand<TLMessageCommonBase> MessageOpenReplyCommand { get; }
        private async void MessageOpenReplyExecute(TLMessageCommonBase messageCommon)
        {
            if (messageCommon != null && messageCommon.ReplyToMsgId.HasValue)
            {
                await LoadMessageSliceAsync(messageCommon.Id, messageCommon.ReplyToMsgId.Value);
            }
        }

        #endregion

        #region Sticker info

        public RelayCommand<TLMessage> MessageStickerPackInfoCommand { get; }
        private async void MessageStickerPackInfoExecute(TLMessage message)
        {
            if (message?.Media is TLMessageMediaDocument documentMedia && documentMedia.Document is TLDocument document)
            {
                var stickerAttribute = document.Attributes.OfType<TLDocumentAttributeSticker>().FirstOrDefault();
                if (stickerAttribute != null && stickerAttribute.StickerSet.TypeId != TLType.InputStickerSetEmpty)
                {
                    await StickerSetView.Current.ShowAsync(stickerAttribute.StickerSet);
                }
            }
        }

        #endregion

        #region Fave sticker

        public RelayCommand<TLMessage> MessageFaveStickerCommand { get; }
        private void MessageFaveStickerExecute(TLMessage message)
        {
            if (message.Media is TLMessageMediaDocument documentMedia && documentMedia.Document is TLDocument document)
            {
                _stickersService.AddRecentSticker(StickerType.Fave, document, (int)(Utils.CurrentTimestamp / 1000), false);
            }
        }

        #endregion

        #region Unfave sticker

        public RelayCommand<TLMessage> MessageUnfaveStickerCommand { get; }
        private void MessageUnfaveStickerExecute(TLMessage message)
        {
            if (message.Media is TLMessageMediaDocument documentMedia && documentMedia.Document is TLDocument document)
            {
                _stickersService.AddRecentSticker(StickerType.Fave, document, (int)(Utils.CurrentTimestamp / 1000), true);
            }
        }

        #endregion

        #region Save sticker as

        public RelayCommand<TLMessage> MessageSaveStickerCommand { get; }
        private async void MessageSaveStickerExecute(TLMessage message)
        {
            if (message?.Media is TLMessageMediaDocument documentMedia && documentMedia.Document is TLDocument document)
            {
                var fileName = document.GetFileName();
                if (File.Exists(FileUtils.GetTempFileName(fileName)))
                {
                    var picker = new FileSavePicker();
                    picker.FileTypeChoices.Add("WebP image", new[] { ".webp" });
                    picker.FileTypeChoices.Add("PNG image", new[] { ".png" });
                    picker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
                    picker.SuggestedFileName = "sticker.webp";

                    var fileNameAttribute = document.Attributes.OfType<TLDocumentAttributeFilename>().FirstOrDefault();
                    if (fileNameAttribute != null)
                    {
                        picker.SuggestedFileName = fileNameAttribute.FileName;
                    }

                    var file = await picker.PickSaveFileAsync();
                    if (file != null)
                    {
                        var sticker = await FileUtils.GetTempFileAsync(fileName);

                        if (Path.GetExtension(file.Name).Equals(".webp"))
                        {
                            await sticker.CopyAndReplaceAsync(file);
                        }
                        else if (Path.GetExtension(file.Name).Equals(".png"))
                        {
                            var buffer = await FileIO.ReadBufferAsync(sticker);
                            var bitmap = WebPImage.DecodeFromBuffer(buffer);

                            using (var stream = await file.OpenAsync(FileAccessMode.ReadWrite))
                            {
                                var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
                                var pixelStream = bitmap.PixelBuffer.AsStream();
                                var pixels = new byte[pixelStream.Length];

                                await pixelStream.ReadAsync(pixels, 0, pixels.Length);

                                encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied, (uint)bitmap.PixelWidth, (uint)bitmap.PixelHeight, 96.0, 96.0, pixels);
                                await encoder.FlushAsync();
                            }
                        }
                    }
                }
            }
        }

        #endregion

        #region Save file as

        public RelayCommand<TLMessage> MessageSaveMediaCommand { get; }
        private async void MessageSaveMediaExecute(TLMessage message)
        {
            if (message.IsSticker())
            {
                MessageSaveStickerExecute(message);
                return;
            }

            var photo = message.GetPhoto();
            if (photo?.Full is TLPhotoSize photoSize)
            {
                await TLFileHelper.SavePhotoAsync(photoSize, message.Date, false);
            }

            var document = message.GetDocument();
            if (document != null)
            {
                await TLFileHelper.SaveDocumentAsync(document, message.Date, false);
            }
        }

        #endregion

        #region Save to Downloads

        public RelayCommand<TLMessage> MessageSaveDownloadCommand { get; }
        private async void MessageSaveDownloadExecute(TLMessage message)
        {
            if (message.IsSticker())
            {
                MessageSaveStickerExecute(message);
                return;
            }

            var photo = message.GetPhoto();
            if (photo?.Full is TLPhotoSize photoSize)
            {
                await TLFileHelper.SavePhotoAsync(photoSize, message.Date, true);
            }

            var document = message.GetDocument();
            if (document != null)
            {
                await TLFileHelper.SaveDocumentAsync(document, message.Date, true);
            }
        }

        #endregion

        #region Save to GIFs

        public RelayCommand<TLMessage> MessageSaveGIFCommand { get; }
        private async void MessageSaveGIFExecute(TLMessage message)
        {
            TLDocument document = null;
            if (message?.Media is TLMessageMediaDocument documentMedia)
            {
                document = documentMedia.Document as TLDocument;
            }
            else if (message?.Media is TLMessageMediaWebPage webPageMedia && webPageMedia.WebPage is TLWebPage webPage)
            {
                document = webPage.Document as TLDocument;
            }

            if (document == null)
            {
                return;
            }

            var response = await ProtoService.SaveGifAsync(new TLInputDocument { Id = document.Id, AccessHash = document.AccessHash }, false);
            if (response.IsSucceeded)
            {
                _stickers.StickersService.AddRecentGif(document, (int)(Utils.CurrentTimestamp / 1000));
            }
        }

        #endregion

        #region Add contact

        public RelayCommand<TLMessage> MessageAddContactCommand { get; }
        private async void MessageAddContactExecute(TLMessage message)
        {
            var contactMedia = message.Media as TLMessageMediaContact;
            if (contactMedia == null)
            {
                return;
            }

            var user = contactMedia.User as TLUser;
            if (user == null)
            {
                return;
            }

            var confirm = await EditUserNameView.Current.ShowAsync(user.FirstName, user.LastName);
            if (confirm != ContentDialogResult.Primary)
            {
                return;
            }

            var contact = new TLInputPhoneContact
            {
                ClientId = user.Id,
                FirstName = EditUserNameView.Current.FirstName,
                LastName = EditUserNameView.Current.LastName,
                Phone = contactMedia.PhoneNumber
            };

            var response = await ProtoService.ImportContactsAsync(new TLVector<TLInputContactBase> { contact });
            if (response.IsSucceeded)
            {
                if (response.Result.Users.Count > 0)
                {
                    Aggregator.Publish(new TLUpdateContactLink
                    {
                        UserId = response.Result.Users[0].Id,
                        MyLink = new TLContactLinkContact(),
                        ForeignLink = new TLContactLinkUnknown()
                    });
                }

                user.RaisePropertyChanged(() => user.HasFirstName);
                user.RaisePropertyChanged(() => user.HasLastName);
                user.RaisePropertyChanged(() => user.FirstName);
                user.RaisePropertyChanged(() => user.LastName);
                user.RaisePropertyChanged(() => user.FullName);
                user.RaisePropertyChanged(() => user.DisplayName);

                user.RaisePropertyChanged(() => user.HasPhone);
                user.RaisePropertyChanged(() => user.Phone);

                var dialog = CacheService.GetDialog(user.ToPeer());
                if (dialog != null)
                {
                    dialog.RaisePropertyChanged(() => dialog.With);
                }
            }
        }

        #endregion

        #region Service message

        public RelayCommand<TLMessageService> MessageServiceCommand { get; }
        private async void MessageServiceExecute(TLMessageService message)
        {
            if (message.Action is TLMessageActionDate)
            {
                var date = BindConvert.Current.DateTime(message.Date);

                var dialog = new Controls.Views.CalendarView();
                dialog.MaxDate = DateTimeOffset.Now.Date;
                dialog.SelectedDates.Add(date);

                var confirm = await dialog.ShowQueuedAsync();
                if (confirm == ContentDialogResult.Primary && dialog.SelectedDates.Count > 0)
                {
                    var offset = TLUtils.DateToUniversalTimeTLInt(ProtoService.ClientTicksDelta, date);
                    await LoadDateSliceAsync(offset);
                }
            }
            else if (message.Action is TLMessageActionPinMessage && message.ReplyToMsgId is int reply)
            {
                await LoadMessageSliceAsync(message.Id, reply);
            }
        }

        #endregion
    }
}
