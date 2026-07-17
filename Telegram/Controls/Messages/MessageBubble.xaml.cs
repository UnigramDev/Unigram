//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using Telegram.Common;
using Telegram.Controls.Chats;
using Telegram.Controls.Media;
using Telegram.Controls.Messages.Content;
using Telegram.Controls.Stories;
using Telegram.Converters;
using Telegram.Native;
using Telegram.Native.Composition;
using Telegram.Navigation;
using Telegram.Services;
using Telegram.Td;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Telegram.ViewModels.Delegates;
using Telegram.ViewModels.Stories;
using Telegram.Views.Popups;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Core.Direct;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Markup;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Shapes;

namespace Telegram.Controls.Messages
{
    public partial class MessageBubbleHighlightOptions
    {
        public MessageBubbleHighlightOptions(long messageId, TextQuote quote, int checklistTaskId, string pollOptionId, bool moveFocus = true, bool highlight = true)
        {
            MessageId = messageId;
            Quote = quote;
            ChecklistTaskId = checklistTaskId;
            PollOptionId = pollOptionId;
            MoveFocus = moveFocus;
            Highlight = highlight;
        }

        public MessageBubbleHighlightOptions(bool moveFocus = true, bool highlight = true)
        {
            MoveFocus = moveFocus;
            Highlight = highlight;
        }

        public long MessageId { get; }

        public int ChecklistTaskId { get; } = 0;

        public string PollOptionId { get; } = "";

        public TextQuote Quote { get; }

        public bool MoveFocus { get; } = true;

        public bool Highlight { get; } = true;
    }

    public sealed partial class MessageBubble : Control, IReactionsDelegate
    {
        private MessageViewModel _message;

        private string _query;
        private long? _photoId;

        private bool _ignoreSizeChanged = true;

        private bool _hasReplyMarkup;

        private LayerVisual _layerVisual;
        private bool _corners;
        private float _topLeft;
        private float _topRight;
        private float _bottomRight;
        private float _bottomLeft;

        public MessageBubble()
        {
            DefaultStyleKey = typeof(MessageBubble);
        }

        public bool HasFloatingElements
        {
            get
            {
                if (_message?.ReplyMarkup is ReplyMarkupInlineKeyboard)
                {
                    return true;
                }

                var content = _message?.GeneratedContent ?? _message?.Content;
                if (content is MessageSticker or MessageAnimatedEmoji or MessageDice or MessageStakeDice or MessageVideoNote or MessageBigEmoji)
                {
                    return true;
                }
                else if (IsFullMedia(content))
                {
                    return _message.InteractionInfo?.Reactions?.Reactions.Count > 0;
                }

                return false;
            }
        }

        public void UpdateQuery(string text, bool invalidate = true)
        {
            _query = text;

            if (invalidate)
            {
                Message?.SetQuery(text);
            }
        }

        private ThemeShadow _shadow;

        public bool NeedShadow => (ContentPanel?.Shadow ?? _shadow) == null;

        public void UpdateShadow(ThemeShadow shadow)
        {
            if (ShadowCaster != null)
            {
                ShadowCaster.Shadow = shadow;

                var radius = _topLeft == 0 && _topRight == 0 && _bottomRight == 0 && _bottomLeft == 0;
                if (radius)
                {
                    ShadowCaster.Translation = Vector3.Zero;
                }
                else
                {
                    ShadowCaster.Translation = new Vector3(0, 0, Constants.BubbleElevation);
                }
            }
            else
            {
                _shadow = shadow;
            }
        }

        private FormattedTextBlockRecyclePool _recyclePool;

        public void UpdateRecyclePool(FormattedTextBlockRecyclePool recyclePool)
        {
            if (Message != null)
            {
                Message.RecyclePool = recyclePool;
            }
            else
            {
                _recyclePool = recyclePool;
            }
        }

        #region InitializeComponent

        private ColumnDefinition PhotoColumn;

        private Rectangle ShadowCaster;
        private Grid ContentPanel;
        private Grid Header;
        private MessageBubblePanel Panel;
        private MessageTextBlock Message;
        private Border Media;
        private MessageFooter Footer;

        // Lazy loaded
        private ProfilePicture Photo;
        private HyperlinkButton PhotoRoot;

        private Border BackgroundPanel;
        private Border CrossPanel;

        private Grid HeaderPanel;
        private TextBlock HeaderLabel;
        private Hyperlink HeaderLink;
        private Run HeaderLinkRun;
        private BadgeControl MemberTag;
        private TextBlock BoostCount;
        private MessageForwardHeader ForwardHeader;
        private IdentityIcon Identity;
        private GlyphButton PsaInfo;

        private MessageReply Reply;

        private HyperlinkButton Thread;
        private RecentUserHeads RecentRepliers;
        private TextBlock ThreadGlyph;
        private TextBlock ThreadLabel;

        private ReactionsPanel Reactions;

        private ReactionsPanel MediaReactions;
        private ReplyMarkupInlinePanel Markup;

        private Border Action;
        private GlyphButton ActionButton;

        private Border Summary;
        private GlyphButton SummaryButton;

        private bool _templateApplied;

        protected override void OnApplyTemplate()
        {
            PhotoColumn = GetTemplateChild(nameof(PhotoColumn)) as ColumnDefinition;
            ShadowCaster = GetTemplateChild(nameof(ShadowCaster)) as Rectangle;
            ContentPanel = GetTemplateChild(nameof(ContentPanel)) as Grid;
            Header = GetTemplateChild(nameof(Header)) as Grid;
            Panel = GetTemplateChild(nameof(Panel)) as MessageBubblePanel;
            Message = GetTemplateChild(nameof(Message)) as MessageTextBlock;
            Media = GetTemplateChild(nameof(Media)) as Border;
            Footer = GetTemplateChild(nameof(Footer)) as MessageFooter;

            //ContentPanel.CanDrag = true;
            //ContentPanel.DragStarting += OnDragStarting;
            ContentPanel.SizeChanged += OnSizeChanged;
            Message.TextEntityClick += Message_TextEntityClick;

            _layerVisual = CompositionDevice.GetElementLayerVisual(ContentPanel);

            // Forces ParentForTransform with LayerVisual
            ElementComposition.GetElementVisual(Media);

            if (_shadow != null)
            {
                ShadowCaster.Shadow = _shadow;
                _shadow = null;
            }

            if (_recyclePool != null)
            {
                Message.RecyclePool = _recyclePool;
                _recyclePool = null;
            }

            ElementCompositionPreview.SetIsTranslationEnabled(Header, true);
            ElementCompositionPreview.SetIsTranslationEnabled(Message, true);
            ElementCompositionPreview.SetIsTranslationEnabled(Media, true);

            _templateApplied = true;
            TemplateApplied?.Invoke(this, EventArgs.Empty);

            if (_message != null)
            {
                UpdateMessage(_message);
            }
        }

        public event EventHandler TemplateApplied;

        #endregion

        public UIElement MediaTemplateRoot
        {
            get => Media.Child;
            set => Media.Child = value;
        }

        public void Recycle()
        {
            Message.Clear();
            Footer.UpdateMessage(null);
            //Media.Child = null;

            // TODO: Setting Media.Child to null is quite expensive
            // but not doing that causes quite a lot of crashes, because
            // MessageViewModel.Delegate reference will be lost while Media.Child
            // is still alive throwing a lot of NullReferenceExceptions and it's not
            // completely clear about how many of them are actually crashy
            // and which ones are actually caught.

            if (Media.Child is IContent content)
            {
                content.Recycle();
            }

            //UnloadObject(ref Reactions);
            //UnloadObject(ref MediaReactions);

            UnregisterEvents();
        }

        public void UpdateMessage(MessageViewModel message)
        {
            if (Message != null && (_message?.Id != message?.Id || _message?.ChatId != message?.ChatId))
            {
                Message.IgnoreSpoilers = false;
            }

            _message = message;

            if (!_templateApplied)
            {
                return;
            }

            if (message != null)
            {
                Footer.UpdateMessage(message);

                UpdateMessageHeader(message);
                UpdateMessageReply(message);
                UpdateMessageContent(message);
                UpdateMessageInteractionInfo(message);

                UpdateMessageReplyMarkup(message);

                UpdateAttach(message);
            }

            if (_highlight != null)
            {
                _highlight.StopAnimation("Opacity");
                _highlight.Opacity = 0;
            }
        }

        public string GetAutomationName()
        {
            if (_message is not MessageViewModel message)
            {
                return null;
            }

            var chat = message.Chat;

            var title = string.Empty;
            var senderBot = false;

            if (message.IsSaved)
            {
                title = message.ClientService.GetTitle(message.ForwardInfo?.Origin, message.ImportInfo);
            }
            else if (chat.Type is ChatTypeBasicGroup || chat.Type is ChatTypeSupergroup supergroup && !supergroup.IsChannel)
            {
                if (message.IsOutgoing)
                {
                    title = null;
                }
                else if (message.ClientService.TryGetUser(message.SenderId, out User senderUser))
                {
                    senderBot = senderUser.Type is UserTypeBot;
                    title = senderUser.FullName();
                }
                else if (message.ClientService.TryGetChat(message.SenderId, out Chat senderChat))
                {
                    title = message.ClientService.GetTitle(senderChat);
                }
            }

            var builder = new StringBuilder();
            if (title?.Length > 0)
            {
                builder.AppendLine(title);

                var viaBot = message.ClientService.GetUser(message.ViaBotUserId);
                if (viaBot != null && viaBot.HasActiveUsername(out string viaBotUsername))
                {
                    builder.Append($" {Strings.ViaBot} @{viaBotUsername}");
                }

                var admin = message.Delegate?.GetMemberTag(message, out _);
                if (admin?.Length > 0)
                {
                    builder.AppendLine($", {admin}. ");
                }
                else
                {
                    builder.Append(". ");
                }
            }

            if (chat.Id != message.ClientService.Options.MyId)
            {
                if (message.SendingState is MessageSendingStateFailed)
                {
                }
                else if (message.SendingState is MessageSendingStatePending)
                {
                }
                else if (message.Id <= message.LastReadOutboxMessageId && message.IsOutgoing && !message.IsChannelPost)
                {
                }
                else if (message.IsOutgoing && !message.IsChannelPost)
                {
                    builder.Append(Strings.AccDescrMsgUnread);
                    builder.Append(". ");
                }
            }

            if (message.ReplyToItem is MessageViewModel replyToMessage)
            {
                if (message.ClientService.TryGetUser(replyToMessage.SenderId, out User replyUser))
                {
                    builder.AppendLine($"{Strings.AccDescrReplying} {replyUser.FullName()}. ");
                }
                else if (message.ClientService.TryGetChat(replyToMessage.SenderId, out Chat replyChat))
                {
                    builder.AppendLine($"{Strings.AccDescrReplying} {message.ClientService.GetTitle(replyChat)}. ");
                }
            }
            else if (message.ReplyToItem is Story replyToStory)
            {
                if (message.ClientService.TryGetUser(replyToStory.PosterId, out User replyUser))
                {
                    builder.AppendLine($"{Strings.AccDescrReplying} {replyUser.FullName()}. ");
                }
                else if (message.ClientService.TryGetChat(replyToStory.PosterId, out Chat replyChat))
                {
                    builder.AppendLine($"{Strings.AccDescrReplying} {message.ClientService.GetTitle(replyChat)}. ");
                }
            }

            if (message.ForwardInfo != null)
            {
                if (message.ForwardInfo?.Origin is MessageOriginUser fromUser)
                {
                    title = message.ClientService.GetUser(fromUser.SenderUserId)?.FullName();
                    builder.AppendLine($"{Strings.AccDescrForwarding} {title}. ");
                }
                if (message.ForwardInfo?.Origin is MessageOriginChat fromChat)
                {
                    title = message.ClientService.GetTitle(message.ClientService.GetChat(fromChat.SenderChatId));
                    builder.AppendLine($"{Strings.AccDescrForwarding} {title}. ");
                }
                else if (message.ForwardInfo?.Origin is MessageOriginChannel fromChannel)
                {
                    title = message.ClientService.GetTitle(message.ClientService.GetChat(fromChannel.ChatId));
                    builder.AppendLine($"{Strings.AccDescrForwarding} {title}. ");
                }
                else if (message.ForwardInfo?.Origin is MessageOriginHiddenUser hiddenUser)
                {
                    title = hiddenUser.SenderName;
                    builder.AppendLine($"{Strings.AccDescrForwarding} {title}. ");
                }
            }

            builder.Append(Automation.GetSummary(message, true));

            if (message.AuthorSignature.Length > 0)
            {
                builder.Append($"{message.AuthorSignature}, ");
            }

            if (message.EditDate != 0 && message.ViaBotUserId == 0 && !senderBot && message.ReplyMarkup is not ReplyMarkupInlineKeyboard)
            {
                builder.Append($"{Strings.EditedMessage}, ");
            }

            if (message.SendingState is MessageSendingStatePending)
            {
                builder.Append(Strings.AccDescrMsgSending);
            }
            else
            {
                if (message.SchedulingState is MessageSchedulingStateSendAtDate sendAtDate)
                {
                    builder.Append(string.Format(Strings.MessageScheduledOn, Formatter.Time(sendAtDate.SendDate)));
                }
                else if (message.SchedulingState is MessageSchedulingStateSendWhenVideoProcessed sendWhenVideoProcessed)
                {
                    builder.Append(string.Format(Strings.MessageScheduledOn, string.Format(Strings.ScheduledTimeApprox, Formatter.Time(sendWhenVideoProcessed.SendDate))));
                }
                else if (message.SchedulingState is MessageSchedulingStateSendWhenOnline)
                {
                    builder.Append(Strings.MessageScheduledUntilOnline);
                }
                else
                {
                    var date = string.Format(Strings.TodayAtFormatted, Formatter.Time(message.Date));
                    if (message.IsOutgoing)
                    {
                        builder.Append(string.Format(Strings.AccDescrSentDate, date));
                    }
                    else
                    {
                        builder.Append(string.Format(Strings.AccDescrReceivedDate, date));
                    }
                }
            }

            if (message.SendingState is MessageSendingStateFailed)
            {
            }
            else if (message.SendingState is MessageSendingStatePending)
            {
            }
            else if (message.Id <= message.LastReadOutboxMessageId && message.IsOutgoing && !message.IsChannelPost)
            {
                builder.Append(". ");
                builder.Append(Strings.AccDescrMsgRead);
            }

            if (message.InteractionInfo?.ViewCount > 0)
            {
                builder.Append(". ");
                builder.Append(Locale.Declension(Strings.R.AccDescrNumberOfViews, message.InteractionInfo.ViewCount));
            }

            // TODO: this is a bit brutal, but we don't have corresponding reaction emoji in the data
            // so for now this is the best way to do this:
            static void AppendReactions(StringBuilder builder, ReactionsPanel panel)
            {
                foreach (ReactionButton button in panel.Children)
                {
                    builder.Append(". ");
                    builder.Append(button.GetAutomationName());
                }
            }

            if (Reactions != null && Reactions.Children.Count > 0)
            {
                AppendReactions(builder, Reactions);
            }
            else if (MediaReactions != null && MediaReactions.Children.Count > 0)
            {
                AppendReactions(builder, MediaReactions);
            }

            return builder.ToString();
        }

        public void UpdateAttach(MessageViewModel message)
        {
            var chat = message?.Chat;
            if (chat == null || !_templateApplied)
            {
                return;
            }

            //var topLeft = 15d;
            //var topRight = 15d;
            //var bottomRight = 15d;
            //var bottomLeft = 15d;
            var radius = SettingsService.Current.Appearance.BubbleRadius;
            var small = radius < 4 ? radius : 4;

            var topLeft = radius;
            var topRight = radius;
            var bottomRight = radius;
            var bottomLeft = radius;

            var bottomOutgoing = false;
            var bottomIncoming = false;

            var isFirst = message.Delegate?.IsSavedMessagesTab ?? false ? message.IsLast : message.IsFirst;
            var isLast = message.Delegate?.IsSavedMessagesTab ?? false ? message.IsFirst : message.IsLast;

            if (message.IsVisuallyOutgoing)
            {
                if (isFirst && isLast)
                {
                    bottomOutgoing = true;
                }
                else if (isFirst)
                {
                    bottomRight = small;
                }
                else if (isLast)
                {
                    topRight = small;
                    bottomOutgoing = true;
                }
                else
                {
                    topRight = small;
                    bottomRight = small;
                }
            }
            else
            {
                if (isFirst && isLast)
                {
                    bottomIncoming = true;
                }
                else if (isFirst)
                {
                    bottomLeft = small;
                }
                else if (isLast)
                {
                    topLeft = small;
                    bottomIncoming = true;
                }
                else
                {
                    topLeft = small;
                    bottomLeft = small;
                }
            }

            var content = message.GeneratedContent ?? message.Content;
            if (message.ReplyMarkup is ReplyMarkupInlineKeyboard)
            {
                if (content is MessageSticker or MessageAnimatedEmoji or MessageDice or MessageStakeDice or MessageVideoNote or MessageBigEmoji)
                {
                    _hasReplyMarkup = false;
                    SetCorners(0, 0, 0, 0);
                }
                else
                {
                    _hasReplyMarkup = true;
                    SetCorners(topLeft, topRight, small, small);
                }

                Markup?.CornerRadius = new Vector2(bottomRight, bottomLeft);
            }
            else
            {
                if (content is MessageSticker or MessageAnimatedEmoji or MessageDice or MessageStakeDice or MessageVideoNote or MessageBigEmoji)
                {
                    _hasReplyMarkup = false;
                    SetCorners(0, 0, 0, 0);
                }
                else
                {
                    _hasReplyMarkup = false;
                    SetCorners(topLeft, topRight, bottomOutgoing ? 0 : bottomRight, bottomIncoming ? 0 : bottomLeft);
                }
            }

            if (message.Delegate != null && message.Delegate.IsDialog)
            {
                var top = isFirst ? 8 : 2;
                var action = message.IsSaved || message.CanBeShared;

                if (message.IsSaved || (chat != null && (chat.Type is ChatTypeBasicGroup || chat.Type is ChatTypeSupergroup)) && !message.IsChannelPost)
                {
                    if (message.IsOutgoing && !message.IsSaved)
                    {
                        if (message.Content is MessageSticker or MessageAnimatedEmoji or MessageVideoNote)
                        {
                            Margin = new Thickness(12, top, 12, 0);
                        }
                        else
                        {
                            Margin = new Thickness(50, top, 12, 0);
                        }
                    }
                    else
                    {
                        if (message.Content is MessageSticker or MessageAnimatedEmoji or MessageVideoNote)
                        {
                            Margin = new Thickness(12, top, 12, 0);
                        }
                        else
                        {
                            Margin = new Thickness(12, top, action ? 14 : 50, 0);
                        }
                    }
                }
                else
                {
                    if (message.Content is MessageSticker or MessageAnimatedEmoji or MessageVideoNote)
                    {
                        Margin = new Thickness(12, top, 12, 0);
                    }
                    else
                    {
                        if (message.IsVisuallyOutgoing)
                        {
                            Margin = new Thickness(50, top, 12, 0);
                        }
                        else
                        {
                            Margin = new Thickness(12, top, action ? 14 : 50, 0);
                        }
                    }
                }

                UpdatePhoto(message);
            }
        }

        private void SetCorners(float topLeft, float topRight, float bottomRight, float bottomLeft)
        {
            if (_topLeft == topLeft && _topRight == topRight && _bottomRight == bottomRight && _bottomLeft == bottomLeft)
            {
                return;
            }

            _topLeft = topLeft;
            _topRight = topRight;
            _bottomRight = bottomRight;
            _bottomLeft = bottomLeft;

            var radius = topLeft == 0 && topRight == 0 && bottomRight == 0 && bottomLeft == 0;
            if (radius)
            {
                ShadowCaster.Translation = Vector3.Zero;
            }
            else
            {
                ShadowCaster.Translation = new Vector3(0, 0, Constants.BubbleElevation);
            }

            radius |= bottomLeft != 0 && bottomRight != 0;
            radius |= _layerVisual == null;

            if (radius)
            {
                if (_layerVisual != null)
                {
                    _layerVisual.Effect = null;
                }
                else
                {
                    if (bottomRight == 0 && topRight != 0)
                    {
                        bottomRight = 15;
                    }

                    if (bottomLeft == 0 && topLeft != 0)
                    {
                        bottomLeft = 15;
                    }
                }

                _corners = true;
                ContentPanel.CornerRadius = new CornerRadius(topLeft, topRight, bottomRight, bottomLeft);
            }
            else
            {
                _layerVisual.Effect = PlaceholderHelper.Foreground.GetTail(topLeft, topRight, bottomRight, bottomLeft); //PlaceholderHelper.Foreground.GetTail3(XamlRoot, topLeft, topRight, bottomRight, bottomLeft);

                if (_corners)
                {
                    _corners = false;
                    ContentPanel.CornerRadius = new CornerRadius();
                }
            }
        }

        private bool _summaryCollapsed = false;

        public void ShowHideSummary(bool show)
        {
            if (Summary != null)
            {
                if (_summaryCollapsed != show)
                {
                    return;
                }

                _summaryCollapsed = !show;
                Summary.Opacity = show ? 1 : 0;
            }
        }

        private bool _photoCollapsed = false;

        public void ShowHidePhoto(bool show)
        {
            if (PhotoRoot != null)
            {
                if (_photoCollapsed != show)
                {
                    return;
                }

                _photoCollapsed = !show;
                PhotoRoot.Opacity = show ? 1 : 0;
            }
        }

        private void UpdatePhoto(MessageViewModel message)
        {
            if (message.HasSenderPhoto)
            {
                var isLast = message.Delegate?.IsSavedMessagesTab ?? false ? message.IsFirst : message.IsLast;
                if (isLast)
                {
                    if (message.Id != _photoId || PhotoRoot == null || PhotoRoot.Visibility == Visibility.Collapsed)
                    {
                        if (PhotoRoot == null)
                        {
                            PhotoRoot = GetTemplateChild(nameof(PhotoRoot)) as HyperlinkButton;
                            PhotoRoot.Click += Photo_Click;

                            Photo = GetTemplateChild(nameof(Photo)) as ProfilePicture;
                        }

                        _photoId = message.Id;
                        PhotoRoot.Visibility = Visibility.Visible;
                        Photo.Source = ProfilePictureSource.Message(message);
                    }
                }
                else if (PhotoRoot != null)
                {
                    _photoId = null;

                    PhotoRoot.Visibility = Visibility.Collapsed;
                    Photo.Source = null;
                }

                if (PhotoColumn.Width.IsAuto)
                {
                    PhotoColumn.Width = new GridLength(38, GridUnitType.Pixel);
                }
            }
            else
            {
                if (PhotoRoot != null)
                {
                    _photoId = null;
                    _photoCollapsed = false;

                    Photo = null;
                    UnloadTemplateChild(ref PhotoRoot);
                }

                if (PhotoColumn.Width.IsAbsolute)
                {
                    PhotoColumn.Width = new GridLength(0, GridUnitType.Auto);
                }
            }
        }

        private void Photo_Click(object sender, RoutedEventArgs e)
        {
            var message = _message;
            if (message?.Delegate == null)
            {
                return;
            }

            message.Delegate.OpenSender(message);
        }

        private bool _outgoingAction;
        private bool _outgoingSummary;

        private void UpdateAction(MessageViewModel message)
        {
            var chat = message?.Chat;
            if (chat == null)
            {
                return;
            }

            var content = message.GeneratedContent ?? message.Content;
            var light = content is MessageSticker
                or MessageDice
                or MessageStakeDice
                or MessageVideoNote
                or MessageBigEmoji
                or MessageAnimatedEmoji;

            if (content is MessageSponsored)
            {
                FindAction(message.IsVisuallyOutgoing);

                ActionButton.Glyph = Icons.DismissFilled16;
                Action.VerticalAlignment = VerticalAlignment.Top;
                Action.Visibility = Visibility.Visible;

                Automation.SetToolTip(ActionButton, Strings.HideAd);
            }
            else if (light && message.IsChannelPost && message.InteractionInfo?.ReplyInfo != null)
            {
                FindAction(message.IsVisuallyOutgoing);

                ActionButton.Glyph = Icons.ChatEmptyFilled16;
                Action.VerticalAlignment = VerticalAlignment.Bottom;
                Action.Visibility = Visibility.Visible;

                Automation.SetToolTip(ActionButton, message.InteractionInfo.ReplyInfo.ReplyCount > 0
                    ? Locale.Declension(Strings.R.Comments, message.InteractionInfo.ReplyInfo.ReplyCount)
                    : Strings.LeaveAComment);
            }
            else if (message.ChatId == message.ClientService.Options.RepliesBotChatId)
            {
                if (light || message.Delegate?.IsForum is true)
                {
                    FindAction(message.IsVisuallyOutgoing);

                    ActionButton.Glyph = light ? Icons.ChatEmptyFilled16 : Icons.ArrowRightFilled16;
                    Action.VerticalAlignment = VerticalAlignment.Bottom;
                    Action.Visibility = Visibility.Visible;

                    Automation.SetToolTip(ActionButton, Strings.ViewInChat);
                }
                else
                {
                    Action?.Visibility = Visibility.Collapsed;
                }
            }
            else if (message.IsSaved)
            {
                if ((message.ImportInfo != null || message.ForwardInfo?.Origin is MessageOriginHiddenUser) && Action != null)
                {
                    Action.Visibility = Visibility.Collapsed;
                }
                else
                {
                    FindAction(message.IsVisuallyOutgoing);

                    ActionButton.Glyph = Icons.ArrowRightFilled16;
                    Action.VerticalAlignment = VerticalAlignment.Bottom;
                    Action.Visibility = Visibility.Visible;

                    Automation.SetToolTip(ActionButton, Strings.AccDescrOpenChat);
                }
            }
            else if (message.CanBeShared)
            {
                FindAction(message.IsVisuallyOutgoing);

                ActionButton.Glyph = Icons.ShareFilled;
                Action.VerticalAlignment = VerticalAlignment.Bottom;
                Action.Visibility = Visibility.Visible;

                Automation.SetToolTip(ActionButton, Strings.ShareFile);
            }
            else
            {
                Action?.Visibility = Visibility.Collapsed;
            }
        }

        private void FindAction(bool outgoing)
        {
            if (Action == null)
            {
                Action = GetTemplateChild(nameof(Action)) as Border;
                ActionButton = GetTemplateChild(nameof(ActionButton)) as GlyphButton;

                ActionButton.Click += Action_Click;
            }

            if (outgoing && !_outgoingAction)
            {
                _outgoingAction = true;
                Action.Margin = new Thickness(0, 0, 8, 0);
                Grid.SetColumn(Action, 0);
            }
            else if (_outgoingAction && !outgoing)
            {
                _outgoingAction = false;
                Action.Margin = new Thickness(8, 0, 0, 0);
                Grid.SetColumn(Action, 2);
            }
        }

        private void FindSummary(bool outgoing)
        {
            if (Summary == null)
            {
                Summary = GetTemplateChild(nameof(Summary)) as Border;
                SummaryButton = GetTemplateChild(nameof(SummaryButton)) as GlyphButton;

                SummaryButton.Click += Summary_Click;
            }

            if (outgoing && !_outgoingSummary)
            {
                _outgoingSummary = true;
                Summary.Margin = new Thickness(0, 0, 8, 0);
                Grid.SetColumn(Summary, 0);
            }
            else if (_outgoingSummary && !outgoing)
            {
                _outgoingSummary = false;
                Summary.Margin = new Thickness(8, 0, 0, 0);
                Grid.SetColumn(Summary, 2);
            }
        }

        private void Action_Click(object sender, RoutedEventArgs e)
        {
            var message = _message;
            if (message == null)
            {
                return;
            }

            var content = message.GeneratedContent ?? message.Content;
            var light = content is MessageSticker
                or MessageDice
                or MessageStakeDice
                or MessageVideoNote
                or MessageBigEmoji
                or MessageAnimatedEmoji;

            if (content is MessageSponsored)
            {
                message.Delegate.HideSponsoredMessage(message);
            }
            else if (light && message.IsChannelPost && message.InteractionInfo?.ReplyInfo != null)
            {
                message.Delegate.OpenThread(message);
            }
            else if (message.ChatId == message.ClientService.Options.RepliesBotChatId)
            {
                message.Delegate.OpenThread(message);
            }
            else if (message.IsSaved)
            {
                if (message.ForwardInfo?.Origin is MessageOriginUser or MessageOriginChat && message.ForwardInfo.Source != null)
                {
                    message.Delegate.OpenChat(message.ForwardInfo.Source.ChatId, message.ForwardInfo.Source.MessageId);
                }
                else if (message.ForwardInfo?.Origin is MessageOriginChannel fromChannel)
                {
                    message.Delegate.OpenChat(fromChannel.ChatId, fromChannel.MessageId);
                }
            }
            else
            {
                message.Delegate.ForwardMessage(message);
            }
        }

        private void Summary_Click(object sender, RoutedEventArgs e)
        {
            var message = _message;
            if (message == null)
            {
                return;
            }

            message.Delegate.SummarizeMessage(message);
        }

        public void UpdateMessageReply(MessageViewModel message)
        {
            if (!_templateApplied)
            {
                return;
            }

            if (Reply == null && message.ReplyTo != null && message.ReplyToState != MessageReplyToState.Hidden)
            {
                Reply = GetTemplateChild(nameof(Reply)) as MessageReply;
                Reply.Click += Reply_Click;

                Panel.Reply = Reply;
            }

            Reply?.UpdateMessageReply(message);
        }

        public void UpdateMessageHeader(MessageViewModel message)
        {
            var chat = message?.Chat;
            if (chat == null || !_templateApplied)
            {
                return;
            }

            // IsVisuallyOutgoing here can't be used
            var outgoing = (message.IsOutgoing && !message.IsChannelPost && message.SenderId is MessageSenderUser) || (message.IsSaved && message.ForwardInfo?.Source is { IsOutgoing: true });
            var content = message.GeneratedContent ?? message.Content;
            var light = content is MessageSticker
                or MessageDice
                or MessageStakeDice
                or MessageVideoNote
                or MessageBigEmoji
                or MessageAnimatedEmoji;

            var shown = false;
            var header = false;
            var forward = false;

            var isFirst = message.Delegate?.IsSavedMessagesTab ?? false ? message.IsLast : message.IsFirst;

            if (!light && isFirst && (message.IsSaved || message.IsVerificationCode) && !outgoing)
            {
                var title = string.Empty;
                var foreground = default(SolidColorBrush);

                if (message.ForwardInfo?.Origin is MessageOriginUser fromUser && message.ClientService.TryGetUser(fromUser.SenderUserId, out User fromUserUser))
                {
                    title = fromUserUser.FullName();
                    foreground = message.ClientService.GetAccentBrush(fromUserUser);
                }
                else if (message.ForwardInfo?.Origin is MessageOriginChat fromChat && message.ClientService.TryGetChat(fromChat.SenderChatId, out Chat fromChatChat))
                {
                    title = message.ClientService.GetTitle(fromChatChat);
                    foreground = message.ClientService.GetAccentBrush(fromChatChat);
                }
                else if (message.ForwardInfo?.Origin is MessageOriginChannel fromChannel && message.ClientService.TryGetChat(fromChannel.ChatId, out Chat fromChannelChat))
                {
                    title = message.ClientService.GetTitle(fromChannelChat);
                    foreground = message.ClientService.GetAccentBrush(fromChannelChat);
                }
                else if (message.ForwardInfo?.Origin is MessageOriginHiddenUser fromHiddenUser)
                {
                    title = fromHiddenUser.SenderName;
                }
                else if (message.ImportInfo != null)
                {
                    title = message.ImportInfo.SenderName;
                }

                LoadHeaderLabel();
                header = true;
                shown = true;

                if (foreground != null)
                {
                    HeaderLink.Foreground = foreground;
                }
                else
                {
                    HeaderLink.ClearValue(TextElement.ForegroundProperty);
                }

                HeaderLinkRun.Text = title;
                Identity.ClearStatus();
            }
            else if (!light && isFirst && !outgoing && (message.HasSenderPhoto || (!message.IsChannelPost && !message.IsDirectMessagesChatTopicMessage)) && (chat.Type is ChatTypeBasicGroup || chat.Type is ChatTypeSupergroup || message.GuestBotCallerId != null))
            {
                if (message.ClientService.TryGetUser(message.SenderId, out User senderUser))
                {
                    LoadHeaderLabel();
                    header = true;
                    shown = true;

                    var title = senderUser.FullName();
                    var foreground = message.IsOutgoing && !message.IsChannelPost
                        ? null
                        : message.ClientService.GetAccentBrush(senderUser);

                    if (foreground != null)
                    {
                        HeaderLink.Foreground = foreground;
                        Identity.Foreground = foreground.WithOpacity(0.6);
                    }
                    else
                    {
                        HeaderLink.ClearValue(TextElement.ForegroundProperty);
                        Identity.ClearValue(ForegroundProperty);
                    }

                    HeaderLinkRun.Text = title;
                    Identity.SetStatus(message.ClientService, senderUser);

                    if (message.GuestBotCallerId != null)
                    {
                        if (HeaderLabel.Inlines.Count > 1)
                        {
                            HeaderLabel.Inlines.RemoveAt(HeaderLabel.Inlines.Count - 1);
                        }

                        var run = new Run
                        {
                            Text = string.Format(header ? " {0} {1}" : "{0} {1}", Strings.GuestBotFor, message.ClientService.GetTitle(message.GuestBotCallerId, true)),
                            FontWeight = FontWeights.Normal
                        };

                        if (foreground != null)
                        {
                            run.Foreground = foreground;
                        }

                        HeaderLabel.Inlines.Add(run);
                    }
                }
                else if (message.ClientService.TryGetChat(message.SenderId, out Chat senderChat))
                {
                    LoadHeaderLabel();
                    header = true;
                    shown = true;

                    var title = senderChat.Title;
                    var foreground = message.IsOutgoing && !message.IsChannelPost
                        ? null
                        : message.ClientService.GetAccentBrush(senderChat);

                    if (foreground != null)
                    {
                        HeaderLink.Foreground = foreground;
                        Identity.Foreground = foreground.WithOpacity(0.6);
                    }
                    else
                    {
                        HeaderLink.ClearValue(TextElement.ForegroundProperty);
                        Identity.ClearValue(ForegroundProperty);
                    }

                    HeaderLinkRun.Text = title;
                    Identity.SetStatus(message.ClientService, senderChat);
                }
            }
            else if (!light && message.IsChannelPost && message.Content is not MessageSponsored && chat.Type is ChatTypeSupergroup && string.IsNullOrEmpty(message.ForwardInfo?.PublicServiceAnnouncementType))
            {
                LoadHeaderLabel();
                header = true;
                shown = true;

                var foreground = message.ClientService.GetAccentBrush(chat);
                var title = chat.Title;

                HeaderLink.Foreground = foreground;
                HeaderLinkRun.Text = title;
                Identity.Foreground = foreground.WithOpacity(0.6);
                Identity.SetStatus(message.ClientService, chat);
            }
            else if (HeaderLabel != null)
            {
                HeaderLinkRun.Text = string.Empty;
            }

            if (message.Content is MessageAsyncStory story)
            {
                LoadForwardLabel();
                forward = true;

                ForwardHeader.UpdateMessage(message, light);
            }
            else if (message.ForwardInfo != null && !message.IsVerificationCode && (!message.IsSaved || !message.ForwardInfo.HasSameOrigin()))
            {
                LoadForwardLabel();
                forward = true;

                ForwardHeader.UpdateMessage(message, light);

                if (message.ForwardInfo.PublicServiceAnnouncementType.Length > 0)
                {
                    if (PsaInfo == null)
                    {
                        PsaInfo = GetTemplateChild(nameof(PsaInfo)) as GlyphButton;
                        PsaInfo.Click += PsaInfo_Click;
                    }

                    PsaInfo.Visibility = Visibility.Visible;
                }
                else
                {
                    PsaInfo?.Visibility = Visibility.Collapsed;
                }
            }
            else
            {
                ForwardHeader?.UpdateMessage(message, light);

                PsaInfo?.Visibility = Visibility.Collapsed;
            }

            var viaBot = message.ClientService.GetUser(message.ViaBotUserId);
            if (viaBot != null && viaBot.HasActiveUsername(out string viaBotUsername))
            {
                LoadHeaderLabel();
                shown = true;

                var text = string.Format(header ? " {0} @" : "{0} @", Strings.ViaBot);
                var hyperlink = new Hyperlink();
                hyperlink.Inlines.Add(CreateRun(text, FontWeights.Normal));
                hyperlink.Inlines.Add(CreateRun(viaBotUsername));
                hyperlink.UnderlineStyle = UnderlineStyle.None;
                hyperlink.Foreground = light ? new SolidColorBrush(Colors.White) : GetBrush("MessageHeaderForegroundBrush");
                hyperlink.Click += ViaBot_Click;

                if (HeaderLabel.Inlines.Count > 1)
                {
                    HeaderLabel.Inlines.RemoveAt(HeaderLabel.Inlines.Count - 1);
                }

                HeaderLabel.Inlines.Add(hyperlink);
            }
            else if (header && HeaderLabel?.Inlines.Count > 1 && message.GuestBotCallerId == null)
            {
                HeaderLabel.Inlines.RemoveAt(HeaderLabel.Inlines.Count - 1);
            }

            if (shown)
            {
                ChatMemberRank rank = ChatMemberRank.Other;
                var title = message.Delegate?.GetMemberTag(message, out rank);
                var boosts = string.Empty;

                if (message.SenderBoostCount > 0 && !outgoing)
                {
                    if (title.Length > 0)
                    {
                        title += " ";
                    }

                    if (message.SenderBoostCount > 1)
                    {
                        boosts = $"{Icons.Boosters212} {message.SenderBoostCount}";
                    }
                    else
                    {
                        boosts = Icons.Boosters12;
                    }
                }

                if (shown && !outgoing && title?.Length > 0)
                {
                    if (MemberTag == null)
                    {
                        LoadTemplateChild(ref MemberTag);
                        MemberTag.Tapped += MemberTag_Tapped;
                    }

                    MemberTag.Text = title;

                    if (rank != ChatMemberRank.Other)
                    {
                        var color = rank == ChatMemberRank.Owner
                            ? Color.FromArgb(0xFF, 0x65, 0x60, 0xF6)
                            : Color.FromArgb(0xFF, 0x75, 0xC8, 0x73);

                        MemberTag.Background = new SolidColorBrush(color) { Opacity = 0.2 };
                        MemberTag.Foreground = new SolidColorBrush(color.Darken());
                    }
                    else
                    {
                        MemberTag.ClearValue(BackgroundProperty);
                        MemberTag.ClearValue(ForegroundProperty);
                    }

                    if (boosts.Length > 0)
                    {
                        if (BoostCount == null)
                        {
                            LoadTemplateChild(ref BoostCount);
                            BoostCount.Tapped += BoostCount_Tapped;
                        }

                        BoostCount.Text = boosts;
                    }
                    else
                    {
                        BoostCount?.Tapped -= BoostCount_Tapped;
                        UnloadTemplateChild(ref BoostCount);
                    }
                }
                else if (shown && !message.IsChannelPost && message.SenderId is MessageSenderChat && message.ForwardInfo != null)
                {
                    if (MemberTag == null)
                    {
                        LoadTemplateChild(ref MemberTag);
                        MemberTag.Tapped += MemberTag_Tapped;
                    }

                    MemberTag.Text = Strings.DiscussChannel;

                    MemberTag.ClearValue(BackgroundProperty);
                    MemberTag.ClearValue(ForegroundProperty);

                    BoostCount?.Tapped -= BoostCount_Tapped;
                    UnloadTemplateChild(ref BoostCount);
                }
                else
                {
                    MemberTag?.Tapped -= MemberTag_Tapped;
                    BoostCount?.Tapped -= BoostCount_Tapped;

                    UnloadTemplateChild(ref MemberTag);
                    UnloadTemplateChild(ref BoostCount);
                }

                if (header is false)
                {
                    Identity?.ClearStatus();
                }

                HeaderPanel.Visibility = Visibility.Visible;
                Header.Visibility = Visibility.Visible;

                ForwardHeader?.Margin = new Thickness(0, -2, 0, 2);
            }
            else
            {
                MemberTag?.Tapped -= MemberTag_Tapped;
                BoostCount?.Tapped -= BoostCount_Tapped;

                UnloadTemplateChild(ref MemberTag);
                UnloadTemplateChild(ref BoostCount);

                //if (HeaderPanel != null)
                //{
                //    XamlMarkupHelper.UnloadObject(HeaderPanel);
                //    HeaderPanel = null;
                //    HeaderLabel = null;
                //    Identity = null;
                //}

                if (HeaderPanel != null)
                {
                    HeaderPanel.Visibility = Visibility.Collapsed;
                    Identity.ClearStatus();
                }

                Header.Visibility = (message.ReplyTo != null && message.ReplyToState != MessageReplyToState.Hidden) || forward ? Visibility.Visible : Visibility.Collapsed;

                ForwardHeader?.Margin = new Thickness(0, 0, 0, 2);
            }
        }

        private async void MemberTag_Tapped(object sender, TappedRoutedEventArgs e)
        {
            if (_message is not MessageViewModel message)
            {
                return;
            }

            var response = await message.ClientService.SendAsync(new GetChatMember(message.ChatId, message.SenderId));
            if (response is ChatMember member && message.ClientService.CanEditTag(message.Chat, member))
            {
                _message?.Delegate?.NavigationService?.ShowPopup(new MemberTagEditPopup(message.ClientService, message.Delegate.Aggregator, message.Chat, member));
            }
            else
            {
                _message?.Delegate?.NavigationService?.ShowPopup(new MemberTagInfoPopup(_message));
            }
        }

        private void BoostCount_Tapped(object sender, TappedRoutedEventArgs e)
        {
            // TODO: boost chat info
        }

        private TextBlock LoadHeaderLabel()
        {
            if (HeaderPanel == null)
            {
                HeaderPanel = GetTemplateChild(nameof(HeaderPanel)) as Grid;
                HeaderLabel = GetTemplateChild(nameof(HeaderLabel)) as TextBlock;
                Identity = GetTemplateChild(nameof(Identity)) as IdentityIcon;

                HeaderLink = HeaderLabel.Inlines[0] as Hyperlink;
                HeaderLinkRun = HeaderLink.Inlines[0] as Run;

                HeaderLink.Click += From_Click;
            }

            return HeaderLabel;
        }

        private void LoadForwardLabel()
        {
            if (ForwardHeader == null)
            {
                ForwardHeader = GetTemplateChild(nameof(ForwardHeader)) as MessageForwardHeader;
                ForwardHeader.Click += FwdFrom_Click;
            }
        }

        private void ViaBot_Click(Hyperlink sender, HyperlinkClickEventArgs args)
        {
            if (_message is not MessageViewModel message || message.Delegate?.IsDialog is not true)
            {
                return;
            }

            message.Delegate.OpenViaBot(message.ViaBotUserId);
        }

        private void FwdFrom_Click(object sender, RoutedEventArgs args)
        {
            if (_message is not MessageViewModel message || message.Delegate?.IsDialog is not true)
            {
                return;
            }

            if (message.ForwardInfo?.Origin is MessageOriginUser fromUser)
            {
                message.Delegate.OpenUser(fromUser.SenderUserId);
            }
            else if (message.ForwardInfo?.Origin is MessageOriginChat fromChat)
            {
                message.Delegate.OpenChat(fromChat.SenderChatId, true);
            }
            else if (message.ForwardInfo?.Origin is MessageOriginChannel fromChannel)
            {
                message.Delegate.OpenChat(fromChannel.ChatId, fromChannel.MessageId);
            }
            else if (message.ForwardInfo?.Origin is MessageOriginHiddenUser)
            {
                ToastPopup.Show(XamlRoot, Strings.HidAccount, ToastPopupIcon.Info);
            }
            else if (message.Content is MessageAsyncStory asyncStory)
            {
                message.Delegate.OpenChat(asyncStory.StoryPosterChatId, true);
            }
        }

        private void From_Click(Hyperlink sender, HyperlinkClickEventArgs args)
        {
            if (_message is not MessageViewModel message || message.Delegate?.IsDialog is not true)
            {
                return;
            }

            if (message.IsSaved || message.IsVerificationCode)
            {
                FwdFrom_Click(sender, args);
            }
            else if (message.ClientService.TryGetChat(message.SenderId, out Chat senderChat))
            {
                if (senderChat.Type is ChatTypeSupergroup supergroup && supergroup.IsChannel)
                {
                    message.Delegate.OpenChat(senderChat.Id);
                }
                else
                {
                    message.Delegate.OpenChat(senderChat.Id, true);
                }
            }
            else if (message.SenderId is MessageSenderUser senderUser)
            {
                message.Delegate.OpenUser(senderUser.UserId);
            }
        }

        public void UpdateMessageState(MessageViewModel message)
        {
            if (!_templateApplied)
            {
                return;
            }

            Footer.UpdateMessageState(message);
        }

        public void UpdateMessageEdited(MessageViewModel message)
        {
            if (!_templateApplied)
            {
                return;
            }

            Footer.UpdateMessageEdited(message);
            UpdateMessageReplyMarkup(message);
        }

        private void UpdateMessageReplyMarkup(MessageViewModel message)
        {
            if (message.ReplyMarkup is ReplyMarkupInlineKeyboard || message.SuggestedPostInfo is SuggestedPostInfo { State: SuggestedPostStateApproved })
            {
                if (Markup == null)
                {
                    Markup = GetTemplateChild(nameof(Markup)) as ReplyMarkupInlinePanel;
                    Markup.InlineButtonClick += ReplyMarkup_ButtonClick;
                }

                Markup.Visibility = Visibility.Visible;
                Markup.Update(message);

                if (!_hasReplyMarkup)
                {
                    UpdateAttach(message);
                }
            }
            else
            {
                if (Markup != null)
                {
                    Markup.Visibility = Visibility.Collapsed;
                    Markup.Children.Clear();
                }

                if (_hasReplyMarkup)
                {
                    UpdateAttach(message);
                }
            }
        }

        public void UpdateMessageIsPinned(MessageViewModel message)
        {
            if (!_templateApplied)
            {
                return;
            }

            Footer.UpdateMessageIsPinned(message);
        }

        public void UpdateMessageEffect(MessageViewModel message)
        {
            if (!_templateApplied)
            {
                return;
            }

            Footer.UpdateMessageEffect(message);
        }

        public bool PlayMessageEffect(MessageViewModel message)
        {
            if (!_templateApplied)
            {
                return false;
            }

            return Footer.PlayMessageEffect(message);
        }

        private long _recentRepliersChatId;
        private long _recentRepliersMessageId;

        public void UpdateMessageInteractionInfo(MessageViewModel message)
        {
            var chat = message?.Chat;
            if (chat == null || !_templateApplied)
            {
                return;
            }

            Footer.UpdateMessageInteractionInfo(message);
            UpdateMessageReactions(message, false);

            if (message.Delegate == null || !message.Delegate.IsDialog)
            {
                return;
            }

            UpdateAction(message);

            var content = message.GeneratedContent ?? message.Content;
            if (content is MessageSticker or MessageAnimatedEmoji or MessageDice or MessageStakeDice or MessageVideoNote or MessageBigEmoji)
            {
                Thread?.Visibility = Visibility.Collapsed;

                return;
            }

            var info = message.InteractionInfo?.ReplyInfo;
            if (info == null || !message.IsChannelPost)
            {
                if (message.ChatId == message.ClientService.Options.RepliesBotChatId && message.Id != 0)
                {
                    if (Thread == null)
                    {
                        Thread = GetTemplateChild(nameof(Thread)) as HyperlinkButton;
                        RecentRepliers = GetTemplateChild(nameof(RecentRepliers)) as RecentUserHeads;
                        ThreadGlyph = GetTemplateChild(nameof(ThreadGlyph)) as TextBlock;
                        ThreadLabel = GetTemplateChild(nameof(ThreadLabel)) as TextBlock;

                        Thread.Click += Thread_Click;
                    }

                    RecentRepliers.Items.Clear();
                    ThreadGlyph.Visibility = Visibility.Visible;
                    ThreadLabel.Text = Strings.ViewInChat;

                    AutomationProperties.SetName(Thread, Strings.ViewInChat);

                    Thread.Visibility = Visibility.Visible;
                }
                else
                {
                    Thread?.Visibility = Visibility.Collapsed;
                }
            }
            else
            {
                if (Thread == null)
                {
                    Thread = GetTemplateChild(nameof(Thread)) as HyperlinkButton;
                    RecentRepliers = GetTemplateChild(nameof(RecentRepliers)) as RecentUserHeads;
                    ThreadGlyph = GetTemplateChild(nameof(ThreadGlyph)) as TextBlock;
                    ThreadLabel = GetTemplateChild(nameof(ThreadLabel)) as TextBlock;

                    Thread.Click += Thread_Click;
                    RecentRepliers.RecentUserHeadChanged += RecentRepliers_RecentUserHeadChanged;
                }

                if (RecentRepliers.Items.Count > 0 && _recentRepliersChatId == message.ChatId && _recentRepliersMessageId == message.Id)
                {
                    RecentRepliers.Items.ReplaceDiff(info.RecentReplierIds);
                }
                else
                {
                    RecentRepliers.Items.ReplaceWith(info.RecentReplierIds);
                }

                _recentRepliersChatId = message.ChatId;
                _recentRepliersMessageId = message.Id;

                ThreadGlyph.Visibility = info.RecentReplierIds.Count > 0
                        ? Visibility.Collapsed
                        : Visibility.Visible;

                var commentsText = info.ReplyCount > 0
                    ? Locale.Declension(Strings.R.Comments, info.ReplyCount)
                    : Strings.LeaveAComment;

                if (info.ReplyCount > 0 && info.LastReadInboxMessageId > 0 && info.LastMessageId > info.LastReadInboxMessageId)
                {
                    commentsText += "\u00A0\u2022";
                }

                ThreadLabel.Text = commentsText;
                AutomationProperties.SetName(Thread, commentsText);

                Thread.Visibility = Visibility.Visible;
            }
        }

        private void RecentRepliers_RecentUserHeadChanged(ProfilePicture sender, MessageSender messageSender)
        {
            sender.Source = ProfilePictureSource.MessageSender(_message.ClientService, messageSender);
        }

        public void UpdateMessageReactions(MessageViewModel message, bool animate)
        {
            var media = Grid.GetRow(Media);
            var footer = Grid.GetRow(Footer);

            var content = message.GeneratedContent ?? message.Content;
            if (content is MessageSticker or MessageAnimatedEmoji or MessageDice or MessageStakeDice or MessageVideoNote or MessageBigEmoji || (media == footer && IsFullMedia(content)))
            {
                Reactions?.UpdateMessageReactions(null);

                if (message.InteractionInfo?.Reactions?.Reactions.Count > 0)
                {
                    LoadTemplateChild(ref MediaReactions);
                    MediaReactions.HorizontalContentAlignment = message.IsVisuallyOutgoing ? HorizontalAlignment.Right : HorizontalAlignment.Left;
                    MediaReactions.UpdateMessageReactions(message, animate);
                }
                else
                {
                    MediaReactions?.UpdateMessageReactions(null);
                }
            }
            else
            {
                MediaReactions?.UpdateMessageReactions(null);

                if (message.InteractionInfo?.Reactions?.Reactions.Count > 0)
                {
                    LoadTemplateChild(ref Reactions);
                    Reactions.UpdateMessageReactions(message, animate);
                }
                else
                {
                    Reactions?.UpdateMessageReactions(null);
                }
            }
        }

        public void UpdateMessageContentOpened(MessageViewModel message)
        {
            if (!_templateApplied)
            {
                return;
            }

            if (Media.Child is IContentWithFile content && content.IsValid(message.GeneratedContent ?? message.Content, true))
            {
                content.UpdateMessageContentOpened(message);
            }
        }

        public void UpdateMessageFactCheck(MessageViewModel message)
        {
            // TODO: this isn't very optimized
            UpdateMessageContent(message);
        }

        public void UpdateMessageSuggestedPostInfo(MessageViewModel message)
        {
            if (Parent is MessageSelector selector)
            {
                selector.UpdateMessageSuggestedPostInfo(message);
            }

            UpdateMessageReplyMarkup(message);
        }

        public void UpdateMessageContent(MessageViewModel message)
        {
            if (Parent is MessageSelector selector)
            {
                selector.UpdateMessageStakeDice(message);
            }

            UpdateMessageContentLayout(message);
            UpdateMessageText(message);
            UpdateMessageContentControl(message);
        }

        public void UpdateMessageTextLayout(MessageViewModel message)
        {
            UpdateMessageContentLayout(message);
            UpdateMessageText(message);
        }

        public void UpdateMessageContentLayout(MessageViewModel message)
        {
            var chat = message?.Chat;
            if (chat == null || !_templateApplied)
            {
                return;
            }

            Panel.ForceNewLine = message?.GeneratedContent is MessageBigEmoji;

            // IsVisuallyOutgoing here can't be used
            var outgoing = (message.IsOutgoing && !message.IsChannelPost && message.SenderId is MessageSenderUser) || (message.IsSaved && message.ForwardInfo?.Source is { IsOutgoing: true });

            var aboveMedia = message.ShowCaptionAboveMedia();
            var factCheck = message.FactCheck == null && message.SummarizedText == null ? 0 : 1;

            var content = message.GeneratedContent ?? message.Content;
            if (content is MessageText text)
            {
                if (text.LinkPreview == null && factCheck == 0)
                {
                    ContentPanel.Padding = new Thickness(0, 4, 0, 0);
                    Media.Margin = new Thickness(0);
                    FooterToNormal();
                    Grid.SetRow(Footer, 2);
                    Grid.SetRow(Message, 2);
                    Panel.Placeholder = true;
                }
                else
                {
                    var caption = text.LinkPreview?.ShowAboveText ?? false;

                    ContentPanel.Padding = new Thickness(0, 4, 0, 0);
                    Media.Margin = new Thickness(10, caption ? -4 : -6, 10, -2);
                    FooterToNormal();
                    Grid.SetRow(Footer, caption ? 4 : 4);
                    Grid.SetRow(Message, caption ? 4 : 2);
                    Panel.Placeholder = caption;
                }
            }
            else if (IsFullMedia(content))
            {
                var top = 0;
                var bottom = 0;

                var isFirst = message.Delegate?.IsSavedMessagesTab ?? false ? message.IsLast : message.IsFirst;

                if (isFirst && !outgoing && !message.IsChannelPost && !message.IsDirectMessagesChatTopicMessage && (chat.Type is ChatTypeBasicGroup || chat.Type is ChatTypeSupergroup))
                {
                    top = 4;
                }
                if (isFirst && message.IsSaved)
                {
                    top = 4;
                }
                if ((message.ForwardInfo != null && !message.IsSaved) || message.ViaBotUserId != 0 || (message.ReplyTo != null && message.ReplyToState != MessageReplyToState.Hidden) || message.IsChannelPost || message.Content is MessageAsyncStory)
                {
                    top = 4;
                }
                if (content.HasCaption() && aboveMedia)
                {
                    top = 4;
                }

                var caption = content is MessageVenue || (content.HasCaption() && !aboveMedia);
                if (caption || (factCheck > 0 && !aboveMedia))
                {
                    FooterToNormal();
                    bottom = 4;
                }
                else if (content is MessageCall || (content is MessageLiveLocation location && !location.Location.IsExpired(location.ExpiresIn, message.Date)))
                {
                    FooterToHidden();
                }
                else
                {
                    FooterToMedia();
                }

                ContentPanel.Padding = new Thickness(0, top, 0, 0);
                Media.Margin = new Thickness(0, aboveMedia ? 0 : top, 0, bottom);
                Grid.SetRow(Footer, (aboveMedia ? 0 : factCheck) + (caption ? 4 : 3));
                Grid.SetRow(Message, caption ? 4 : aboveMedia ? 2 : 5);
                Panel.Placeholder = caption;
            }
            else if (content is MessageSticker or MessageAnimatedEmoji or MessageDice or MessageStakeDice or MessageVideoNote or MessageBigEmoji)
            {
                ContentPanel.Padding = new Thickness(0);
                Media.Margin = new Thickness(0);

                if (message.IsVisuallyOutgoing)
                {
                    FooterToLightMedia(true);
                    Grid.SetRow(Footer, 3);
                    Grid.SetRow(Message, 2);
                    Panel.Placeholder = false;
                }
                else
                {
                    FooterToLightMedia(false);
                    Grid.SetRow(Footer, content is MessageBigEmoji ? 2 : 3);
                    Grid.SetRow(Message, 2);
                    Panel.Placeholder = content is MessageBigEmoji;
                }
            }
            else if (content is MessageGame or MessageGiveaway or MessageGiveawayWinners or MessageUnsupported or MessageAsyncStory)
            {
                ContentPanel.Padding = new Thickness(0, 4, 0, 0);
                Media.Margin = new Thickness(10, -6, 10, 0);
                FooterToNormal();
                Grid.SetRow(Footer, 4);
                Grid.SetRow(Message, 2);
                Panel.Placeholder = false;
            }
            else if (content is MessagePoll or MessageChecklist)
            {
                ContentPanel.Padding = new Thickness(0, 0, 0, 0);
                Media.Margin = new Thickness(0);
                FooterToNormal();
                Grid.SetRow(Footer, 4);
                Grid.SetRow(Message, 2);
                Panel.Placeholder = false;
            }
            else if (content is MessageInvoice invoice)
            {
                var caption = invoice.ProductInfo.Photo == null;

                ContentPanel.Padding = new Thickness(0, 4, 0, 0);
                Media.Margin = new Thickness(10, 0, 10, 6);
                FooterToNormal();
                Grid.SetRow(Footer, 4);
                Grid.SetRow(Message, 2);
                Panel.Placeholder = caption;
            }
            else if (content is MessageContact)
            {
                ContentPanel.Padding = new Thickness(0, 4, 0, 0);
                Media.Margin = new Thickness(10, 4, 10, 0);
                FooterToNormal();
                Grid.SetRow(Footer, 4);
                Grid.SetRow(Message, 2);
                Panel.Placeholder = false;
            }
            else if (content is MessageRichMessage)
            {
                ContentPanel.Padding = new Thickness(0, 4, 0, 0);
                Media.Margin = new Thickness(0);
                FooterToNormal();
                Grid.SetRow(Footer, 4);
                Grid.SetRow(Message, 2);
                Panel.Placeholder = true;

                //ContentPanel.Padding = new Thickness(0, 0, 0, 0);
                //Media.Margin = new Thickness(0, 0, 0, 0);
                //FooterToNormal();
                //Grid.SetRow(Footer, 4);
                //Grid.SetRow(Message, 2);
                //Panel.Placeholder = false;
            }
            else
            {
                var caption = content.HasCaption();
                if (content is MessageCall)
                {
                    FooterToHidden();
                }
                else
                {
                    FooterToNormal();
                }

                ContentPanel.Padding = new Thickness(0, 4, 0, 0);
                Media.Margin = new Thickness(10, 4, 10, 8);
                Grid.SetRow(Footer, factCheck + (caption ? 4 : 3));
                Grid.SetRow(Message, caption ? 4 : 5);
                Panel.Placeholder = caption;
            }

            if (Panel.Children.Count > 0 && Panel.Children[0] is MessageSummary summary)
            {
                if (message.SummarizedText == null)
                {
                    Panel.Children.Remove(summary);

                    if (message.FactCheck != null)
                    {
                        Panel.Children.Insert(0, new MessageFactCheck(message));
                    }
                }
            }
            else if (Panel.Children.Count > 0 && Panel.Children[0] is MessageFactCheck factChecko)
            {
                if (message.SummarizedText != null)
                {
                    summary = new MessageSummary();
                    summary.Click += Summary_Click;

                    Panel.Children.Remove(factChecko);
                    Panel.Children.Insert(0, summary);
                }
                else if (message.FactCheck != null)
                {
                    factChecko.UpdateMessage(message);
                }
                else
                {
                    Panel.Children.Remove(factChecko);
                }
            }
            else if (message.SummarizedText != null)
            {
                summary = new MessageSummary();
                summary.Click += Summary_Click;

                Panel.Children.Insert(0, summary);
            }
            else if (message.FactCheck != null)
            {
                Panel.Children.Insert(0, new MessageFactCheck(message));
            }
        }

        public void UpdateMessageContentControl(MessageViewModel message)
        {
            var content = message.GeneratedContent ?? message.Content;

            if (Media.Child is IContent media)
            {
                if (media.IsValid(content, true))
                {
                    media.UpdateMessage(message);
                    return;
                }
                else
                {
                    media.Recycle();
                }
            }

            //if (Media.Child is StickerContent or VideoNoteContent)
            //{
            //    UpdateAttach(message);
            //}

            Media.Child = content switch
            {
                MessageText textMessage when textMessage.LinkPreview != null => /*textMessage.LinkPreview.InstantViewVersion != 0 ? new InstantContent(message) :*/ new WebPageContent(message),
                MessageRichMessage => new InstantContent(message),
                MessageAlbum => new AlbumContent(message),
                MessagePaidAlbum => new PaidMediaContent(message),
                MessageAnimation => new AnimationContent(message),
                MessageAudio => new AudioContent(message),
                MessageCall or MessageGroupCall => new CallContent(message),
                MessageContact => new ContactContent(message),
                MessageDice => new DiceContent(message),
                MessageDocument => new DocumentContent(message),
                MessageGame => new GameContent(message),
                MessageInvoice invoice when invoice.PaidMedia is PaidMediaPhoto => new PhotoContent(message),
                MessageInvoice invoice when invoice.PaidMedia is PaidMediaVideo => new VideoContent(message),
                MessageInvoice invoice when invoice.PaidMedia is PaidMediaPreview => new InvoicePreviewContent(message),
                MessageInvoice invoice when invoice.ProductInfo.Photo != null => new InvoicePhotoContent(message),
                MessageInvoice => new InvoiceContent(message),
                MessageLiveLocation => new LiveLocationContent(message),
                MessageLocation => new LocationContent(message),
                MessagePhoto => new PhotoContent(message),
                MessagePoll => new PollContent(message),
                MessageChecklist => new ChecklistContent(message),
                MessageSticker => new StickerContent(message),
                MessageStakeDice => new StakeDiceContent(message),
                MessageVenue => new VenueContent(message),
                MessageVideo => new VideoContent(message),
                MessageVideoNote => new VideoNoteContent(message),
                MessageVoiceNote => new VoiceNoteContent(message),
                MessageGiveaway or MessageGiveawayWinners => new GiveawayContent(message),
                MessageAsyncStory story when story.State != MessageStoryState.Expired => new AspectView
                {
                    Constraint = message
                },
                MessageAnimatedEmoji => new StickerContent(message),
                MessageSponsored => new SponsoredContent(message),
                MessageUnsupported => new UnsupportedContent(message),
                _ => null
            };
        }

        public IPlayerView GetPlaybackElement()
        {
            if (Media?.Child is IContentWithPlayback content)
            {
                return content.GetPlaybackElement();
            }
            else if (Media?.Child is IPlayerView playback)
            {
                return playback;
            }

            return null;
        }

        public void UpdateMessageText(MessageViewModel message)
        {
            var result = false;
            var processedText = message.SummarizedText ?? message.TranslatedText;

            var styledText = processedText switch
            {
                MessageTranslateResultSummary summary => summary.Text,
                MessageTranslateResultText translated => message.Delegate?.IsTranslating ?? false
                    ? translated.Text
                    : message.Text,
                _ => message.Text
            };

            if (styledText != null && message.Content is not MessageAnimatedEmoji and not MessageRichMessage)
            {
                var fontSize = 0d;

                if (message.GeneratedContent is MessageBigEmoji bigEmoji)
                {
                    if (bigEmoji.Text.Entities.Count > 0)
                    {
                        var height = 180 * message.ClientService.Config.GetNamedNumber("emojies_animated_zoom", 0.625f);
                        var ratio = 14d / 20d;
                        var scaledFontSize = height * ratio;
                        var step = (scaledFontSize - 14) / 7;

                        fontSize = scaledFontSize - step * Math.Min(7, bigEmoji.Count);
                    }
                    else
                    {
                        fontSize = 32;
                    }
                }

                Message.ShowHideSkeleton(processedText is MessageTranslateResultPending);
                Message.SetText(message.ClientService, styledText, fontSize);
                Message.SetQuery(_query);

                ContentPanel.MaxWidth = Message.HasCodeBlocks ? double.PositiveInfinity : 432;
                Message.Visibility = styledText.Text.Length > 0
                    ? Visibility.Visible
                    : Visibility.Collapsed;

                if (message.CanSummarizeText)
                {
                    FindSummary(message.IsVisuallyOutgoing);

                    SummaryButton.Glyph = message.SummarizedText == null ? Icons.ArrowMinimizeSparkles16 : Icons.ArrowMaximizeSparkles16;
                    Summary.Visibility = Visibility.Visible;

                    Automation.SetToolTip(SummaryButton, Strings.SummaryTitle);
                }
                else
                {
                    Summary?.Visibility = Visibility.Collapsed;
                }

                return;
            }
            //else
            //{
            //    ContentPanel.MaxWidth = 432;
            //    Message.Visibility = Visibility.Collapsed;

            //    return;
            //}

            var content = message.GeneratedContent ?? message.Content;
            switch (content)
            {
                case MessageText text:
                    result = ReplaceEntities(message, text.Text);
                    break;
                case MessageAlbum album:
                    result = ReplaceEntities(message, album.Caption);
                    break;
                case MessagePaidAlbum paidAlbum:
                    result = ReplaceEntities(message, paidAlbum.Caption);
                    break;
                case MessageAnimation animation:
                    result = ReplaceEntities(message, animation.Caption);
                    break;
                case MessageAudio audio:
                    result = ReplaceEntities(message, audio.Caption);
                    break;
                case MessageDocument document:
                    result = ReplaceEntities(message, document.Caption);
                    break;
                case MessageInvoice invoice:
                    result = ReplaceEntities(message, invoice.PaidMediaCaption);
                    break;
                case MessagePhoto photo:
                    result = ReplaceEntities(message, photo.Caption);
                    break;
                case MessageVideo video:
                    result = ReplaceEntities(message, video.Caption);
                    break;
                case MessageVoiceNote voiceNote:
                    result = ReplaceEntities(message, voiceNote.Caption);
                    break;
                case MessageUnsupported:
                    {
                        var usupported = Strings.UnsupportedMessage;
                        var entity = new TextEntity(0, Strings.UnsupportedMessage.Length, new TextEntityTypeItalic());

                        result = ReplaceEntities(message, new FormattedText(usupported, new[] { entity }));
                        break;
                    }

                case MessageVenue venue:
                    {
                        var venueText = $"{venue.Venue.Title}\n{venue.Venue.Address}";
                        var venueEntities = new TextEntity[]
                        {
                    new(0, venue.Venue.Title.Length, new TextEntityTypeBold())
                        };

                        result = ReplaceEntities(message, venueText, venueEntities);
                        break;
                    }

                case MessageBigEmoji bigEmoji:
                    //var paragraph = new Paragraph();
                    //paragraph.Inlines.Add(new Run { Text = bigEmoji.Text.Text, FontSize = 32 });

                    //Message.Blocks.Clear();
                    //Message.Blocks.Add(paragraph);
                    result = ReplaceEntities(message, bigEmoji.Text, 32);
                    break;
            }

            ContentPanel.MaxWidth = Message.HasCodeBlocks ? double.PositiveInfinity : 432;
            Message.Visibility = result ? Visibility.Visible : Visibility.Collapsed;
            //Footer.HorizontalAlignment = adjust ? HorizontalAlignment.Left : HorizontalAlignment.Right;

            Summary?.Visibility = Visibility.Collapsed;
        }

        private bool GetEntities(MessageViewModel message, string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                //Message.Visibility = Visibility.Collapsed;
                return false;
            }
            else
            {
                //Message.Visibility = Visibility.Visible;
                return ReplaceEntities(message, text, ClientEx.GetTextEntities(text));
            }
        }

        private bool ReplaceEntities(MessageViewModel message, FormattedText text, double fontSize = 0)
        {
            if (text == null)
            {
                return false;
            }

            return ReplaceEntities(message, text.Text, text.Entities, fontSize);
        }

        private bool ReplaceEntities(MessageViewModel message, string text, IList<TextEntity> entities, double fontSize = 0)
        {
            // TODO: this crashes due to an internal framework exception
            //Message.IsTextSelectionEnabled = !message.Chat.HasProtectedContent;
            Message.ShowHideSkeleton(false);
            Message.SetText(message.ClientService, text, entities, fontSize);
            Message.SetQuery(_query);

            ContentPanel.MaxWidth = Message.HasCodeBlocks ? double.PositiveInfinity : 432;

            return text.Length > 0;
        }

        private Run CreateRun(string text, FontWeight? fontWeight = null, FontFamily fontFamily = null)
        {
            var direct = XamlDirect.GetDefault();
            var run = direct.CreateInstance(XamlTypeIndex.Run);
            direct.SetStringProperty(run, XamlPropertyIndex.Run_Text, text);

            if (fontWeight != null)
            {
                direct.SetObjectProperty(run, XamlPropertyIndex.TextElement_FontWeight, fontWeight.Value);
            }

            if (fontFamily != null)
            {
                direct.SetObjectProperty(run, XamlPropertyIndex.TextElement_FontFamily, fontFamily);
            }

            return direct.GetObject(run) as Run;
        }

        private Brush GetBrush(string key)
        {
            var message = _message;
            if (message != null && message.IsOutgoing && !message.IsChannelPost)
            {
                if (ActualTheme == ElementTheme.Light)
                {
                    return ThemeOutgoing.Light[key].Brush;
                }
                else
                {
                    return ThemeOutgoing.Dark[key].Brush;
                }
            }
            else if (ActualTheme == ElementTheme.Light)
            {
                return ThemeIncoming.Light[key].Brush;
            }
            else
            {
                return ThemeIncoming.Dark[key].Brush;
            }
        }

        private void Message_TextEntityClick(object sender, TextEntityClickEventArgs e)
        {
            if (_message is not MessageViewModel message || message.Delegate == null)
            {
                return;
            }

            TextEntityClick(message, sender as FormattedTextBlock, e);
        }

        public static void TextEntityClick(MessageViewModel message, FormattedTextBlock textBlock, TextEntityClickEventArgs e)
        {
            void OpenUrl(string url, bool trust)
            {
                if (message.Content is MessageText text && text.LinkPreview?.InstantViewVersion != 0 && MessageHelper.AreTheSame(text.LinkPreview?.Url, url, out _))
                {
                    message.Delegate.OpenWebPage(message);
                }
                else
                {
                    message.Delegate.OpenUrl(url, trust, new OpenUrlSourceChat(message.ChatId, message.SenderId));
                }
            }

            if (e.Type is TextEntityTypeTextUrl textUrl)
            {
                OpenUrl(textUrl.Url, true);
            }
            else if (e.Type is TextEntityTypeUrl && e.Text is string url)
            {
                OpenUrl(url, false);
            }
            if (e.Type is TextEntityTypeBotCommand && e.Text is string command)
            {
                message.Delegate.SendBotCommand(command);
            }
            else if (e.Type is TextEntityTypeEmailAddress)
            {
                message.Delegate.OpenUrl("mailto:" + e.Text, false);
            }
            else if (e.Type is TextEntityTypePhoneNumber)
            {
                message.Delegate.OpenUrl("tel:" + e.Text, false);
            }
            else if (e.Type is TextEntityTypeHashtag or TextEntityTypeCashtag && e.Text is string hashtag)
            {
                message.Delegate.OpenHashtag(hashtag);
            }
            else if (e.Type is TextEntityTypeMention && e.Text is string username)
            {
                message.Delegate.OpenUsername(username);
            }
            else if (e.Type is TextEntityTypeMentionName mentionName)
            {
                message.Delegate.OpenUser(mentionName.UserId);
            }
            else if (e.Type is TextEntityTypeBankCardNumber && e.Text is string cardNumber)
            {
                message.Delegate.OpenBankCardNumber(cardNumber);
            }
            else if (e.Type is TextEntityTypeMediaTimestamp mediaTimestamp)
            {
                var target = message.HasTimestampedMedia ? message : message.ReplyToItem;
                if (target is MessageViewModel targetMessage)
                {
                    if (targetMessage.Content is MessageText text && text.LinkPreview != null)
                    {
                        var regex = new Regex("^.*(?:(?:youtu\\.be\\/|v\\/|vi\\/|u\\/\\w\\/|embed\\/|shorts\\/)|(?:(?:watch)?\\?v(?:i)?=|\\&v(?:i)?=))([^#\\&\\?]*).*");

                        var match = regex.Match(text.LinkPreview.Url);
                        if (match.Success && match.Groups.Count == 2)
                        {
                            message.Delegate.OpenUrl($"https://youtu.be/{match.Groups[1].Value}?t={mediaTimestamp.MediaTimestamp}", false);
                        }
                        else
                        {
                            message.Delegate.OpenUrl(text.LinkPreview.Url, false);
                        }
                    }
                    else
                    {
                        message.Delegate.OpenMedia(targetMessage, null, mediaTimestamp.MediaTimestamp);
                    }
                }
                else
                {
                    // TODO
                }
            }
        }

        private string _currentState = "Normal";

        private void FooterToLightMedia(bool isOut)
        {
            var state = "LightState" + (isOut ? "Out" : string.Empty);
            if (state != _currentState)
            {
                _currentState = state;
                VisualStateManager.GoToState(this, state, false);
            }

            BackgroundPanel?.Visibility = Visibility.Collapsed;
        }

        private void FooterToMedia()
        {
            if (_currentState != "MediaState")
            {
                _currentState = "MediaState";
                VisualStateManager.GoToState(this, "MediaState", false);
            }
        }

        private void FooterToHidden()
        {
            if (_currentState != "HiddenState")
            {
                _currentState = "HiddenState";
                VisualStateManager.GoToState(this, "HiddenState", false);
            }
        }

        private void FooterToNormal()
        {
            if (_currentState != "Normal")
            {
                _currentState = "Normal";
                VisualStateManager.GoToState(this, "Normal", false);
            }
        }

        public void RegisterEvents()
        {
            _ignoreSizeChanged = false;
        }

        public void UnregisterEvents()
        {
            _ignoreSizeChanged = true;
        }

        public void AnimateSendout(float xTranslate, float xScale, float yScale, float fontScale, double outer, double inner, double delay, bool reply)
        {
            if (!_templateApplied)
            {
                return;
            }

            var content = _message?.GeneratedContent ?? _message?.Content;
            var panel = ElementComposition.GetElementVisual(ContentPanel);

            if (content is MessageText)
            {
                var crossScale = BootStrapper.Current.Compositor.CreateVector3KeyFrameAnimation();
                crossScale.InsertKeyFrame(0, new Vector3(1, yScale, 1));
                crossScale.InsertKeyFrame(1, new Vector3(1));
                crossScale.Duration = TimeSpan.FromMilliseconds(outer);
                crossScale.DelayTime = TimeSpan.FromMilliseconds(delay);
                crossScale.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;

                var outOpacity = BootStrapper.Current.Compositor.CreateScalarKeyFrameAnimation();
                outOpacity.InsertKeyFrame(0, 1);
                outOpacity.InsertKeyFrame(1, 0);
                outOpacity.Duration = TimeSpan.FromMilliseconds(inner);
                outOpacity.DelayTime = TimeSpan.FromMilliseconds(delay);
                outOpacity.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;

                if (BackgroundPanel == null)
                {
                    BackgroundPanel = GetTemplateChild(nameof(BackgroundPanel)) as Border;
                    CrossPanel = GetTemplateChild(nameof(CrossPanel)) as Border;
                }

                var cross = ElementComposition.GetElementVisual(CrossPanel);
                cross.StartAnimation("Opacity", outOpacity);

                var background = ElementComposition.GetElementVisual(BackgroundPanel);
                background.CenterPoint = new Vector3(0, reply ? 0 : ContentPanel.ActualSize.Y / 2, 0);
                background.StartAnimation("Scale", crossScale);
            }

            var header = ElementComposition.GetElementVisual(Header);
            var text = ElementComposition.GetElementVisual(Message);
            var media = ElementComposition.GetElementVisual(Media);
            var footer = ElementComposition.GetElementVisual(Footer);

            var scale = BootStrapper.Current.Compositor.CreateVector3KeyFrameAnimation();
            scale.InsertKeyFrame(0, new Vector3(xScale, 1, 1));
            scale.InsertKeyFrame(1, new Vector3(1));
            scale.Duration = TimeSpan.FromMilliseconds(inner);
            scale.DelayTime = TimeSpan.FromMilliseconds(delay);
            scale.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;

            var factor = BootStrapper.Current.Compositor.CreateExpressionAnimation("Vector3(1 / content.Scale.X, 1, 1)");
            factor.SetReferenceParameter("content", panel);

            CompositionAnimation textScale = factor;
            if (fontScale != 1)
            {
                var textScaleImpl = BootStrapper.Current.Compositor.CreateScalarKeyFrameAnimation();
                textScaleImpl.InsertKeyFrame(0, fontScale);
                textScaleImpl.InsertKeyFrame(1, 1);
                textScaleImpl.Duration = TimeSpan.FromMilliseconds(outer);
                textScaleImpl.DelayTime = TimeSpan.FromMilliseconds(delay);
                textScaleImpl.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;

                textScale = BootStrapper.Current.Compositor.CreateExpressionAnimation("Vector3(this.Scale * (1 / content.Scale.X), this.Scale, 1)");
                textScale.SetReferenceParameter("content", panel);
                textScale.Properties.InsertScalar("Scale", fontScale);
                textScale.Properties.StartAnimation("Scale", textScaleImpl);

                Message.Tag = textScaleImpl;
                Media.Tag = textScale;
            }

            var inOpacity = BootStrapper.Current.Compositor.CreateScalarKeyFrameAnimation();
            inOpacity.InsertKeyFrame(0, 0);
            inOpacity.InsertKeyFrame(1, 1);
            inOpacity.Duration = TimeSpan.FromMilliseconds(outer / 3 * 2);
            inOpacity.DelayTime = TimeSpan.FromMilliseconds(outer / 3);
            inOpacity.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;

            var headerLeft = (float)Header.Margin.Left;
            var textLeft = (float)Message.Margin.Left;

            var mediaLeft = (float)Media.Margin.Left;
            var mediaBottom = (float)Media.Margin.Bottom;

            var footerRight = (float)Footer.Margin.Right;
            var footerBottom = (float)Footer.Margin.Bottom;

            header.CenterPoint = new Vector3(-headerLeft, 0, 0);
            text.CenterPoint = new Vector3(-textLeft, Message.ActualSize.Y, 0);
            media.CenterPoint = new Vector3(-mediaLeft, Media.ActualSize.Y + mediaBottom, 0);
            footer.CenterPoint = new Vector3(Footer.ActualSize.X + footerRight, Footer.ActualSize.Y + footerBottom, 0);

            header.StartAnimation("Scale", factor);
            text.StartAnimation("Scale", textScale);
            media.StartAnimation("Scale", textScale);
            footer.StartAnimation("Scale", factor);
            footer.StartAnimation("Opacity", inOpacity);

            var headerOffsetX = content is MessageText ? 10 : 14;
            var headerOffsetY = 0f;

            var textOffsetX = 0f;
            var textOffsetY = 0f;

            if (content is MessageSticker or MessageAnimatedEmoji or MessageDice or MessageStakeDice)
            {
                headerOffsetY = reply ? 46 : 0;
                textOffsetX = ContentPanel.ActualSize.X - Media.ActualSize.X; // - 10;
            }
            if (content is MessageBigEmoji)
            {
                headerOffsetY = reply ? -36 : 0;
                textOffsetX = ContentPanel.ActualSize.X - Message.ActualSize.X; //- 10;
            }
            else if (content is MessageText)
            {
                textOffsetY = reply ? 16 : 0;
            }

            var headerOffset = BootStrapper.Current.Compositor.CreateVector3KeyFrameAnimation();
            headerOffset.InsertKeyFrame(0, new Vector3(-(headerOffsetX * (1 / xScale)), headerOffsetY, 0));
            headerOffset.InsertKeyFrame(1, new Vector3(0));
            headerOffset.Duration = TimeSpan.FromMilliseconds(headerOffsetY > 0 ? outer : inner);
            headerOffset.DelayTime = TimeSpan.FromMilliseconds(delay);
            headerOffset.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;
            header.StartAnimation("Translation", headerOffset);

            var textOffset = BootStrapper.Current.Compositor.CreateVector3KeyFrameAnimation();
            textOffset.InsertKeyFrame(0, new Vector3(-textOffsetX, textOffsetY, 0));
            textOffset.InsertKeyFrame(1, new Vector3());
            textOffset.Duration = TimeSpan.FromMilliseconds(textOffsetY > 0 ? outer : inner);
            textOffset.DelayTime = TimeSpan.FromMilliseconds(delay);
            textOffset.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;

            if (content is MessageSticker or MessageAnimatedEmoji or MessageDice or MessageStakeDice)
            {
                media.StartAnimation("Translation", textOffset);
            }
            else
            {
                text.StartAnimation("Translation", textOffset);
            }

            var offset = BootStrapper.Current.Compositor.CreateScalarKeyFrameAnimation();
            offset.InsertKeyFrame(0, -xTranslate);
            offset.InsertKeyFrame(1, 0);
            offset.Duration = TimeSpan.FromMilliseconds(textOffsetY > 0 ? outer : inner);
            offset.DelayTime = TimeSpan.FromMilliseconds(delay);
            offset.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;

            ElementCompositionPreview.SetIsTranslationEnabled(ContentPanel, true);

            panel.CenterPoint = new Vector3(ContentPanel.ActualSize, 0);
            panel.StartAnimation("Scale", scale);
            panel.StartAnimation("Translation.X", offset);
        }

        // TODO: this method seems to work in many cases but I'm not sure it's correct
        private int ComputeDirection()
        {
            var selector = this.GetParent<ChatHistoryViewItem>();
            if (selector == null)
            {
                return 1;
            }

            var panel = selector.Owner.ItemsPanelRoot as ItemsStackPanel;
            if (panel == null)
            {
                return 1;
            }

            var index = selector.Owner.IndexFromContainer(selector);

            var direction = panel.ItemsUpdatingScrollMode == ItemsUpdatingScrollMode.KeepItemsInView ? -1 : 1;
            var edge = (index == panel.LastVisibleIndex && direction == 1) || (index == panel.FirstVisibleIndex && direction == -1);

            if (edge && !selector.Owner.ScrollingHost.ViewportContains(selector))
            {
                direction *= -1;
            }

            var first = direction == 1 ? panel.FirstCacheIndex : index + 1;
            var last = direction == 1 ? index : panel.LastCacheIndex;

            if (index < first || index > last)
            {
                direction *= -1;
            }

            return direction;
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            var message = _message;
            if (message == null || _ignoreSizeChanged || e.PreviousSize.Width < 1 || e.PreviousSize.Height < 1)
            {
                return;
            }

            var content = message.GeneratedContent ?? message.Content;
            if (content is MessageSticker or MessageAnimatedEmoji or MessageDice or MessageStakeDice or MessageVideoNote or MessageBigEmoji)
            {
                return;
            }

            var prev = e.PreviousSize.ToVector2();
            var next = e.NewSize.ToVector2();
            var direction = ComputeDirection();

            var batch = BootStrapper.Current.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);

            var anim = BootStrapper.Current.Compositor.CreateVector3KeyFrameAnimation();
            anim.InsertKeyFrame(0, new Vector3(prev / next, 1));
            anim.InsertKeyFrame(1, Vector3.One);
            //anim.Duration = TimeSpan.FromSeconds(5);

            var panel = ElementComposition.GetElementVisual(ContentPanel);
            panel.CenterPoint = new Vector3(message.IsVisuallyOutgoing ? next.X : 0, direction < 0 ? next.Y : 0, 0);
            panel.StartAnimation("Scale", anim);

            var factor = BootStrapper.Current.Compositor.CreateExpressionAnimation("Vector3(1 / content.Scale.X, 1 / content.Scale.Y, 1)");
            factor.SetReferenceParameter("content", panel);

            var bottomOffset = anim.Compositor.CreateScalarKeyFrameAnimation();
            bottomOffset.InsertKeyFrame(0, prev.Y - next.Y);
            bottomOffset.InsertKeyFrame(1, 0);
            //bottomOffset.Duration = TimeSpan.FromSeconds(5);

            var header = ElementComposition.GetElementVisual(Header);
            var text = ElementComposition.GetElementVisual(Panel);
            var footer = ElementComposition.GetElementVisual(Footer);

            var headerLeft = (float)Header.Margin.Left;
            var textLeft = (float)Panel.Margin.Left;

            header.CenterPoint = new Vector3(-headerLeft, -Header.ActualOffset.Y, 0);
            text.CenterPoint = new Vector3(-textLeft, -Panel.ActualOffset.Y, 0);

            header.StartAnimation("Scale", factor);
            text.StartAnimation("Scale", factor);

            {
                var footerOffset = anim.Compositor.CreateVector3KeyFrameAnimation();
                footerOffset.InsertKeyFrame(0, new Vector3(prev - next, 0));
                footerOffset.InsertKeyFrame(1, Vector3.Zero);
                //footerOffset.Duration = TimeSpan.FromSeconds(5);

                ElementCompositionPreview.SetIsTranslationEnabled(Footer, true);
                footer.StartAnimation("Translation", footerOffset);
            }

            if (Media.Child != null && _currentState == "MediaState")
            {
                AnimateBottom(Media, bottomOffset);
            }

            if (Thread != null)
            {
                var thread = ElementComposition.GetElementVisual(Thread);
                thread.CenterPoint = new Vector3(0, Thread.ActualSize.Y, 0);
                thread.StartAnimation("Scale", factor);
            }

            if (Reactions != null)
            {
                AnimateBottom(Reactions, bottomOffset);
            }

            if (Panel.Children.Count > 0 && Panel.Children[0] is MessageFactCheck or MessageSummary)
            {
                AnimateBottom(Panel.Children[0], bottomOffset);
            }

            if (Photo != null && _photoId.HasValue)
            {
                AnimateBottom(Photo, bottomOffset);
            }

            if (Action?.Visibility == Visibility.Visible && direction == 1)
            {
                AnimateBottom(Action, bottomOffset);
            }

            batch.End();

            static void AnimateBottom(UIElement element, CompositionAnimation animation)
            {
                ElementCompositionPreview.SetIsTranslationEnabled(element, true);

                var photo = ElementComposition.GetElementVisual(element);
                photo.StartAnimation("Translation.Y", animation);
            }
        }

        private ContainerVisual _highlight;

        public Rect Highlight(MessageBubbleHighlightOptions options)
        {
            var message = _message;
            if (message == null)
            {
                return new Rect(0, 0, ActualWidth, ActualHeight);
            }

            _highlight = BootStrapper.Current.Compositor.CreateContainerVisual();

            var content = message.GeneratedContent ?? message.Content;
            var light = content is MessageSticker
                or MessageDice
                or MessageStakeDice
                or MessageVideoNote
                or MessageBigEmoji
                or MessageAnimatedEmoji;

            FrameworkElement target;
            if (light)
            {
                ElementCompositionPreview.SetElementChildVisual(ContentPanel, null);
                ElementCompositionPreview.SetElementChildVisual(Media, _highlight);
                target = Media;
            }
            else
            {
                ElementCompositionPreview.SetElementChildVisual(Media, null);
                ElementCompositionPreview.SetElementChildVisual(ContentPanel, _highlight);
                target = ContentPanel;
            }

            CompositionBrush brush = null;
            if (Media.Child is IContentWithMask withMask)
            {
                var alpha = withMask.GetAlphaMask();
                if (alpha != null)
                {
                    var mask = _highlight.Compositor.CreateMaskBrush();
                    mask.Source = brush;
                    mask.Mask = alpha;

                    brush = mask;
                }
            }

            brush ??= _highlight.Compositor.CreateColorBrush(ActualTheme == ElementTheme.Light
                ? Theme.AccentLight.Default
                : Theme.AccentDark.Default);

            var solid = BootStrapper.Current.Compositor.CreateSpriteVisual();
            solid.Size = target.ActualSize;
            solid.Opacity = 0f;
            solid.Brush = brush;

            _highlight.Children.RemoveAll();
            _highlight.Children.InsertAtTop(solid);
            _highlight.Size = target.ActualSize;

            if (options.Quote != null && options.Quote.IsManual && !string.IsNullOrEmpty(message.Text?.Text))
            {
                var caption = content.GetCaption();
                var index = ClientEx.SearchQuote(caption, options.Quote);
                if (index >= 0)
                {
                    var fontSize = Theme.Current.MessageFontSize * BootStrapper.Current.TextScaleFactor;
                    var quoteSize = Theme.Current.CaptionFontSize * BootStrapper.Current.TextScaleFactor;

                    var minX = double.MaxValue;
                    var minY = double.MaxValue;
                    var maxX = double.MinValue;
                    var maxY = double.MinValue;

                    var shapes = new List<IList<Rect>>();
                    var current = new List<Rect>();
                    var last = default(Rect);

                    var visual = BootStrapper.Current.Compositor.CreateShapeVisual();
                    visual.Size = target.ActualSize;
                    visual.Opacity = 0;

                    var transform = Message.TransformToVisual(ContentPanel);
                    var position = transform.TransformPoint(new Windows.Foundation.Point());

                    for (int j = 0; j < message.Text.Paragraphs.Count; j++)
                    {
                        StyledParagraph styled = message.Text.Paragraphs[j];
                        Paragraph paragraph = Message.GetBlock(j, out double width, out Point adjustment) as Paragraph;

                        if (!TextStyleRun.GetRelativeRange(index, options.Quote.Text.Text.Length, styled.Offset, styled.Length, out int xoffset, out int xlength))
                        {
                            continue;
                        }

                        var partial = message.Text.Text.Substring(styled.Offset, styled.Length);
                        var entities = styled.Parts ?? Array.Empty<TextStylePart>();

                        var size = styled.Type is TextParagraphTypeQuote
                            ? quoteSize
                            : fontSize;

                        var rectangles = PlaceholderHelper.Foreground.RangeMetrics(partial, xoffset, xlength, entities, size, width - paragraph.Margin.Left - paragraph.Margin.Right, styled.Direction == TextDirectionality.RightToLeft, true);
                        var relative = paragraph.ContentStart.GetCharacterRect(paragraph.ContentStart.LogicalDirection);

                        var point = new Windows.Foundation.Point(paragraph.Margin.Left + position.X + adjustment.X, relative.Y + position.Y + adjustment.Y);

                        for (int i = 0; i < rectangles.Count; i++)
                        {
                            var rect = rectangles[i];
                            rect = new Rect(rect.X - 2, rect.Y, rect.Width + 4, rect.Height);
                            rect.X += point.X;
                            rect.Y += point.Y;

                            if (current.Count > 0 && !rect.IntersectsOrTouches(last))
                            {
                                shapes.Add(current);
                                current = new List<Rect>();
                            }

                            current.Add(rect);
                            last = rect;

                            minX = Math.Min(minX, rect.Left);
                            minY = Math.Min(minY, rect.Top);
                            maxX = Math.Max(maxX, rect.Right);
                            maxY = Math.Max(maxY, rect.Bottom);
                        }
                    }

                    if (current.Count > 0)
                    {
                        shapes.Add(current);
                    }

                    var shape = BootStrapper.Current.Compositor.CreateSpriteShape(BootStrapper.Current.Compositor.CreatePathGeometry(PlaceholderHelper.Foreground.GetRoundedPolygon(shapes)));
                    shape.FillBrush = brush;
                    shape.StrokeThickness = 0;
                    visual.Shapes.Add(shape);

                    var wwidth = (float)(maxX - minX);
                    var hheight = (float)(maxY - minY);

                    solid.Scale = new Vector3(wwidth / target.ActualSize.X, hheight / target.ActualSize.Y, 0);
                    solid.CenterPoint = new Vector3(new Windows.Foundation.Point(maxX - (wwidth / 2), maxY - (hheight / 2)).ToVector2(), 0);
                    solid.CenterPoint = new Vector3(new Windows.Foundation.Point(minX + 16, minY + 8).ToVector2(), 0);

                    if (ApiInfo.CanCreateRectangleClip)
                    {
                        solid.Clip = BootStrapper.Current.Compositor.CreateRectangleClip(0, 0, (float)target.ActualWidth, (float)target.ActualHeight, new Vector2(_topLeft), new Vector2(_topRight), new Vector2(_bottomRight), new Vector2(_bottomLeft));
                    }

                    _highlight.Children.InsertAtTop(visual);

                    var scale = _highlight.Compositor.CreateVector3KeyFrameAnimation();
                    scale.Duration = TimeSpan.FromSeconds(2);
                    scale.InsertKeyFrame(0, new Vector3(1));
                    scale.InsertKeyFrame(300f / 2000f, new Vector3(1));
                    scale.InsertKeyFrame(700f / 2000f, new Vector3(wwidth / target.ActualSize.X, hheight / target.ActualSize.Y, 0));

                    solid.StartAnimation("Scale", scale);

                    var opacity1 = _highlight.Compositor.CreateScalarKeyFrameAnimation();
                    opacity1.Duration = TimeSpan.FromSeconds(2);
                    opacity1.InsertKeyFrame(300f / 2000f, 0.4f);
                    opacity1.InsertKeyFrame(700f / 2000f, 0.0f);
                    opacity1.InsertKeyFrame(1, 0);

                    var opacity2 = _highlight.Compositor.CreateScalarKeyFrameAnimation();
                    opacity2.Duration = TimeSpan.FromSeconds(2);
                    opacity2.InsertKeyFrame(300f / 2000f, 0.0f);
                    opacity2.InsertKeyFrame(700f / 2000f, 0.4f);
                    opacity2.InsertKeyFrame(1700f / 2000f, 0.4f);
                    opacity2.InsertKeyFrame(1, 0);

                    solid.StartAnimation("Opacity", opacity1);
                    visual.StartAnimation("Opacity", opacity2);

                    return new Rect(minX, minY, maxX - minX, maxY - minY);
                }
            }

            if (Media.Child is AlbumContent album)
            {
                var area = album.Highlight(options);
                if (!area.IsEmpty)
                {
                    var point = Media.TransformToVector2(ContentPanel);
                    var offset = area.ToOffset();
                    solid.Offset = new Vector3(offset.X, point.Y + offset.Y, 0);
                    solid.Size = area.ToSizeF();
                }
            }
            else if (Media.Child is ChecklistContent checklist && options.ChecklistTaskId != 0)
            {
                var area = checklist.Highlight(options);
                if (!area.IsEmpty)
                {
                    var point = Media.TransformToVector2(ContentPanel);
                    var offset = area.ToOffset();
                    solid.Offset = new Vector3(offset.X, point.Y + offset.Y, 0);
                    solid.Size = area.ToSizeF();
                }
            }
            else if (Media.Child is PollContent poll && !string.IsNullOrEmpty(options.PollOptionId))
            {
                var area = poll.Highlight(options);
                if (!area.IsEmpty)
                {
                    var point = Media.TransformToVector2(ContentPanel);
                    var offset = area.ToOffset();
                    solid.Offset = new Vector3(offset.X, point.Y + offset.Y, 0);
                    solid.Size = area.ToSizeF();
                }
            }

            var animation = _highlight.Compositor.CreateScalarKeyFrameAnimation();
            animation.Duration = TimeSpan.FromSeconds(2);
            animation.InsertKeyFrame(300f / 2000f, 0.4f);
            animation.InsertKeyFrame(1700f / 2000f, 0.4f);
            animation.InsertKeyFrame(1, 0);

            solid.StartAnimation("Opacity", animation);

            return new Rect(0, 0, ActualWidth, ActualHeight);
        }

        #region Actions

        private void PsaInfo_Click(object sender, RoutedEventArgs e)
        {
            var message = _message;
            if (message == null)
            {
                return;
            }

            var type = LocaleService.Current.GetString("PsaMessageInfo_" + message.ForwardInfo.PublicServiceAnnouncementType);
            if (string.IsNullOrEmpty(type))
            {
                type = Strings.PsaMessageInfoDefault;
            }

            var entities = ClientEx.GetTextEntities(type);
            ToastPopup.Show(PsaInfo, new FormattedText(type, entities), TeachingTipPlacementMode.TopLeft);
        }

        private void Thread_Click(object sender, RoutedEventArgs e)
        {
            var message = _message;
            if (message?.Delegate == null)
            {
                return;
            }

            message.Delegate.OpenThread(message);
        }

        private void Reply_Click(object sender, RoutedEventArgs e)
        {
            var message = _message;
            if (message?.Delegate == null)
            {
                return;
            }

            if (message.ReplyTo is MessageReplyToStory)
            {
                if (message.ReplyToState == MessageReplyToState.Deleted)
                {
                    ToastPopup.Show(XamlRoot, Strings.StoryNotFound, ToastPopupIcon.ExpiredStory);
                }
                else if (message.ReplyToItem is Story item)
                {
                    OpenStory(message, item);
                }
            }
            else
            {
                message.Delegate.OpenReply(message);
            }
        }

        public void OpenStory(MessageViewModel message, Story story)
        {
            var activeStories = new ActiveStoriesViewModel(message.ClientService, message.Delegate.Settings, message.Delegate.Aggregator, story);
            var viewModel = StoryListViewModel.Create(message.Delegate.NavigationService, activeStories);

            var origin = GetStoryOrigin(null);

            var window = new StoriesWindow();
            window.Update(viewModel, activeStories, StoryOpenOrigin.Card, origin, GetStoryOrigin);
            _ = window.ShowAsync(XamlRoot);
        }

        private Rect GetStoryOrigin(ActiveStoriesViewModel activeStories)
        {
            var transform = Reply.TransformToVisual(null);
            var point = transform.TransformPoint(new Windows.Foundation.Point());

            return new Rect(point.X + 10, point.Y + 4, 36, 36);
        }

        private void ReplyMarkup_ButtonClick(object sender, ReplyMarkupInlineButtonClickEventArgs e)
        {
            var message = _message;
            if (message == null)
            {
                return;
            }

            message.Delegate?.OpenInlineButton(message, e.Button);
        }

        #endregion

        public void Mockup(string message, bool outgoing, DateTime date, bool first = true, bool last = true)
        {
            if (!_templateApplied)
            {
                void loaded(object o, EventArgs e)
                {
                    TemplateApplied -= loaded;
                    Mockup(message, outgoing, date, first, last);
                }

                TemplateApplied += loaded;
                return;
            }

            Header.Visibility = Visibility.Collapsed;

            Footer.Mockup(outgoing, date);
            Panel.ForceNewLine = false;

            Media.Margin = new Thickness(0);
            FooterToNormal();
            Grid.SetRow(Footer, 2);
            Grid.SetRow(Message, 2);
            Panel.Placeholder = true;

            Message.SetText(null, message, Array.Empty<TextEntity>());

            UpdateMockup(outgoing, first, last);
        }

        public void Mockup(string message, string sender, string reply, bool outgoing, DateTime date, bool first = true, bool last = true)
        {
            if (!_templateApplied)
            {
                void loaded(object o, EventArgs e)
                {
                    TemplateApplied -= loaded;
                    Mockup(message, sender, reply, outgoing, date, first, last);
                }

                TemplateApplied += loaded;
                return;
            }

            Header.Visibility = Visibility.Visible;

            if (Reply == null)
            {
                void layoutUpdated(object o, object e)
                {
                    Reply.LayoutUpdated -= layoutUpdated;
                    Reply.Mockup(sender, reply);
                }

                Reply = GetTemplateChild(nameof(Reply)) as MessageReply;
                Reply.LayoutUpdated += layoutUpdated;

                Panel.Reply = Reply;
            }
            else
            {
                Reply.Visibility = Visibility.Visible;
                Reply.Mockup(sender, reply);
            }

            Footer.Mockup(outgoing, date);
            Panel.ForceNewLine = false;

            Media.Margin = new Thickness(0);
            FooterToNormal();
            Grid.SetRow(Footer, 2);
            Grid.SetRow(Message, 2);
            Panel.Placeholder = true;

            Message.SetText(null, message, Array.Empty<TextEntity>());

            UpdateMockup(outgoing, first, last);
        }

        public void Mockup(IClientService clientService, string message, object sender, string reply, bool outgoing, DateTime date, bool first = true, bool last = true)
        {
            if (!_templateApplied)
            {
                void loaded(object o, EventArgs e)
                {
                    TemplateApplied -= loaded;
                    Mockup(clientService, message, sender, reply, outgoing, date, first, last);
                }

                TemplateApplied += loaded;
                return;
            }

            Header.Visibility = Visibility.Visible;

            var title = sender switch
            {
                User u => u.FullName(),
                Chat c => c.Title,
                _ => null
            };

            if (Reply == null)
            {
                void layoutUpdated(object o, object e)
                {
                    Reply.LayoutUpdated -= layoutUpdated;
                    Reply.Mockup(title, reply);

                    if (sender is User user)
                    {
                        Reply.UpdateMockup(clientService, user.BackgroundCustomEmojiId, user.AccentColorId, user.UpgradedGiftColors);
                    }
                    else if (sender is Chat chat)
                    {
                        Reply.UpdateMockup(clientService, chat.BackgroundCustomEmojiId, chat.AccentColorId, chat.UpgradedGiftColors);
                    }
                }

                Reply = GetTemplateChild(nameof(Reply)) as MessageReply;
                Reply.LayoutUpdated += layoutUpdated;

                Panel.Reply = Reply;
            }
            else
            {
                Reply.Visibility = Visibility.Visible;
                Reply.Mockup(title, reply);

                if (sender is User user)
                {
                    Reply.UpdateMockup(clientService, user.BackgroundCustomEmojiId, user.AccentColorId, user.UpgradedGiftColors);
                }
                else if (sender is Chat chat)
                {
                    Reply.UpdateMockup(clientService, chat.BackgroundCustomEmojiId, chat.AccentColorId, chat.UpgradedGiftColors);
                }
            }

            Footer.Mockup(outgoing, date);
            Panel.ForceNewLine = false;

            Media.Margin = new Thickness(0);
            FooterToNormal();
            Grid.SetRow(Footer, 2);
            Grid.SetRow(Message, 2);
            Panel.Placeholder = true;

            Message.SetText(null, message, Array.Empty<TextEntity>());

            UpdateMockup(outgoing, first, last);
        }

        public void Mockup(IClientService clientService, string message, MessageSender sender, string reply, LinkPreview linkPreview, bool outgoing, DateTime date, bool first = true, bool last = true)
        {
            if (!_templateApplied)
            {
                void loaded(object o, EventArgs e)
                {
                    TemplateApplied -= loaded;
                    Mockup(clientService, message, sender, reply, linkPreview, outgoing, date, first, last);
                }

                TemplateApplied += loaded;
                return;
            }

            Header.Visibility = Visibility.Visible;

            var obj = clientService.GetMessageSender(sender);
            var title = obj switch
            {
                User u => u.FullName(),
                Chat c => c.Title,
                _ => null
            };

            if (Reply == null)
            {
                void layoutUpdated(object o, object e)
                {
                    Reply.LayoutUpdated -= layoutUpdated;
                    Reply.Mockup(title, reply);

                    if (obj is User user)
                    {
                        Reply.UpdateMockup(clientService, user.BackgroundCustomEmojiId, user.AccentColorId, user.UpgradedGiftColors);
                    }
                    else if (obj is Chat chat)
                    {
                        Reply.UpdateMockup(clientService, chat.BackgroundCustomEmojiId, chat.AccentColorId, chat.UpgradedGiftColors);
                    }
                }

                Reply = GetTemplateChild(nameof(Reply)) as MessageReply;
                Reply.LayoutUpdated += layoutUpdated;

                Panel.Reply = Reply;
            }
            else
            {
                Reply.Visibility = Visibility.Visible;
                Reply.Mockup(title, reply);
            }

            {
                var presenter = new WebPageContent();

                void layoutUpdated(object o, object e)
                {
                    presenter.LayoutUpdated -= layoutUpdated;
                    presenter.Mockup(clientService, linkPreview);

                    if (obj is User user)
                    {
                        presenter.UpdateMockup(clientService, user.BackgroundCustomEmojiId, user.AccentColorId, user.UpgradedGiftColors);
                    }
                    else if (obj is Chat chat)
                    {
                        presenter.UpdateMockup(clientService, chat.BackgroundCustomEmojiId, chat.AccentColorId, chat.UpgradedGiftColors);
                    }
                }

                presenter.LayoutUpdated += layoutUpdated;
                Media.Child = presenter;
            }

            Footer.Mockup(outgoing, date);
            Panel.ForceNewLine = false;

            ContentPanel.Padding = new Thickness(0, 4, 0, 0);
            Media.Margin = new Thickness(10, -6, 10, 0);
            FooterToNormal();
            Grid.SetRow(Footer, 4);
            Grid.SetRow(Message, 2);
            Panel.Placeholder = false;

            Message.SetText(null, message, Array.Empty<TextEntity>());

            LoadTemplateChild(ref HeaderPanel);
            LoadTemplateChild(ref HeaderLabel);

            var hyperlink = HeaderLabel.Inlines[0] as Hyperlink;
            var run = hyperlink.Inlines[0] as Run;
            run.Text = title;

            Header.Visibility = Visibility.Visible;
            HeaderPanel.Visibility = Visibility.Visible;
            HeaderLabel.Visibility = Visibility.Visible;

            if (PhotoRoot == null)
            {
                PhotoRoot = GetTemplateChild(nameof(PhotoRoot)) as HyperlinkButton;
                PhotoRoot.Click += Photo_Click;

                Photo = GetTemplateChild(nameof(Photo)) as ProfilePicture;
            }

            PhotoRoot.Visibility = Visibility.Visible;

            if (obj is User user)
            {
                Photo.Source = ProfilePictureSource.User(clientService, user);
            }
            else if (obj is Chat chat)
            {
                Photo.Source = ProfilePictureSource.Chat(clientService, chat);
            }

            PhotoColumn.Width = new GridLength(38, GridUnitType.Pixel);

            UpdateMockup(outgoing, first, last);
        }

        public void Mockup(MessageContent content, bool outgoing, DateTime date, bool first = true, bool last = true)
        {
            if (!_templateApplied)
            {
                void loaded(object o, EventArgs e)
                {
                    TemplateApplied -= loaded;
                    Mockup(content, outgoing, date, first, last);
                }

                TemplateApplied += loaded;
                return;
            }

            Header.Visibility = Visibility.Collapsed;
            Message.Visibility = Visibility.Collapsed;

            Footer.Mockup(outgoing, date);
            Panel.ForceNewLine = content is MessageBigEmoji;

            Media.Margin = new Thickness(10, 4, 10, 8);
            FooterToNormal();
            Grid.SetRow(Footer, 3);
            Grid.SetRow(Message, 2);
            Panel.Placeholder = false;

            if (content is MessageVoiceNote voiceNote)
            {
                var presenter = new VoiceNoteContent();

                void layoutUpdated(object o, object e)
                {
                    presenter.LayoutUpdated -= layoutUpdated;
                    presenter.Mockup(voiceNote);
                }

                presenter.LayoutUpdated += layoutUpdated;
                Media.Child = presenter;
            }
            else if (content is MessageAudio audio)
            {
                var presenter = new AudioContent();

                void layoutUpdated(object o, object e)
                {
                    presenter.LayoutUpdated -= layoutUpdated;
                    presenter.Mockup(audio);
                }

                presenter.LayoutUpdated += layoutUpdated;
                Media.Child = presenter;
            }

            Message.Clear();

            UpdateMockup(outgoing, first, last);
        }

        public void Mockup(MessageContent content, string caption, bool outgoing, DateTime date, bool first = true, bool last = true)
        {
            if (!_templateApplied)
            {
                void loaded(object o, EventArgs e)
                {
                    TemplateApplied -= loaded;
                    Mockup(content, caption, outgoing, date, first, last);
                }

                TemplateApplied += loaded;
                return;
            }

            Header.Visibility = Visibility.Collapsed;

            Footer.Mockup(outgoing, date);
            Panel.ForceNewLine = content is MessageBigEmoji;

            Media.Margin = new Thickness(0, 0, 0, 4);
            FooterToNormal();
            Grid.SetRow(Footer, 4);
            Grid.SetRow(Message, 4);
            Panel.Placeholder = true;

            if (content is MessagePhoto photo)
            {
                var presenter = new PhotoContent();

                void layoutUpdated(object o, object e)
                {
                    presenter.LayoutUpdated -= layoutUpdated;
                    presenter.Mockup(photo);
                }

                presenter.LayoutUpdated += layoutUpdated;
                Media.Child = presenter;
            }

            Message.SetText(null, caption, Array.Empty<TextEntity>());

            UpdateMockup(outgoing, first, last);
        }

        public void UpdateMockup(IClientService clientService, long customEmojiId, int color, UpgradedGiftColors upgradedGift)
        {
            if (!_templateApplied)
            {
                void loaded(object o, EventArgs e)
                {
                    TemplateApplied -= loaded;
                    UpdateMockup(clientService, customEmojiId, color, upgradedGift);
                }

                TemplateApplied += loaded;
                return;
            }

            if (Media.Child is WebPageContent webPageContent)
            {
                webPageContent.UpdateMockup(clientService, customEmojiId, color, upgradedGift);
            }

            Reply?.UpdateMockup(clientService, customEmojiId, color, upgradedGift);

            if (HeaderLabel?.Inlines.Count > 0 && HeaderLabel.Inlines[0] is Hyperlink hyperlink)
            {
                if (upgradedGift != null)
                {
                    hyperlink.Foreground = new SolidColorBrush(upgradedGift.LightThemeAccentColor.ToColor());
                }
                else
                {
                    hyperlink.Foreground = clientService.GetAccentBrush(color);
                }
            }
        }

        public void UpdateMockup(IClientService clientService, Chat chat, MessageSender sender, string tag, ChatMemberRank rank)
        {
            if (!_templateApplied)
            {
                void loaded(object o, EventArgs e)
                {
                    TemplateApplied -= loaded;
                    UpdateMockup(clientService, chat, sender, tag, rank);
                }

                TemplateApplied += loaded;
                return;
            }

            if (sender != null)
            {
                var message = new Message(chat.Id, sender, 0, null, null, false, false, false, false, false, false, false, false, false, false, 0, 0, null, null, null, Array.Empty<UnreadReaction>(), null, null, null, null, null, 0, 0, 0, null, 0, 0, string.Empty, 0, string.Empty, 0, 0, null, string.Empty, null, null);
                var settings = clientService.Session.Resolve<ISettingsService>();

                var delegato = new ChatMessageDelegate(clientService, settings, chat);
                var viewModel = new MessageViewModel(clientService, delegato, chat, null, null, message, true);

                UpdateMessageHeader(viewModel);
            }

            if (MemberTag == null)
            {
                LoadTemplateChild(ref MemberTag);
                MemberTag.Tapped += MemberTag_Tapped;
            }

            MemberTag.Text = tag;

            if (rank != ChatMemberRank.Other)
            {
                var color = rank == ChatMemberRank.Owner
                    ? Color.FromArgb(0xFF, 0x65, 0x60, 0xF6)
                    : Color.FromArgb(0xFF, 0x75, 0xC8, 0x73);

                MemberTag.Background = new SolidColorBrush(color) { Opacity = 0.2 };
                MemberTag.Foreground = new SolidColorBrush(color.Darken());
            }
            else
            {
                MemberTag.ClearValue(BackgroundProperty);
                MemberTag.ClearValue(ForegroundProperty);
            }

            Message.Visibility = Visibility.Collapsed;

            Footer.Mockup(false, DateTime.Now);
            Panel.ForceNewLine = false;

            var placeholder = new StackPanel
            {
                Orientation = Orientation.Vertical,
                //Width = 480,
                //Height = 48
            };

            var backgroundColor = Color.FromArgb(0x0F, 0xFF, 0xFF, 0xFF);

            var lookup = ThemeService.GetLookup(ActualTheme);
            if (lookup.TryGet("MenuFlyoutItemBackgroundPointerOver", out backgroundColor))
            {
            }

            var bar1 = new Border
            {
                CornerRadius = new CornerRadius(4),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Height = 8,
                Background = new SolidColorBrush(backgroundColor),
                Margin = new Thickness(0, 0, 4, 0)
            };

            var bar2 = new Border
            {
                CornerRadius = new CornerRadius(4),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Height = 8,
                Background = new SolidColorBrush(backgroundColor),
                Margin = new Thickness(0, 12, 36, 16)
            };

            placeholder.Children.Add(bar1);
            placeholder.Children.Add(bar2);

            Media.Child = placeholder;
            Media.Margin = new Thickness(10, 4, 10, 8);
            FooterToNormal();
            Grid.SetRow(Footer, 3);
            Grid.SetRow(Message, 2);
            Panel.Placeholder = false;

            if (sender != null)
            {
                if (PhotoRoot == null)
                {
                    PhotoRoot = GetTemplateChild(nameof(PhotoRoot)) as HyperlinkButton;
                    PhotoRoot.Click += Photo_Click;

                    Photo = GetTemplateChild(nameof(Photo)) as ProfilePicture;
                }

                PhotoRoot.Visibility = Visibility.Visible;
                Photo.Source = ProfilePictureSource.MessageSender(clientService, sender);

                if (PhotoColumn.Width.IsAuto)
                {
                    PhotoColumn.Width = new GridLength(38, GridUnitType.Pixel);
                }
            }

            UpdateMockup(false, true, true, false);
        }

        public void UpdateMockup(bool outgoing, bool first, bool last, bool margin = true)
        {
            var radius = SettingsService.Current.Appearance.BubbleRadius;
            var small = radius < 4 ? radius : 4;

            var topLeft = radius;
            var topRight = radius;
            var bottomRight = radius;
            var bottomLeft = radius;

            if (outgoing)
            {
                if (first && last)
                {
                    bottomRight = 0;
                }
                else if (first)
                {
                    bottomRight = small;
                }
                else if (last)
                {
                    topRight = small;
                    bottomRight = 0;
                }
                else
                {
                    topRight = small;
                    bottomRight = small;
                }
            }
            else
            {
                if (first && last)
                {
                    bottomLeft = 0;
                }
                else if (first)
                {
                    bottomLeft = small;
                }
                else if (last)
                {
                    topLeft = small;
                    bottomLeft = 0;
                }
                else
                {
                    topLeft = small;
                    bottomLeft = small;
                }
            }

            if (margin)
            {
                Margin = new Thickness(outgoing ? 50 : 12, first ? 2 : 1, outgoing ? 12 : 50, last ? 2 : 1);
            }

            Message.SetFontSize(Theme.Current.MessageFontSize);
            SetCorners(topLeft, topRight, bottomRight, bottomLeft);
        }





        protected override Size MeasureOverride(Size availableSize)
        {
            var availableWidth = Math.Min(availableSize.Width, Math.Min(double.IsNaN(Width) ? double.PositiveInfinity : Width, 420));
            var availableHeight = Math.Min(availableSize.Height, Math.Min(double.IsNaN(Height) ? double.PositiveInfinity : Height, 420));

            var ttl = false;
            var caption = false;
            var width = 0.0;
            var height = 0.0;

            var constraint = _message as object;
            if (constraint is MessageViewModel viewModel)
            {
                //ttl = viewModel.SelfDestructTime > 0;
                constraint = viewModel.GeneratedContent ?? viewModel.Content;
            }
            else if (constraint is Message message)
            {
                //ttl = message.SelfDestructTime > 0;
                constraint = message.Content;
            }

            if (constraint is MessagePoll poll)
            {
                constraint = poll.Media;
            }

            if (constraint is MessageAnimation animationMessage)
            {
                ttl = animationMessage.IsSecret;
                caption = animationMessage.Caption?.Text.Length > 0;
                constraint = animationMessage.Animation;
            }
            else if (constraint is MessageInvoice invoiceMessage)
            {
                if (invoiceMessage.PaidMedia is PaidMediaPhoto paidMediaPhoto)
                {
                    constraint = paidMediaPhoto.Photo;
                }
                else if (invoiceMessage.PaidMedia is PaidMediaVideo paidMediaVideo)
                {
                    constraint = paidMediaVideo.Video;
                }
                else if (invoiceMessage.PaidMedia is PaidMediaPreview paidMediaPreview)
                {
                    width = paidMediaPreview.Width;
                    height = paidMediaPreview.Height;

                    goto Calculate;
                }
                else
                {
                    constraint = invoiceMessage.ProductInfo.Photo;
                }
            }
            else if (constraint is MessageLocation locationMessage)
            {
                constraint = locationMessage.Location;
            }
            else if (constraint is MessagePhoto photoMessage)
            {
                ttl = photoMessage.IsSecret;
                caption = photoMessage.Caption?.Text.Length > 0;
                constraint = photoMessage.Photo;
            }
            else if (constraint is MessageSticker stickerMessage)
            {
                constraint = stickerMessage.Sticker;
            }
            else if (constraint is MessageAnimatedEmoji animatedEmojiMessage)
            {
                if (animatedEmojiMessage.AnimatedEmoji.Sticker != null)
                {
                    constraint = animatedEmojiMessage.AnimatedEmoji.Sticker;
                }
                else
                {
                    width = animatedEmojiMessage.AnimatedEmoji.StickerWidth;
                    height = animatedEmojiMessage.AnimatedEmoji.StickerHeight;
                }
            }
            else if (constraint is MessageAsyncStory storyMessage)
            {
                width = 720;
                height = 1280;

                goto Calculate;
            }
            else if (constraint is MessageVenue venueMessage)
            {
                constraint = venueMessage.Venue;
            }
            else if (constraint is MessageVideo videoMessage)
            {
                ttl = videoMessage.IsSecret;
                caption = videoMessage.Caption?.Text.Length > 0;
                constraint = videoMessage.Video;
            }
            else if (constraint is MessageVideoNote videoNoteMessage)
            {
                ttl = videoNoteMessage.IsSecret;
                constraint = videoNoteMessage.VideoNote;
            }
            else if (constraint is MessageVoiceNote voiceNoteMessage)
            {
                constraint = voiceNoteMessage.VoiceNote;
            }
            else if (constraint is MessageChatChangePhoto chatChangePhoto)
            {
                constraint = chatChangePhoto.Photo;
            }
            else if (constraint is MessageAlbum album)
            {
                if (album.Messages.Count == 1)
                {
                    if (album.Messages[0].Content is MessagePhoto photoContent)
                    {
                        constraint = photoContent.Photo;
                    }
                    else if (album.Messages[0].Content is MessageVideo videoContent)
                    {
                        constraint = videoContent.Video;
                    }
                }
                else if (album.IsMedia)
                {
                    var positions = album.GetPositionsForWidth(availableWidth, false);
                    width = positions.Item2.Width;
                    height = positions.Item2.Height;

                    goto Calculate;
                }
            }
            else if (constraint is MessagePaidAlbum paidAlbum)
            {
                if (paidAlbum.Media.Count == 1)
                {
                    if (paidAlbum.Media[0] is PaidMediaPhoto photoContent)
                    {
                        constraint = photoContent.Photo;
                    }
                    else if (paidAlbum.Media[0] is PaidMediaVideo videoContent)
                    {
                        constraint = videoContent.Video;
                    }
                    else if (paidAlbum.Media[0] is PaidMediaPreview paidMediaPreview)
                    {
                        width = paidMediaPreview.Width;
                        height = paidMediaPreview.Height;

                        goto Calculate;
                    }
                }
                else
                {
                    var positions = paidAlbum.GetPositionsForWidth(availableWidth, false);
                    width = positions.Item2.Width;
                    height = positions.Item2.Height;

                    goto Calculate;
                }
            }
            else if (constraint is PaidMediaPreview paidMediaPreview)
            {
                width = paidMediaPreview.Width;
                height = paidMediaPreview.Height;
            }

            if (constraint is Animation animation)
            {
                width = animation.Width;
                height = animation.Height;

                goto Calculate;
            }
            else if (constraint is Location)
            {
                width = 320;
                height = 200;

                goto Calculate;
            }
            else if (constraint is Photo photo)
            {
                if (ttl)
                {
                    width = 240;
                    height = 240;
                }
                else if (photo.Sizes.Count > 0)
                {
                    var size = photo.Sizes[^1];
                    width = size.Width;
                    height = size.Height;
                }

                goto Calculate;
            }
            else if (constraint is Sticker)
            {
                // We actually don't have to calculate bubble width for stickers,
                // As it might be wider due to reply
                //width = sticker.Width;
                //height = sticker.Height;

                //goto Calculate;
            }
            else if (constraint is Venue)
            {
                width = 320;
                height = 200;

                goto Calculate;
            }
            else if (constraint is Video video)
            {
                if (ttl)
                {
                    width = 240;
                    height = 240;
                }
                else
                {
                    width = video.Width;
                    height = video.Height;
                }

                goto Calculate;
            }
            else if (constraint is VideoNote)
            {
                // We actually don't have to calculate bubble width for video notes,
                // As it might be wider due to reply/forward
                //width = 224;
                //height = 224;

                //goto Calculate;
            }
            else if (constraint is VoiceNote voiceNote)
            {
                width = Math.Min(Math.Max(4, voiceNote.Duration), 30) / 30d * availableSize.Width;

                //return base.MeasureOverride(new Size(width, availableSize.Height));
            }

            return base.MeasureOverride(availableSize);

        Calculate:

            if (Footer.DesiredSize.IsEmpty)
            {
                Footer.Measure(availableSize);
            }

            var additional = 0d;
            var minWidth = caption ? 240 : 96;

            if (PhotoColumn.Width.IsAbsolute)
            {
                additional += 38;
            }

            if (Action != null)
            {
                additional += 38;
            }

            if (availableWidth + additional > availableSize.Width)
            {
                additional = 0;
            }

            width = Math.Max(Footer.DesiredSize.Width + /*margin left*/ 8 + /*padding right*/ 6 + /*margin right*/ 6, Math.Max(width, minWidth));

            if (width > availableWidth + additional || height > availableHeight)
            {
                var ratioX = availableWidth / width;
                var ratioY = availableHeight / height;
                var ratio = Math.Min(ratioX, ratioY);

                return base.MeasureOverride(new Size(Math.Max(minWidth, width * ratio) + additional, availableSize.Height));
            }
            else
            {
                return base.MeasureOverride(new Size(Math.Max(minWidth, width) + additional, availableSize.Height));
            }
        }

        private static bool IsFullMedia(MessageContent content, bool width = false)
        {
            switch (content)
            {
                case MessageLocation:
                case MessageVenue:
                case MessagePhoto:
                case MessageVideo:
                case MessageAnimation:
                case MessagePaidAlbum:
                    return true;
                case MessageAlbum album:
                    return album.IsMedia;
                case MessageInvoice invoice:
                    return invoice.PaidMedia is not PaidMediaUnsupported and not null
                        || (width && invoice.ProductInfo.Photo != null);
                case MessageAsyncStory story:
                    return story.State != MessageStoryState.Expired;
                case MessageRichMessage richMessage:
                    return richMessage.Message.Blocks[^1] is PageBlockAnimation { Caption: null } or PageBlockCollage { Caption : null } or PageBlockMap { Caption: null } or PageBlockPhoto { Caption: null } or PageBlockSlideshow { Caption: null } or PageBlockVideo { Caption: null };
                default:
                    return false;
            }
        }

        #region XamlMarkupHelper

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void LoadTemplateChild<T>(ref T element, [CallerArgumentExpression("element")] string name = null)
            where T : DependencyObject
        {
            element ??= GetTemplateChild(name) as T;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void UnloadTemplateChild<T>(ref T element)
            where T : DependencyObject
        {
            if (element != null)
            {
                XamlMarkupHelper.UnloadObject(element);
                element = null;
            }
        }

        #endregion
    }
}
