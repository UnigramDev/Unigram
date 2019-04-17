using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using Telegram.Td.Api;
using Template10.Common;
using Template10.Services.NavigationService;
using Unigram.Common;
using Unigram.Common.Chats;
using Unigram.Controls.Messages;
using Unigram.Converters;
using Unigram.Services;
using Unigram.ViewModels;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation;
using Windows.UI.Xaml.Automation.Peers;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace Unigram.Controls.Cells
{
    public sealed partial class ChatCell : UserControl
    {
        private Chat _chat;
        private IProtoService _protoService;

        private Visual _onlineBadge;

        public ChatCell()
        {
            InitializeComponent();

            _onlineBadge = ElementCompositionPreview.GetElementVisual(OnlineBadge);
            _onlineBadge.CenterPoint = new System.Numerics.Vector3(6.5f);
            _onlineBadge.Opacity = 0;
            _onlineBadge.Scale = new System.Numerics.Vector3(0);
        }

        public void UpdateChat(IProtoService protoService, Chat chat)
        {
            _protoService = protoService;
            Update(chat);
        }

        public void UpdateMessage(IProtoService protoService, Message message)
        {
            _protoService = protoService;

            var chat = protoService.GetChat(message.ChatId);
            if (chat == null)
            {
                return;
            }

            UpdateChatTitle(chat);
            UpdateChatPhoto(chat);
            UpdateChatType(chat);
            UpdateNotificationSettings(chat);

            PinnedIcon.Visibility = Visibility.Collapsed;
            UnreadBadge.Visibility = Visibility.Collapsed;
            UnreadMentionsBadge.Visibility = Visibility.Collapsed;

            DraftLabel.Text = string.Empty;
            FromLabel.Text = UpdateFromLabel(chat, message);
            BriefLabel.Text = UpdateBriefLabel(chat, message, true, false);
            TimeLabel.Text = UpdateTimeLabel(message);
            StateIcon.Glyph = UpdateStateIcon(chat.LastReadOutboxMessageId, chat, null, message, message.SendingState);
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
                        builder.Append(". ");
                    }
                    if (user.Id == protoService.Options.MyId)
                    {
                        builder.Append(Strings.Resources.SavedMessages);
                    }
                    else
                    {
                        builder.Append(user.GetFullName());
                    }

                    builder.Append(". ");
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

                builder.Append(". ");
                builder.Append(protoService.GetTitle(chat));
                builder.Append(". ");
            }

            if (chat.UnreadCount > 0)
            {
                builder.Append(Locale.Declension("NewMessages", chat.UnreadCount));
                builder.Append(". ");
            }

            if (message == null)
            {
                //AutomationProperties.SetName(this, builder.ToString());
                return builder.ToString();
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

            builder.Append(". ");

            if (!message.IsOutgoing && message.SenderUserId != 0 && !message.IsService())
            {
                var fromUser = protoService.GetUser(message.SenderUserId);
                if (fromUser != null)
                {
                    builder.Append(fromUser.GetFullName());
                    builder.Append(". ");
                }
            }

            if (chat.Type is ChatTypeSecret == false)
            {
                builder.Append(Automation.GetSummary(protoService, message));
            }

            //AutomationProperties.SetName(this, builder.ToString());
            return builder.ToString();
        }

        #region Updates

        public void UpdateChatLastMessage(Chat chat)
        {
            DraftLabel.Text = UpdateDraftLabel(chat);
            FromLabel.Text = UpdateFromLabel(chat);
            BriefLabel.Text = UpdateBriefLabel(chat);
            TimeLabel.Text = UpdateTimeLabel(chat);
            StateIcon.Glyph = UpdateStateIcon(chat.LastReadOutboxMessageId, chat, chat.DraftMessage, chat.LastMessage, chat.LastMessage?.SendingState);
        }

        public void UpdateChatReadInbox(Chat chat)
        {
            PinnedIcon.Visibility = (chat.UnreadCount == 0 && !chat.IsMarkedAsUnread) && chat.IsPinned ? Visibility.Visible : Visibility.Collapsed;
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

        public void UpdateChatUnreadMentionCount(Chat chat)
        {
            UpdateChatReadInbox(chat);
            UnreadMentionsBadge.Visibility = chat.UnreadMentionCount > 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        public void UpdateNotificationSettings(Chat chat)
        {
            var muted = _protoService.GetNotificationSettingsMuteFor(chat) > 0;
            UnreadBackground.Visibility = muted ? Visibility.Collapsed : Visibility.Visible;
            UnreadMutedBackground.Visibility = muted ? Visibility.Visible : Visibility.Collapsed;
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
                Photo.Source = PlaceholderHelper.GetSavedMessages(privata.UserId, 48);
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
            }
            else
            {
                TypingLabel.Visibility = Visibility.Collapsed;
                BriefInfo.Visibility = Visibility.Visible;
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

        private void Update(Chat chat)
        {
            _chat = chat;
            Tag = chat;

            //UpdateViewState(chat, ChatFilterMode.None, false, false);

            UpdateChatTitle(chat);
            UpdateChatPhoto(chat);
            UpdateChatType(chat);

            UpdateChatLastMessage(chat);
            //UpdateChatReadInbox(chat);
            UpdateChatUnreadMentionCount(chat);
            UpdateNotificationSettings(chat);
            UpdateChatActions(chat, _protoService.GetChatActions(chat.Id));

            var user = _protoService.GetUser(chat);
            if (user != null && user.Type is UserTypeRegular && user.Id != _protoService.Options.MyId)
            {
                UpdateUserStatus(chat, user.Status);
            }
            else
            {
                OnlineBadge.Visibility = Visibility.Collapsed;
            }
        }

        #endregion

        public void UpdateViewState(Chat chat, bool selected, bool compact)
        {
            VisualStateManager.GoToState(this, selected ? "Selected" : chat.Type is ChatTypeSecret ? "Secret" : "Normal", false);
            VisualStateManager.GoToState(this, compact ? "Compact" : "Expanded", false);
        }

        private Visibility UpdateIsPinned(bool isPinned, int unreadCount)
        {
            return isPinned && unreadCount == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private string UpdateBriefLabel(Chat chat)
        {
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
                        return $"{Strings.Resources.Draft}: ";
                }
            }

            return string.Empty;
        }

        private string UpdateFromLabel(Chat chat)
        {
            if (chat.DraftMessage != null)
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

            var result = string.Empty;

            if (ShowFrom(chat, message))
            {
                if (message.IsOutgoing)
                {
                    if (!(chat.Type is ChatTypePrivate priv && priv.UserId == message.SenderUserId) && !message.IsChannelPost)
                    {
                        result = $"{Strings.Resources.FromYou}: ";
                    }
                }
                else
                {
                    var from = _protoService.GetUser(message.SenderUserId);
                    if (from != null)
                    {
                        if (!string.IsNullOrEmpty(from.FirstName))
                        {
                            result = $"{from.FirstName.Trim()}: ";
                        }
                        else if (!string.IsNullOrEmpty(from.LastName))
                        {
                            result = $"{from.LastName.Trim()}: ";
                        }
                        else if (!string.IsNullOrEmpty(from.Username))
                        {
                            result = $"{from.Username.Trim()}: ";
                        }
                        else if (from.Type is UserTypeDeleted)
                        {
                            result = $"{Strings.Resources.HiddenName}: ";
                        }
                        else
                        {
                            result = $"{from.Id}: ";
                        }
                    }
                }
            }

            if (message.SendingState is MessageSendingStateFailed && message.IsOutgoing)
            {
                result = "Failed: ";
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

                if (performer == null && title == null)
                {
                    return result + Strings.Resources.AttachMusic + GetCaption(audio.Caption.Text);
                }
                else
                {
                    return $"{result}{performer ?? Strings.Resources.AudioUnknownArtist} - {title ?? Strings.Resources.AudioUnknownTitle}" + GetCaption(audio.Caption.Text);
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
                var outgoing = message.IsOutgoing;
                var missed = call.DiscardReason is CallDiscardReasonMissed || call.DiscardReason is CallDiscardReasonDeclined;

                return result + (missed ? (outgoing ? Strings.Resources.CallMessageOutgoingMissed : Strings.Resources.CallMessageIncomingMissed) : (outgoing ? Strings.Resources.CallMessageOutgoing : Strings.Resources.CallMessageIncoming));
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

                    return string.Empty;
                }

                if (message.SendingState is MessageSendingStateFailed)
                {
                    // TODO: 
                    return "\uE599"; // Failed
                }
                else if (message.SendingState is MessageSendingStatePending)
                {
                    return "\uE600"; // Pending
                }
                else if (message.Id <= maxId)
                {
                    return "\uE601"; // Read
                }

                return "\uE602"; // Unread
            }

            return string.Empty;
        }

        private string UpdateTimeLabel(Chat chat)
        {
            if (_protoService != null && _protoService.IsChatSponsored(chat))
            {
                return Strings.Resources.UseProxySponsor;
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
    }

    public class ChatCellAutomationPeer : FrameworkElementAutomationPeer
    {
        private ChatCell _owner;
        private string _name;

        public ChatCellAutomationPeer(ChatCell owner)
            : base(owner)
        {
            _owner = owner;
        }

        public void SetName(string name)
        {
            SetName(name, _name);
        }

        private void SetName(string newValue, string oldValue)
        {
            _name = newValue;
            RaisePropertyChangedEvent(AutomationElementIdentifiers.NameProperty, oldValue ?? string.Empty, newValue);
        }

        protected override string GetNameCore()
        {
            return "ciao";
        }
    }
}
