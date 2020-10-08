using Microsoft.Graphics.Canvas.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Common.Chats;
using Unigram.Controls.Messages;
using Unigram.Converters;
using Unigram.Navigation;
using Unigram.Navigation.Services;
using Unigram.Services;
using Unigram.ViewModels.Delegates;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation.Peers;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;

namespace Unigram.Controls.Cells
{
    public enum MessageTicksState
    {
        None,
        Pending,
        Failed,
        Sent,
        Read
    }

    public sealed partial class ChatCell : ToggleButton
    {
        private Chat _chat;
        private ChatList _chatList;

        private IProtoService _protoService;
        private IChatListDelegate _delegate;

        private Visual _onlineBadge;

        private bool _expanded = false;

        private MessageTicksState _ticksState;

        public ChatCell()
        {
            InitializeComponent();
            InitializeSelection();
            InitializeTicks();

            _onlineBadge = ElementCompositionPreview.GetElementVisual(OnlineBadge);
            _onlineBadge.CenterPoint = new Vector3(6.5f);
            _onlineBadge.Opacity = 0;
            _onlineBadge.Scale = new Vector3(0);
        }

        protected override AutomationPeer OnCreateAutomationPeer()
        {
            return new ChatCellAutomationPeer(this);
        }

        public void UpdateService(IProtoService protoService, IChatListDelegate delegato)
        {
            _protoService = protoService;
            _delegate = delegato;
        }

        public void UpdateChat(IProtoService protoService, IChatListDelegate delegato, Chat chat, ChatList chatList)
        {
            _protoService = protoService;
            _delegate = delegato;

            Update(chat, chatList);
        }

        public void UpdateMessage(IProtoService protoService, IChatListDelegate delegato, Message message)
        {
            _protoService = protoService;
            _delegate = delegato;

            var chat = protoService.GetChat(message.ChatId);
            if (chat == null)
            {
                return;
            }

            UpdateChatTitle(chat);
            UpdateChatPhoto(chat);
            UpdateChatType(chat);
            UpdateNotificationSettings(chat);

            UpdateMinithumbnail(message);

            PinnedIcon.Visibility = Visibility.Collapsed;
            UnreadBadge.Visibility = Visibility.Collapsed;
            UnreadMentionsBadge.Visibility = Visibility.Collapsed;

            DraftLabel.Text = string.Empty;
            FromLabel.Text = UpdateFromLabel(chat, message);
            BriefLabel.Text = UpdateBriefLabel(chat, message, true, false);
            TimeLabel.Text = UpdateTimeLabel(message);
            StateIcon.Glyph = UpdateStateIcon(chat.LastReadOutboxMessageId, chat, null, message, message.SendingState);
        }

        public async void UpdateChatList(IProtoService protoService, IChatListDelegate delegato, ChatList chatList)
        {
            _protoService = protoService;
            _delegate = delegato;

            TitleLabel.Text = Strings.Resources.ArchivedChats;
            Photo.Source = PlaceholderHelper.GetGlyph(Icons.Archive, 0, 96);

            TypeIcon.Text = string.Empty;
            TypeIcon.Visibility = Visibility.Collapsed;
            VerifiedIcon.Visibility = Visibility.Collapsed;
            UnreadMentionsBadge.Visibility = Visibility.Collapsed;
            PinnedIcon.Visibility = Visibility.Collapsed;

            DraftLabel.Text = string.Empty;
            TimeLabel.Text = string.Empty;
            StateIcon.Glyph = string.Empty;
            FailedBadge.Visibility = Visibility.Collapsed;

            MutedIcon.Visibility = Visibility.Collapsed;

            MinithumbnailPanel.Visibility = Visibility.Collapsed;

            VisualStateManager.GoToState(LayoutRoot, "Muted", false);

            UpdateTicks(null);

            var unreadCount = protoService.GetUnreadCount(chatList);
            UnreadBadge.Visibility = unreadCount.UnreadChatCount.UnreadCount > 0 ? Visibility.Visible : Visibility.Collapsed;
            UnreadLabel.Text = $"{unreadCount.UnreadChatCount.UnreadCount}";

            var response = await protoService.GetChatListAsync(chatList, 0, 20);
            if (response is Telegram.Td.Api.Chats chats)
            {
                Visibility = chats.ChatIds.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
                BriefInfo.Inlines.Clear();

                foreach (var id in chats.ChatIds)
                {
                    var chat = protoService.GetChat(id);
                    if (chat == null)
                    {
                        continue;
                    }

                    if (BriefInfo.Inlines.Count > 0)
                    {
                        BriefInfo.Inlines.Add(new Run { Text = ", " });
                    }

                    var run = new Run { Text = chat.Title };
                    if (chat.IsUnread())
                    {
                        run.Foreground = App.Current.Resources["ListViewItemForegroundSelected"] as Brush;
                    }

                    BriefInfo.Inlines.Add(run);
                }
            }
        }

        public string GetAutomationName()
        {
            if (_protoService == null || _chat == null)
            {
                return null;
            }

            return UpdateAutomation(_protoService, _chat, _chat.LastMessage);
        }

        private string UpdateAutomation(IProtoService protoService, Chat chat, Message message)
        {
            var builder = new StringBuilder();
            if (chat.Type is ChatTypeSecret)
            {
                builder.Append(Strings.Resources.AccDescrSecretChat);
                builder.Append(". ");
            }

            if (chat.Type is ChatTypePrivate || chat.Type is ChatTypeSecret)
            {
                var user = protoService.GetUser(chat);
                if (user != null)
                {
                    if (user.Type is UserTypeBot)
                    {
                        builder.Append(Strings.Resources.Bot);
                        builder.Append(", ");
                    }
                    if (user.Id == protoService.Options.MyId)
                    {
                        builder.Append(Strings.Resources.SavedMessages);
                    }
                    else
                    {
                        builder.Append(user.GetFullName());
                    }

                    builder.Append(", ");
                }
            }
            else
            {
                if (chat.Type is ChatTypeSupergroup super && super.IsChannel)
                {
                    builder.Append(Strings.Resources.AccDescrChannel);
                }
                else
                {
                    builder.Append(Strings.Resources.AccDescrGroup);
                }

                builder.Append(", ");
                builder.Append(protoService.GetTitle(chat));
                builder.Append(", ");
            }

            if (chat.UnreadCount > 0)
            {
                builder.Append(Locale.Declension("NewMessages", chat.UnreadCount));
                builder.Append(", ");
            }

            if (message == null)
            {
                //AutomationProperties.SetName(this, builder.ToString());
                return builder.ToString();
            }

            //if (!message.IsOutgoing && message.SenderUserId != 0 && !message.IsService())
            if (ShowFrom(chat, message))
            {
                var fromUser = protoService.GetUser(message.SenderUserId);
                if (fromUser != null)
                {
                    if (message.IsOutgoing)
                    {
                        if (!(chat.Type is ChatTypePrivate priv && priv.UserId == message.SenderUserId) && !message.IsChannelPost)
                        {
                            builder.Append(Strings.Resources.FromYou);
                            builder.Append(": ");
                        }
                    }
                    else
                    {
                        builder.Append(fromUser.GetFullName());
                        builder.Append(": ");
                    }
                }
            }

            if (chat.Type is ChatTypeSecret == false)
            {
                builder.Append(Automation.GetSummary(protoService, message));
            }

            var date = Locale.FormatDateAudio(message.Date);
            if (message.IsOutgoing)
            {
                builder.Append(string.Format(Strings.Resources.AccDescrSentDate, date));
            }
            else
            {
                builder.Append(string.Format(Strings.Resources.AccDescrReceivedDate, date));
            }

            //AutomationProperties.SetName(this, builder.ToString());
            return builder.ToString();
        }

        #region Updates

        public void UpdateChatLastMessage(Chat chat, ChatPosition position = null)
        {
            if (position == null)
            {
                position = chat.GetPosition(_chatList);
            }

            DraftLabel.Text = UpdateDraftLabel(chat);
            FromLabel.Text = UpdateFromLabel(chat, position);
            BriefLabel.Text = UpdateBriefLabel(chat, position);
            TimeLabel.Text = UpdateTimeLabel(chat, position);
            StateIcon.Glyph = UpdateStateIcon(chat.LastReadOutboxMessageId, chat, chat.DraftMessage, chat.LastMessage, chat.LastMessage?.SendingState);
            FailedBadge.Visibility = chat.LastMessage?.SendingState is MessageSendingStateFailed ? Visibility.Visible : Visibility.Collapsed;

            UpdateMinithumbnail(chat.LastMessage);
        }

        public void UpdateChatReadInbox(Chat chat, ChatPosition position = null)
        {
            if (position == null)
            {
                position = chat.GetPosition(_chatList);
            }

            PinnedIcon.Visibility = (chat.UnreadCount == 0 && !chat.IsMarkedAsUnread) && (position?.IsPinned ?? false) ? Visibility.Visible : Visibility.Collapsed;
            UnreadBadge.Visibility = (chat.UnreadCount > 0 || chat.IsMarkedAsUnread) ? chat.UnreadMentionCount == 1 && chat.UnreadCount == 1 ? Visibility.Collapsed : Visibility.Visible : Visibility.Collapsed;
            UnreadLabel.Text = chat.UnreadCount > 0 ? chat.UnreadCount.ToString() : string.Empty;

            //UpdateAutomation(_protoService, chat, chat.LastMessage);
        }

        public void UpdateChatReadOutbox(Chat chat)
        {
            StateIcon.Glyph = UpdateStateIcon(chat.LastReadOutboxMessageId, chat, chat.DraftMessage, chat.LastMessage, chat.LastMessage?.SendingState);
        }

        public void UpdateChatIsMarkedAsUnread(Chat chat)
        {

        }

        public void UpdateChatUnreadMentionCount(Chat chat, ChatPosition position = null)
        {
            UpdateChatReadInbox(chat, position);
            UnreadMentionsBadge.Visibility = chat.UnreadMentionCount > 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        public void UpdateNotificationSettings(Chat chat)
        {
            var muted = _protoService.GetNotificationSettingsMuteFor(chat) > 0;
            VisualStateManager.GoToState(LayoutRoot, muted ? "Muted" : "Unmuted", false);
            MutedIcon.Visibility = muted ? Visibility.Visible : Visibility.Collapsed;
        }

        public void UpdateChatTitle(Chat chat)
        {
            TitleLabel.Text = _protoService.GetTitle(chat);
        }

        public void UpdateChatPhoto(Chat chat)
        {
            Photo.Source = PlaceholderHelper.GetChat(_protoService, chat, 48);
        }

        public void UpdateFile(Chat chat, File file)
        {
            if (chat.Type is ChatTypePrivate privata && privata.UserId == _protoService.Options.MyId)
            {
                Photo.Source = PlaceholderHelper.GetSavedMessages(privata.UserId, 96);
            }
            else
            {
                Photo.Source = PlaceholderHelper.GetChat(null, chat, 48);
            }
        }

        public void UpdateChatActions(Chat chat, IDictionary<int, ChatAction> actions)
        {
            if (actions != null && actions.Count > 0)
            {
                TypingLabel.Text = InputChatActionManager.GetTypingString(chat, actions, _protoService.GetUser, out ChatAction commonAction);
                TypingLabel.Visibility = Visibility.Visible;
                BriefInfo.Visibility = Visibility.Collapsed;
                Minithumbnail.Visibility = Visibility.Collapsed;
            }
            else
            {
                TypingLabel.Visibility = Visibility.Collapsed;
                BriefInfo.Visibility = Visibility.Visible;
                Minithumbnail.Visibility = Visibility.Visible;
            }
        }

        private void UpdateChatType(Chat chat)
        {
            var type = UpdateType(chat);
            TypeIcon.Text = type ?? string.Empty;
            TypeIcon.Visibility = type == null ? Visibility.Collapsed : Visibility.Visible;

            var verified = false;
            if (chat.Type is ChatTypePrivate privata)
            {
                verified = _protoService.GetUser(privata.UserId)?.IsVerified ?? false;
            }
            else if (chat.Type is ChatTypeSupergroup super)
            {
                verified = _protoService.GetSupergroup(super.SupergroupId)?.IsVerified ?? false;
            }

            VerifiedIcon.Visibility = verified ? Visibility.Visible : Visibility.Collapsed;
        }

        public void UpdateUserStatus(Chat chat, UserStatus status)
        {
            UpdateOnlineBadge(status is UserStatusOnline);
        }

        private void UpdateOnlineBadge(bool visible)
        {
            OnlineBadge.Visibility = Visibility.Visible;

            var scale = _onlineBadge.Compositor.CreateVector3KeyFrameAnimation();
            //scale.InsertKeyFrame(0, new System.Numerics.Vector3(visible ? 0 : 1));
            scale.InsertKeyFrame(1, new System.Numerics.Vector3(visible ? 1 : 0));

            var opacity = _onlineBadge.Compositor.CreateScalarKeyFrameAnimation();
            //opacity.InsertKeyFrame(0, visible ? 0 : 1);
            opacity.InsertKeyFrame(1, visible ? 1 : 0);

            _onlineBadge.StopAnimation("Scale");
            _onlineBadge.StopAnimation("Opacity");

            _onlineBadge.StartAnimation("Scale", scale);
            _onlineBadge.StartAnimation("Opacity", opacity);
        }

        private void Update(Chat chat, ChatList chatList)
        {
            _chat = chat;
            _chatList = chatList;

            Tag = chat;

            var position = chat.GetPosition(chatList);

            //UpdateViewState(chat, ChatFilterMode.None, false, false);

            UpdateChatTitle(chat);
            UpdateChatPhoto(chat);
            UpdateChatType(chat);

            UpdateChatLastMessage(chat, position);
            //UpdateChatReadInbox(chat);
            UpdateChatUnreadMentionCount(chat, position);
            UpdateNotificationSettings(chat);
            UpdateChatActions(chat, _protoService.GetChatActions(chat.Id));

            var user = _protoService.GetUser(chat);
            if (user != null && user.Type is UserTypeRegular && user.Id != _protoService.Options.MyId && user.Id != 777000)
            {
                UpdateUserStatus(chat, user.Status);
            }
            else
            {
                OnlineBadge.Visibility = Visibility.Collapsed;
            }
        }

        #endregion

        public void UpdateViewState(Chat chat, bool selected, bool compact, bool threeLines)
        {
            VisualStateManager.GoToState(LayoutRoot, selected ? "Selected" : chat.Type is ChatTypeSecret ? "Secret" : "Normal", false);
            VisualStateManager.GoToState(LayoutRoot, compact ? "Compact" : "Expanded", false);
            VisualStateManager.GoToState(LayoutRoot, threeLines ? "ThreeLines" : "Default", false);

            if (threeLines != _expanded && _protoService != null)
            {
                _expanded = threeLines;

                var position = chat.GetPosition(_chatList);

                DraftLabel.Text = UpdateDraftLabel(chat);
                FromLabel.Text = UpdateFromLabel(chat, position);
                BriefLabel.Text = UpdateBriefLabel(chat, position);
            }
        }

        public void UpdateViewState(ChatList chatList, bool selected, bool compact, bool threeLines)
        {
            VisualStateManager.GoToState(LayoutRoot, selected ? "Selected" : "Normal", false);
            VisualStateManager.GoToState(LayoutRoot, compact ? "Compact" : "Expanded", false);
            VisualStateManager.GoToState(LayoutRoot, threeLines ? "ThreeLines" : "Default", false);

            if (threeLines != _expanded && _protoService != null)
            {
                _expanded = threeLines;

                UpdateChatList(_protoService, _delegate, chatList);
            }
        }

        private async void UpdateMinithumbnail(Message message)
        {
            var thumbnail = message?.GetMinithumbnail(false);
            if (thumbnail != null && SettingsService.Current.Diagnostics.Minithumbnails)
            {
                double ratioX = (double)16 / thumbnail.Width;
                double ratioY = (double)16 / thumbnail.Height;
                double ratio = Math.Max(ratioX, ratioY);

                var width = (int)(thumbnail.Width * ratio);
                var height = (int)(thumbnail.Height * ratio);

                var bitmap = new BitmapImage { DecodePixelWidth = width, DecodePixelHeight = height, DecodePixelType = DecodePixelType.Logical };
                var bytes = thumbnail.Data.ToArray();

                var stream = new System.IO.MemoryStream(bytes);
                var random = System.IO.WindowsRuntimeStreamExtensions.AsRandomAccessStream(stream);

                Minithumbnail.Source = bitmap;
                MinithumbnailPanel.Visibility = Visibility.Visible;

                await bitmap.SetSourceAsync(random);
            }
            else
            {
                MinithumbnailPanel.Visibility = Visibility.Collapsed;
                Minithumbnail.Source = null;
            }
        }

        private string UpdateBriefLabel(Chat chat, ChatPosition position)
        {
            if (position?.Source is ChatSourcePublicServiceAnnouncement psa && !string.IsNullOrEmpty(psa.Text))
            {
                return psa.Text.Replace('\n', ' ');
            }

            var topMessage = chat.LastMessage;
            if (topMessage != null)
            {
                return UpdateBriefLabel(chat, topMessage, true, true);
            }
            else if (chat.Type is ChatTypeSecret secretType)
            {
                var secret = _protoService.GetSecretChat(secretType.SecretChatId);
                if (secret != null)
                {
                    if (secret.State is SecretChatStateReady)
                    {
                        return secret.IsOutbound ? string.Format(Strings.Resources.EncryptedChatStartedOutgoing, _protoService.GetTitle(chat)) : Strings.Resources.EncryptedChatStartedIncoming;
                    }
                    else if (secret.State is SecretChatStatePending)
                    {
                        return string.Format(Strings.Resources.AwaitingEncryption, _protoService.GetTitle(chat));
                    }
                    else if (secret.State is SecretChatStateClosed)
                    {
                        return Strings.Resources.EncryptionRejected;
                    }
                }
            }

            return string.Empty;
        }

        private string UpdateBriefLabel(Chat chat, Message value, bool showContent, bool draft)
        {
            //if (ViewModel.DraftMessage is DraftMessage draft && !string.IsNullOrWhiteSpace(draft.InputMessageText.ToString()))
            //{
            //    return draft.Message;
            //}

            if (chat.DraftMessage != null && draft)
            {
                switch (chat.DraftMessage.InputMessageText)
                {
                    case InputMessageText text:
                        return text.Text.Text.Replace('\n', ' ');
                }
            }

            //if (value is TLMessageEmpty messageEmpty)
            //{
            //    return string.Empty;
            //}

            //if (value is TLMessageService messageService)
            //{
            //    return string.Empty;
            //}

            if (!showContent)
            {
                return Strings.Resources.Message;
            }

            switch (value.Content)
            {
                case MessageAnimation animation:
                    return animation.Caption.Text.Replace('\n', ' ');
                case MessageAudio audio:
                    return audio.Caption.Text.Replace('\n', ' ');
                case MessageDocument document:
                    return document.Caption.Text.Replace('\n', ' ');
                case MessagePhoto photo:
                    return photo.Caption.Text.Replace('\n', ' ');
                case MessageVideo video:
                    return video.Caption.Text.Replace('\n', ' ');
                case MessageVoiceNote voiceNote:
                    return voiceNote.Caption.Text.Replace('\n', ' ');

                case MessageText text:
                    return text.Text.Text.Replace('\n', ' ');

                case MessageDice dice:
                    return dice.Emoji;
            }

            return string.Empty;
        }

        private string UpdateDraftLabel(Chat chat)
        {
            if (chat.DraftMessage != null)
            {
                switch (chat.DraftMessage.InputMessageText)
                {
                    case InputMessageText text:
                        return string.Format(_expanded ? "{0}\r\n" : "{0}: ", Strings.Resources.Draft);
                }
            }

            return string.Empty;
        }

        private string UpdateFromLabel(Chat chat, ChatPosition position)
        {
            if (position?.Source is ChatSourcePublicServiceAnnouncement psa && !string.IsNullOrEmpty(psa.Text))
            {
                return string.Empty;
            }
            else if (chat.DraftMessage != null)
            {
                switch (chat.DraftMessage.InputMessageText)
                {
                    case InputMessageText text:
                        return string.Empty;
                }
            }

            var message = chat.LastMessage;
            if (message == null)
            {
                return string.Empty;
            }

            return UpdateFromLabel(chat, message);
        }

        private string UpdateFromLabel(Chat chat, Message message)
        {
            if (message.IsService())
            {
                return MessageService.GetText(new ViewModels.MessageViewModel(_protoService, null, null, message));
            }

            var format = _expanded ? "{0}\r\n" : "{0}: ";
            var result = string.Empty;

            if (ShowFrom(chat, message))
            {
                if (message.IsOutgoing)
                {
                    if (!(chat.Type is ChatTypePrivate priv && priv.UserId == message.SenderUserId) && !message.IsChannelPost)
                    {
                        result = string.Format(format, Strings.Resources.FromYou);
                    }
                }
                else
                {
                    var from = _protoService.GetUser(message.SenderUserId);
                    if (from != null)
                    {
                        if (!string.IsNullOrEmpty(from.FirstName))
                        {
                            result = string.Format(format, from.FirstName.Trim());
                        }
                        else if (!string.IsNullOrEmpty(from.LastName))
                        {
                            result = string.Format(format, from.LastName.Trim());
                        }
                        else if (!string.IsNullOrEmpty(from.Username))
                        {
                            result = string.Format(format, from.Username.Trim());
                        }
                        else if (from.Type is UserTypeDeleted)
                        {
                            result = string.Format(format, Strings.Resources.HiddenName);
                        }
                        else
                        {
                            result = string.Format(format, from.Id);
                        }
                    }
                }
            }

            if (message.Content is MessageGame gameMedia)
            {
                return result + "\uD83C\uDFAE " + gameMedia.Game.Title;
            }
            if (message.Content is MessageExpiredVideo)
            {
                return result + Strings.Resources.AttachVideoExpired;
            }
            else if (message.Content is MessageExpiredPhoto)
            {
                return result + Strings.Resources.AttachPhotoExpired;
            }
            else if (message.Content is MessageVideoNote)
            {
                return result + Strings.Resources.AttachRound;
            }
            else if (message.Content is MessageSticker sticker)
            {
                if (string.IsNullOrEmpty(sticker.Sticker.Emoji))
                {
                    return result + Strings.Resources.AttachSticker;
                }

                return result + $"{sticker.Sticker.Emoji} {Strings.Resources.AttachSticker}";
            }

            string GetCaption(string caption)
            {
                return string.IsNullOrEmpty(caption) ? string.Empty : ", ";
            }

            if (message.Content is MessageVoiceNote voiceNote)
            {
                return result + Strings.Resources.AttachAudio + GetCaption(voiceNote.Caption.Text);
            }
            else if (message.Content is MessageVideo video)
            {
                return result + (video.IsSecret ? Strings.Resources.AttachDestructingVideo : Strings.Resources.AttachVideo) + GetCaption(video.Caption.Text);
            }
            else if (message.Content is MessageAnimation animation)
            {
                return result + Strings.Resources.AttachGif + GetCaption(animation.Caption.Text);
            }
            else if (message.Content is MessageAudio audio)
            {
                var performer = string.IsNullOrEmpty(audio.Audio.Performer) ? null : audio.Audio.Performer;
                var title = string.IsNullOrEmpty(audio.Audio.Title) ? null : audio.Audio.Title;

                if (performer == null || title == null)
                {
                    return result + Strings.Resources.AttachMusic + GetCaption(audio.Caption.Text);
                }
                else
                {
                    return $"{result}\uD83C\uDFB5 {performer} - {title}" + GetCaption(audio.Caption.Text);
                }
            }
            else if (message.Content is MessageDocument document)
            {
                if (string.IsNullOrEmpty(document.Document.FileName))
                {
                    return result + Strings.Resources.AttachDocument + GetCaption(document.Caption.Text);
                }

                return result + document.Document.FileName + GetCaption(document.Caption.Text);
            }
            else if (message.Content is MessageInvoice invoice)
            {
                return result + invoice.Title;
            }
            else if (message.Content is MessageContact)
            {
                return result + Strings.Resources.AttachContact;
            }
            else if (message.Content is MessageLocation location)
            {
                return result + (location.LivePeriod > 0 ? Strings.Resources.AttachLiveLocation : Strings.Resources.AttachLocation);
            }
            else if (message.Content is MessageVenue vanue)
            {
                return result + Strings.Resources.AttachLocation;
            }
            else if (message.Content is MessagePhoto photo)
            {
                return result + (photo.IsSecret ? Strings.Resources.AttachDestructingPhoto : Strings.Resources.AttachPhoto) + GetCaption(photo.Caption.Text);
            }
            else if (message.Content is MessagePoll poll)
            {
                return result + "\uD83D\uDCCA " + poll.Poll.Question;
            }
            else if (message.Content is MessageCall call)
            {
                return result + call.ToOutcomeText(message.IsOutgoing);
            }
            else if (message.Content is MessageUnsupported)
            {
                return result + Strings.Resources.UnsupportedAttachment;
            }

            return result;
        }

        private bool ShowFrom(Chat chat, Message message)
        {
            if (message.IsService())
            {
                return false;
            }

            if (message.IsOutgoing)
            {
                return true;
            }

            if (chat.Type is ChatTypeBasicGroup)
            {
                return true;
            }

            if (chat.Type is ChatTypeSupergroup supergroup)
            {
                return !supergroup.IsChannel;
            }

            return false;
        }

        private string UpdateStateIcon(long maxId, Chat chat, DraftMessage draft, Message message, MessageSendingState state)
        {
            if (draft != null || message == null)
            {
                UpdateTicks(null);

                _ticksState = MessageTicksState.None;
                return string.Empty;
            }

            if (message.IsOutgoing /*&& IsOut(ViewModel)*/)
            {
                if (chat.Type is ChatTypePrivate privata && privata.UserId == _protoService.Options.MyId)
                {
                    if (message.SendingState is MessageSendingStateFailed)
                    {
                        // TODO: 
                        return "\uE599"; // Failed
                    }
                    else if (message.SendingState is MessageSendingStatePending)
                    {
                        return "\uE600"; // Pending
                    }

                    UpdateTicks(null);

                    _ticksState = MessageTicksState.None;
                    return string.Empty;
                }

                if (message.SendingState is MessageSendingStateFailed)
                {
                    UpdateTicks(null);

                    _ticksState = MessageTicksState.Failed;

                    // TODO: 
                    return "\uE599"; // Failed
                }
                else if (message.SendingState is MessageSendingStatePending)
                {
                    UpdateTicks(null);

                    _ticksState = MessageTicksState.Pending;
                    return "\uE600"; // Pending
                }
                else if (message.Id <= maxId)
                {
                    UpdateTicks(true, _ticksState == MessageTicksState.Sent);

                    _ticksState = MessageTicksState.Read;
                    return _container != null ? "\uE603" : "\uE601"; // Read
                }

                UpdateTicks(false, _ticksState == MessageTicksState.Pending);

                _ticksState = MessageTicksState.Sent;
                return _container != null ? "\uE603" : "\uE602"; // Unread
            }

            UpdateTicks(null);

            _ticksState = MessageTicksState.None;
            return string.Empty;
        }

        private string UpdateTimeLabel(Chat chat, ChatPosition position)
        {
            if (position?.Source is ChatSourceMtprotoProxy)
            {
                return Strings.Resources.UseProxySponsor;
            }
            else if (position?.Source is ChatSourcePublicServiceAnnouncement psa)
            {
                var type = LocaleService.Current.GetString("PsaType_" + psa.Type);
                if (type.Length > 0)
                {
                    return type;
                }

                return Strings.Resources.PsaTypeDefault;
            }

            var lastMessage = chat.LastMessage;
            if (lastMessage != null)
            {
                return UpdateTimeLabel(lastMessage);
            }

            return string.Empty;
        }

        private string UpdateTimeLabel(Message message)
        {
            return BindConvert.Current.DateExtended(message.Date);
        }

        private string UpdateType(Chat chat)
        {
            if (chat.Type is ChatTypeSupergroup supergroup)
            {
                return supergroup.IsChannel ? Icons.Channel : Icons.Group;
            }
            else if (chat.Type is ChatTypeBasicGroup)
            {
                return Icons.Group;
            }
            else if (chat.Type is ChatTypeSecret)
            {
                return Icons.Secret;
            }
            else if (chat.Type is ChatTypePrivate privata && _protoService != null)
            {
                if (_protoService.IsRepliesChat(chat))
                {
                    return null;
                }

                var user = _protoService.GetUser(privata.UserId);
                if (user != null && user.Type is UserTypeBot)
                {
                    return Icons.Bot;
                }
            }

            return null;
        }

        private void ToolTip_Opened(object sender, RoutedEventArgs e)
        {
            var tooltip = sender as ToolTip;
            if (tooltip != null)
            {
                if (string.IsNullOrEmpty(BriefInfo.Text) || ApiInfo.CanCheckTextTrimming && !BriefInfo.IsTextTrimmed)
                {
                    tooltip.IsOpen = false;
                }
                else
                {
                    tooltip.Content = BriefInfo.Text;
                }
            }
        }

        protected override void OnDragEnter(DragEventArgs e)
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            if (_protoService.CanPostMessages(chat) && e.DataView.AvailableFormats.Count > 0)
            {
                if (DropVisual == null)
                    FindName(nameof(DropVisual));

                DropVisual.Visibility = Visibility.Visible;
                e.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Copy;
            }
            else
            {
                if (DropVisual != null)
                    DropVisual.Visibility = Visibility.Collapsed;

                e.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.None;
            }

            base.OnDragEnter(e);
        }

        protected override void OnDragLeave(DragEventArgs e)
        {
            if (DropVisual != null)
                DropVisual.Visibility = Visibility.Collapsed;

            base.OnDragLeave(e);
        }

        protected override void OnDrop(DragEventArgs e)
        {
            if (DropVisual != null)
                DropVisual.Visibility = Visibility.Collapsed;

            if (e.DataView.AvailableFormats.Count == 0)
            {
                return;
            }

            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            var service = WindowContext.GetForCurrentView().NavigationServices.GetByFrameId($"Main{_protoService.SessionId}") as NavigationService;
            if (service != null)
            {
                App.DataPackages[chat.Id] = e.DataView;
                service.NavigateToChat(chat);
            }

            base.OnDrop(e);
        }

        public void Mockup(ChatType type, int color, string title, string from, string message, bool sent, int unread, bool muted, bool pinned, DateTime date, bool online = false)
        {
            TitleLabel.Text = title;
            Photo.Source = type is ChatTypeSupergroup ? PlaceholderHelper.GetNameForChat(title, 48, color) : PlaceholderHelper.GetNameForUser(title, 48, color);
            //UpdateChatType(chat);
            TypeIcon.Text = type is ChatTypeSupergroup ? Icons.Group : string.Empty;
            TypeIcon.Visibility = type is ChatTypeSupergroup ? Visibility.Visible : Visibility.Collapsed;

            MutedIcon.Visibility = muted ? Visibility.Visible : Visibility.Collapsed;
            VisualStateManager.GoToState(LayoutRoot, muted ? "Muted" : "Unmuted", false);

            VerifiedIcon.Visibility = Visibility.Collapsed;

            PinnedIcon.Visibility = pinned ? Visibility.Visible : Visibility.Collapsed;
            UnreadBadge.Visibility = unread > 0 ? Visibility.Visible : Visibility.Collapsed;
            UnreadLabel.Text = unread.ToString();
            UnreadMentionsBadge.Visibility = Visibility.Collapsed;

            DraftLabel.Text = string.Empty;
            FromLabel.Text = from;
            BriefLabel.Text = message;
            TimeLabel.Text = BindConvert.Current.ShortTime.Format(date);
            StateIcon.Glyph = sent ? "\uE601" : string.Empty;

            if (_container != null)
            {
                _container.IsVisible = false;
            }

            _onlineBadge.Opacity = online ? 1 : 0;
            _onlineBadge.Scale = new Vector3(online ? 1 : 0);
        }


        #region Accent

        public Color Accent
        {
            get { return (Color)GetValue(AccentProperty); }
            set { SetValue(AccentProperty, value); }
        }

        public static readonly DependencyProperty AccentProperty =
            DependencyProperty.Register("Accent", typeof(Color), typeof(ChatCell), new PropertyMetadata(default(Color), OnAccentChanged));

        private static void OnAccentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var sender = d as ChatCell;
            var solid = (Color)e.NewValue;

            if (solid == null || sender._ellipse == null)
            {
                return;
            }

            var brush = Window.Current.Compositor.CreateColorBrush(solid);

            sender._ellipse.FillBrush = brush;
        }

        #endregion

        #region Accent

        public SolidColorBrush SelectionStroke
        {
            get { return (SolidColorBrush)GetValue(SelectionStrokeProperty); }
            set { SetValue(SelectionStrokeProperty, value); }
        }

        public static readonly DependencyProperty SelectionStrokeProperty =
            DependencyProperty.Register("SelectionStroke", typeof(SolidColorBrush), typeof(ChatCell), new PropertyMetadata(default(Color), OnSelectionStrokeChanged));

        private static void OnSelectionStrokeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var sender = d as ChatCell;
            var solid = e.NewValue as SolidColorBrush;

            if (solid == null || sender._stroke == null)
            {
                return;
            }

            var brush = Window.Current.Compositor.CreateColorBrush(solid.Color);

            sender._stroke.FillBrush = brush;
        }

        #endregion

        #region Selection Animation

        private Visual _selectionOutline;
        private Visual _selectionPhoto;

        private CompositionPathGeometry _polygon;
        private CompositionSpriteShape _ellipse;
        private CompositionSpriteShape _stroke;
        private ShapeVisual _visual;

        private void InitializeSelection()
        {
            CompositionPath GetCheckMark()
            {
                CanvasGeometry result;
                using (var builder = new CanvasPathBuilder(null))
                {
                    //builder.BeginFigure(new Vector2(3.821f, 7.819f));
                    //builder.AddLine(new Vector2(6.503f, 10.501f));
                    //builder.AddLine(new Vector2(12.153f, 4.832f));
                    builder.BeginFigure(new Vector2(5.821f, 9.819f));
                    builder.AddLine(new Vector2(7.503f, 12.501f));
                    builder.AddLine(new Vector2(14.153f, 6.832f));
                    builder.EndFigure(CanvasFigureLoop.Open);
                    result = CanvasGeometry.CreatePath(builder);
                }
                return new CompositionPath(result);
            }

            var compositor = Window.Current.Compositor;
            //12.711,5.352 11.648,4.289 6.5,9.438 4.352,7.289 3.289,8.352 6.5,11.563

            if (ApiInfo.CanUseDirectComposition)
            {
                var polygon = compositor.CreatePathGeometry();
                polygon.Path = GetCheckMark();

                var shape1 = compositor.CreateSpriteShape();
                shape1.Geometry = polygon;
                shape1.StrokeThickness = 1.5f;
                shape1.StrokeBrush = compositor.CreateColorBrush(Windows.UI.Colors.White);

                var ellipse = compositor.CreateEllipseGeometry();
                ellipse.Radius = new Vector2(8);
                ellipse.Center = new Vector2(10);

                var shape2 = compositor.CreateSpriteShape();
                shape2.Geometry = ellipse;
                shape2.FillBrush = compositor.CreateColorBrush(Windows.UI.Colors.Black);

                var outer = compositor.CreateEllipseGeometry();
                outer.Radius = new Vector2(10);
                outer.Center = new Vector2(10);

                var shape3 = compositor.CreateSpriteShape();
                shape3.Geometry = outer;
                shape3.FillBrush = compositor.CreateColorBrush(Windows.UI.Colors.White);

                var visual = compositor.CreateShapeVisual();
                visual.Shapes.Add(shape3);
                visual.Shapes.Add(shape2);
                visual.Shapes.Add(shape1);
                visual.Size = new Vector2(20, 20);
                visual.Offset = new Vector3(48 - 19, 48 - 19, 0);
                visual.CenterPoint = new Vector3(8);
                visual.Scale = new Vector3(0);

                ElementCompositionPreview.SetElementChildVisual(PhotoPanel, visual);

                _polygon = polygon;
                _ellipse = shape2;
                _stroke = shape3;
                _visual = visual;
            }

            _selectionPhoto = ElementCompositionPreview.GetElementVisual(Photo);
            _selectionOutline = ElementCompositionPreview.GetElementVisual(SelectionOutline);
            _selectionPhoto.CenterPoint = new Vector3(24);
            _selectionOutline.CenterPoint = new Vector3(24);
            _selectionOutline.Opacity = 0;
        }

        protected override void OnToggle()
        {
            if (_selectionMode == ListViewSelectionMode.Multiple)
            {
                OnSelectionChanged(IsChecked != true, true);
                base.OnToggle();

                if (IsChecked == true)
                {
                    _delegate?.AddSelectedItem(_chat);
                }
                else
                {
                    _delegate?.RemoveSelectedItem(_chat);
                }
            }
        }

        private void OnSelectionChanged(bool selected, bool animate)
        {
            if (animate && selected != (IsChecked == true))
            {
                var compositor = Window.Current.Compositor;

                var anim3 = compositor.CreateScalarKeyFrameAnimation();
                anim3.InsertKeyFrame(selected ? 0 : 1, 0);
                anim3.InsertKeyFrame(selected ? 1 : 0, 1);

                if (_visual != null)
                {
                    var anim1 = compositor.CreateScalarKeyFrameAnimation();
                    anim1.InsertKeyFrame(selected ? 0 : 1, 0);
                    anim1.InsertKeyFrame(selected ? 1 : 0, 1);
                    anim1.DelayTime = TimeSpan.FromMilliseconds(anim1.Duration.TotalMilliseconds / 2);
                    anim1.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;

                    var anim2 = compositor.CreateVector3KeyFrameAnimation();
                    anim2.InsertKeyFrame(selected ? 0 : 1, new Vector3(0));
                    anim2.InsertKeyFrame(selected ? 1 : 0, new Vector3(1));

                    _polygon.StartAnimation("TrimEnd", anim1);
                    _visual.StartAnimation("Scale", anim2);
                    _visual.StartAnimation("Opacity", anim3);
                }


                var anim4 = compositor.CreateVector3KeyFrameAnimation();
                anim4.InsertKeyFrame(selected ? 0 : 1, new Vector3(1));
                anim4.InsertKeyFrame(selected ? 1 : 0, new Vector3(40f / 48f));

                var anim5 = compositor.CreateVector3KeyFrameAnimation();
                anim5.InsertKeyFrame(selected ? 1 : 0, new Vector3(1));
                anim5.InsertKeyFrame(selected ? 0 : 1, new Vector3(40f / 48f));

                _selectionPhoto.StartAnimation("Scale", anim4);
                _selectionOutline.StartAnimation("Scale", anim5);
                _selectionOutline.StartAnimation("Opacity", anim3);
            }
            else
            {
                if (_visual != null)
                {
                    _polygon.TrimEnd = selected ? 1 : 0;
                    _visual.Scale = new Vector3(selected ? 1 : 0);
                    _visual.Opacity = selected ? 1 : 0;
                }

                _selectionPhoto.Scale = new Vector3(selected ? 40f / 48f : 1);
                _selectionOutline.Scale = new Vector3(selected ? 1 : 40f / 48f);
                _selectionOutline.Opacity = selected ? 1 : 0;
            }
        }

        private ListViewSelectionMode _selectionMode;

        public void SetSelectionMode(ListViewSelectionMode mode, bool animate)
        {
            if (mode == ListViewSelectionMode.Multiple && _delegate?.IsItemSelected(_chat) == true)
            {
                OnSelectionChanged(true, animate);
                IsChecked = true;
            }
            else if (mode == ListViewSelectionMode.Single && _delegate?.SelectedItem == _chat.Id)
            {
                OnSelectionChanged(false, _selectionMode == ListViewSelectionMode.Multiple && IsChecked == true);
                IsChecked = null;
            }
            else
            {
                OnSelectionChanged(false, animate);
                IsChecked = false;
            }

            _selectionMode = mode;
        }

        #endregion

        #region Tick Animation

        private CompositionGeometry _line11;
        private CompositionGeometry _line12;
        private ShapeVisual _visual1;

        private CompositionGeometry _line21;
        private CompositionGeometry _line22;
        private ShapeVisual _visual2;

        private CompositionSpriteShape[] _shapes;

        private SpriteVisual _container;

        #region Stroke

        public Brush Stroke
        {
            get { return (Brush)GetValue(StrokeProperty); }
            set { SetValue(StrokeProperty, value); }
        }

        public static readonly DependencyProperty StrokeProperty =
            DependencyProperty.Register("Stroke", typeof(Brush), typeof(ChatCell), new PropertyMetadata(null, OnStrokeChanged));

        private static void OnStrokeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var sender = d as ChatCell;
            var solid = e.NewValue as SolidColorBrush;

            if (solid == null || sender._container == null)
            {
                return;
            }

            var brush = Window.Current.Compositor.CreateColorBrush(solid.Color);

            foreach (var shape in sender._shapes)
            {
                shape.StrokeBrush = brush;
            }
        }

        #endregion

        private void InitializeTicks()
        {
            if (!ApiInfo.CanUseDirectComposition)
            {
                return;
            }

            var width = 18f;
            var height = 10f;
            var stroke = 2f;
            var distance = stroke * 2;

            var sqrt = (float)Math.Sqrt(2);

            var side = (stroke / sqrt) / 2f;
            var diagonal = height * sqrt;
            var length = (diagonal / 2f) / sqrt;

            var join = stroke / 2 * sqrt;

            var line11 = Window.Current.Compositor.CreateLineGeometry();
            var line12 = Window.Current.Compositor.CreateLineGeometry();

            line11.Start = new Vector2(width - height + side + join - length - distance, height - side - length);
            line11.End = new Vector2(width - height + side + join - distance, height - side);

            line12.Start = new Vector2(width - height + side - distance, height - side);
            line12.End = new Vector2(width - side - distance, side);

            var shape11 = Window.Current.Compositor.CreateSpriteShape(line11);
            shape11.StrokeThickness = 2;
            shape11.StrokeBrush = Window.Current.Compositor.CreateColorBrush(Windows.UI.Colors.Black);
            shape11.IsStrokeNonScaling = true;

            var shape12 = Window.Current.Compositor.CreateSpriteShape(line12);
            shape12.StrokeThickness = 2;
            shape12.StrokeBrush = Window.Current.Compositor.CreateColorBrush(Windows.UI.Colors.Black);
            shape12.IsStrokeNonScaling = true;

            var visual1 = Window.Current.Compositor.CreateShapeVisual();
            visual1.Shapes.Add(shape12);
            visual1.Shapes.Add(shape11);
            visual1.Size = new Vector2(width, height);
            visual1.CenterPoint = new Vector3(width, height / 2f, 0);


            var line21 = Window.Current.Compositor.CreateLineGeometry();
            var line22 = Window.Current.Compositor.CreateLineGeometry();

            line21.Start = new Vector2(width - height + side + join - length, height - side - length);
            line21.End = new Vector2(width - height + side + join, height - side);

            line22.Start = new Vector2(width - height + side, height - side);
            line22.End = new Vector2(width - side, side);

            var shape21 = Window.Current.Compositor.CreateSpriteShape(line21);
            shape21.StrokeThickness = 2;
            shape21.StrokeBrush = Window.Current.Compositor.CreateColorBrush(Windows.UI.Colors.Black);

            var shape22 = Window.Current.Compositor.CreateSpriteShape(line22);
            shape22.StrokeThickness = 2;
            shape22.StrokeBrush = Window.Current.Compositor.CreateColorBrush(Windows.UI.Colors.Black);

            var visual2 = Window.Current.Compositor.CreateShapeVisual();
            visual2.Shapes.Add(shape22);
            visual2.Shapes.Add(shape21);
            visual2.Size = new Vector2(width, height);


            var container = Window.Current.Compositor.CreateSpriteVisual();
            container.Children.InsertAtTop(visual2);
            container.Children.InsertAtTop(visual1);
            container.Size = new Vector2(width, height);

            ElementCompositionPreview.SetElementChildVisual(StateIcon, container);

            _line11 = line11;
            _line12 = line12;
            _line21 = line21;
            _line22 = line22;
            _shapes = new[] { shape11, shape12, shape21, shape22 };
            _visual1 = visual1;
            _visual2 = visual2;
            _container = container;
        }

        private void UpdateTicks(bool? read, bool animate = false)
        {
            if (_container == null)
            {
                return;
            }

            if (read == null)
            {
                _container.IsVisible = false;
            }
            else if (animate)
            {
                AnimateTicks(read == true);
            }
            else
            {
                _line11.TrimEnd = read == true ? 1 : 0;
                _line12.TrimEnd = read == true ? 1 : 0;

                _line21.TrimStart = read == true ? 1 : 0;

                _container.IsVisible = true;
            }
        }

        private void AnimateTicks(bool read)
        {
            _container.IsVisible = true;

            var height = 10f;
            var stroke = 2f;

            var sqrt = (float)Math.Sqrt(2);

            var diagonal = height * sqrt;
            var length = (diagonal / 2f) / sqrt;

            var duration = 250;
            var percent = stroke / length;

            var linear = Window.Current.Compositor.CreateLinearEasingFunction();

            var anim11 = Window.Current.Compositor.CreateScalarKeyFrameAnimation();
            anim11.InsertKeyFrame(0, 0);
            anim11.InsertKeyFrame(1, 1, linear);
            anim11.Duration = TimeSpan.FromMilliseconds(duration - (percent * duration));

            var anim12 = Window.Current.Compositor.CreateScalarKeyFrameAnimation();
            anim12.InsertKeyFrame(0, 0);
            anim12.InsertKeyFrame(1, 1);
            anim12.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;
            anim12.DelayTime = anim11.Duration;
            anim12.Duration = TimeSpan.FromMilliseconds(400);

            var anim22 = Window.Current.Compositor.CreateVector3KeyFrameAnimation();
            anim22.InsertKeyFrame(0, new Vector3(1));
            anim22.InsertKeyFrame(0.2f, new Vector3(1.1f));
            anim22.InsertKeyFrame(1, new Vector3(1));
            anim22.Duration = anim11.Duration + anim12.Duration;

            if (read)
            {
                _line11.StartAnimation("TrimEnd", anim11);
                _line12.StartAnimation("TrimEnd", anim12);
                _visual1.StartAnimation("Scale", anim22);

                var anim21 = Window.Current.Compositor.CreateScalarKeyFrameAnimation();
                anim21.InsertKeyFrame(0, 0);
                anim21.InsertKeyFrame(1, 1, linear);
                anim11.Duration = TimeSpan.FromMilliseconds(duration);

                _line21.StartAnimation("TrimStart", anim21);
            }
            else
            {
                _line11.TrimEnd = 0;
                _line12.TrimEnd = 0;

                _line21.TrimStart = 0;

                _line21.StartAnimation("TrimEnd", anim11);
                _line22.StartAnimation("TrimEnd", anim12);
                _visual2.StartAnimation("Scale", anim22);
            }
        }

        #endregion

        private void OnClick(object sender, RoutedEventArgs e)
        {
            if (_selectionMode != ListViewSelectionMode.Multiple)
            {
                _delegate?.SetSelectedItem(_chat);
            }
            else if (_delegate.SelectedCount < 1)
            {
                _delegate?.SetSelectionMode(false);
            }
        }
    }

    public class ChatCellAutomationPeer : ToggleButtonAutomationPeer
    {
        private ChatCell _owner;

        public ChatCellAutomationPeer(ChatCell owner)
            : base(owner)
        {
            _owner = owner;
        }

        protected override string GetNameCore()
        {
            return _owner.GetAutomationName();
        }

        protected override AutomationControlType GetAutomationControlTypeCore()
        {
            return AutomationControlType.ListItem;
        }
    }
}
