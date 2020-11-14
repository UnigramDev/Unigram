﻿using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Text;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Controls.Messages.Content;
using Unigram.Converters;
using Unigram.Services;
using Unigram.ViewModels;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Text;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media;

namespace Unigram.Controls.Messages
{
    public sealed partial class MessageBubble : StackPanel
    {
        private MessageViewModel _message;

        private string _query;

        public MessageBubble()
        {
            InitializeComponent();
        }

        public void UpdateQuery(string text)
        {
            _query = text;
        }

        public void UpdateMessage(MessageViewModel message)
        {
            _message = message;
            Tag = message;

            if (message != null)
            {
                UpdateMessageHeader(message);
                UpdateMessageReply(message);
                UpdateMessageContent(message);
                UpdateMessageInteractionInfo(message);

                Footer.UpdateMessage(message);
                UpdateMessageReplyMarkup(message);

                UpdateAttach(message);
            }
            else
            {
                Span.Inlines.Clear();
                Media.Child = null;
            }

            if (_highlight != null)
            {
                _highlight.StopAnimation("Opacity");
                _highlight.Opacity = 0;
            }
        }

        public string GetAutomationName()
        {
            if (_message == null)
            {
                return null;
            }

            return UpdateAutomation(_message);
        }

        public string UpdateAutomation(MessageViewModel message)
        {
            var chat = message.GetChat();
            var content = message.GeneratedContent ?? message.Content;

            var title = string.Empty;
            var senderBot = false;

            if (message.IsSaved())
            {
                title = message.ProtoService.GetTitle(message.ForwardInfo);
            }
            else
            {
                if (message.ProtoService.TryGetUser(message.Sender, out User senderUser))
                {
                    senderBot = senderUser.Type is UserTypeBot;
                    title = senderUser.GetFullName();
                }
                else if (message.ProtoService.TryGetChat(message.Sender, out Chat senderChat))
                {
                    title = message.ProtoService.GetTitle(senderChat);
                }
            }

            var builder = new StringBuilder();
            if (title?.Length > 0)
            {
                builder.AppendLine($"{title}. ");
            }

            if (message.ReplyToMessage != null)
            {
                if (message.ProtoService.TryGetUser(message.ReplyToMessage.Sender, out User replyUser))
                {
                    builder.AppendLine($"{Strings.Resources.AccDescrReplying} {replyUser.GetFullName()}. ");
                }
                else if (message.ProtoService.TryGetChat(message.ReplyToMessage.Sender, out Chat replyChat))
                {
                    builder.AppendLine($"{Strings.Resources.AccDescrReplying} {message.ProtoService.GetTitle(replyChat)}. ");
                }
            }

            builder.Append(Automation.GetSummary(message.ProtoService, message.Get()));

            if (message.EditDate != 0 && message.ViaBotUserId == 0 && !senderBot && !(message.ReplyMarkup is ReplyMarkupInlineKeyboard))
            {
                builder.Append($"{Strings.Resources.EditedMessage}, ");
            }

            var date = string.Format(Strings.Resources.TodayAtFormatted, BindConvert.Current.ShortTime.Format(Utils.UnixTimestampToDateTime(message.Date)));
            if (message.IsOutgoing)
            {
                builder.Append(string.Format(Strings.Resources.AccDescrSentDate, date));
            }
            else
            {
                builder.Append(string.Format(Strings.Resources.AccDescrReceivedDate, date));
            }

            builder.Append(". ");

            var maxId = 0L;
            if (chat != null)
            {
                maxId = chat.LastReadOutboxMessageId;
            }

            if (message.SendingState is MessageSendingStateFailed)
            {
            }
            else if (message.SendingState is MessageSendingStatePending)
            {
            }
            else if (message.Id <= maxId)
            {
                builder.Append(Strings.Resources.AccDescrMsgRead);
            }
            else
            {
                builder.Append(Strings.Resources.AccDescrMsgUnread);
            }

            if (message.InteractionInfo?.ViewCount > 0)
            {
                builder.Append(". ");
                builder.Append(Locale.Declension("AccDescrNumberOfViews", message.InteractionInfo.ViewCount));
            }

            builder.Append(".");

            return builder.ToString();
        }

        public void UpdateAttach(MessageViewModel message, bool wide = false)
        {
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

            if (message.IsOutgoing && !wide)
            {
                if (message.IsFirst && message.IsLast)
                {
                }
                else if (message.IsFirst)
                {
                    bottomRight = small;
                }
                else if (message.IsLast)
                {
                    topRight = small;
                }
                else
                {
                    topRight = small;
                    bottomRight = small;
                }
            }
            else
            {
                if (message.IsFirst && message.IsLast)
                {
                }
                else if (message.IsFirst)
                {
                    bottomLeft = small;
                }
                else if (message.IsLast)
                {
                    topLeft = small;
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
                if (content is MessageSticker || content is MessageDice || content is MessageVideoNote)
                {
                    ContentPanel.CornerRadius = new CornerRadius();
                }
                else
                {
                    ContentPanel.CornerRadius = new CornerRadius(topLeft, topRight, small, small);
                }

                if (Markup != null)
                {
                    Markup.CornerRadius = new CornerRadius(small, small, bottomRight, bottomLeft);
                }
            }
            else if (content is MessageSticker || content is MessageDice || content is MessageVideoNote)
            {
                ContentPanel.CornerRadius = new CornerRadius();
            }
            else
            {
                ContentPanel.CornerRadius = new CornerRadius(topLeft, topRight, bottomRight, bottomLeft);
            }

            //if (_selectorItem != null)
            //{
            //    //_selectorItem.Margin = new Thickness(0, message.IsFirst ? 2 : 1, 0, message.IsLast ? 2 : 1);
            //    //_selectorItem.Margin = new Thickness(0, message.IsFirst ? 3 : 1, 0, 1);
            //    _selectorItem.Margin = new Thickness(0, message.IsFirst ? 4 : 2, 0, 0);
            //}
            //else
            {
                //Margin = new Thickness(0, message.IsFirst ? 2 : 1, 0, message.IsLast ? 2 : 1);
                //Margin = new Thickness(0, message.IsFirst ? 3 : 1, 0, 1);
                Margin = new Thickness(0, message.IsFirst ? 4 : 2, 0, 0);
            }


            //UpdateMessageContent(message, true);
        }

        private SelectorItem _selectorItem;

        public void UpdateSelectorItem(SelectorItem selectorItem)
        {
            _selectorItem = selectorItem;
        }

        public void UpdateMessageReply(MessageViewModel message)
        {
            if (Reply == null && message.ReplyToMessageId != 0 && message.ReplyToMessageState != ReplyToMessageState.Hidden)
            {
                FindName("Reply");
            }

            if (Reply != null)
            {
                Reply.UpdateMessageReply(message);
            }
        }

        public void UpdateMessageHeader(MessageViewModel message)
        {
            var paragraph = HeaderLabel;
            var admin = AdminLabel;
            var parent = Header;

            paragraph.Inlines.Clear();

            if (message == null)
            {
                return;
            }

            var chat = message.GetChat();
            var content = message.GeneratedContent ?? message.Content;

            var sticker = content is MessageSticker;
            var light = sticker || content is MessageDice || content is MessageVideoNote;
            var shown = false;

            if (!light && message.IsFirst && !message.IsOutgoing && !message.IsChannelPost && (chat.Type is ChatTypeBasicGroup || chat.Type is ChatTypeSupergroup))
            {
                if (message.ProtoService.TryGetUser(message.Sender, out User senderUser))
                {
                    var hyperlink = new Hyperlink();
                    hyperlink.Inlines.Add(new Run { Text = senderUser.GetFullName() });
                    hyperlink.UnderlineStyle = UnderlineStyle.None;
                    hyperlink.Foreground = PlaceholderHelper.GetBrush(senderUser.Id);
                    hyperlink.Click += (s, args) => From_Click(message);

                    paragraph.Inlines.Add(hyperlink);
                    shown = true;
                }
                else if (message.ProtoService.TryGetChat(message.Sender, out Chat senderChat))
                {
                    var hyperlink = new Hyperlink();
                    hyperlink.Inlines.Add(new Run { Text = senderChat.Title });
                    hyperlink.UnderlineStyle = UnderlineStyle.None;
                    hyperlink.Foreground = PlaceholderHelper.GetBrush(senderChat.Id);
                    hyperlink.Click += (s, args) => From_Click(message);

                    paragraph.Inlines.Add(hyperlink);
                    shown = true;
                }
                else if (message.IsSaved())
                {
                    var title = string.Empty;
                    var foreground = default(SolidColorBrush);

                    if (message.ForwardInfo?.Origin is MessageForwardOriginUser fromUser)
                    {
                        title = message.ProtoService.GetUser(fromUser.SenderUserId)?.GetFullName();
                        foreground = PlaceholderHelper.GetBrush(fromUser.SenderUserId);
                    }
                    else if (message.ForwardInfo?.Origin is MessageForwardOriginChat fromChat)
                    {
                        title = message.ProtoService.GetTitle(message.ProtoService.GetChat(fromChat.SenderChatId));
                    }
                    else if (message.ForwardInfo?.Origin is MessageForwardOriginChannel fromChannel)
                    {
                        title = message.ProtoService.GetTitle(message.ProtoService.GetChat(fromChannel.ChatId));
                    }
                    else if (message.ForwardInfo?.Origin is MessageForwardOriginHiddenUser fromHiddenUser)
                    {
                        title = fromHiddenUser.SenderName;
                    }

                    var hyperlink = new Hyperlink();
                    hyperlink.Inlines.Add(new Run { Text = title ?? string.Empty });
                    hyperlink.UnderlineStyle = UnderlineStyle.None;
                    hyperlink.Click += (s, args) => FwdFrom_Click(message);

                    if (foreground != null)
                    {
                        hyperlink.Foreground = foreground;
                    }

                    paragraph.Inlines.Add(hyperlink);
                    shown = true;
                }
            }
            else if (!light && message.IsChannelPost && chat.Type is ChatTypeSupergroup && string.IsNullOrEmpty(message.ForwardInfo?.PublicServiceAnnouncementType))
            {
                var hyperlink = new Hyperlink();
                hyperlink.Inlines.Add(new Run { Text = message.ProtoService.GetTitle(chat) });
                hyperlink.UnderlineStyle = UnderlineStyle.None;
                //hyperlink.Foreground = Convert.Bubble(message.ChatId);
                hyperlink.Click += (s, args) => From_Click(message);

                paragraph.Inlines.Add(hyperlink);
                shown = false;
            }
            else if (!light && message.IsFirst && message.IsSaved())
            {
                var title = string.Empty;
                var foreground = default(SolidColorBrush);

                if (message.ForwardInfo?.Origin is MessageForwardOriginUser fromUser)
                {
                    title = message.ProtoService.GetUser(fromUser.SenderUserId)?.GetFullName();
                    foreground = PlaceholderHelper.GetBrush(fromUser.SenderUserId);
                }
                else if (message.ForwardInfo?.Origin is MessageForwardOriginChat fromChat)
                {
                    title = message.ProtoService.GetTitle(message.ProtoService.GetChat(fromChat.SenderChatId));
                }
                else if (message.ForwardInfo?.Origin is MessageForwardOriginChannel fromChannel)
                {
                    title = message.ProtoService.GetTitle(message.ProtoService.GetChat(fromChannel.ChatId));
                }
                else if (message.ForwardInfo?.Origin is MessageForwardOriginHiddenUser fromHiddenUser)
                {
                    title = fromHiddenUser.SenderName;
                }

                var hyperlink = new Hyperlink();
                hyperlink.Inlines.Add(new Run { Text = title ?? string.Empty });
                hyperlink.UnderlineStyle = UnderlineStyle.None;
                hyperlink.Click += (s, args) => FwdFrom_Click(message);

                if (foreground != null)
                {
                    hyperlink.Foreground = foreground;
                }

                paragraph.Inlines.Add(hyperlink);
                shown = true;
            }

            if (shown)
            {
                if (message.Sender is MessageSenderUser senderUser)
                {
                    var title = message.Delegate.GetAdminTitle(senderUser.UserId);
                    if (admin != null && !message.IsOutgoing && message.Delegate != null && !string.IsNullOrEmpty(title))
                    {
                        paragraph.Inlines.Add(new Run { Text = " " + title, Foreground = null });
                    }
                }
                else if (message.ForwardInfo != null && !message.IsChannelPost)
                {
                    paragraph.Inlines.Add(new Run { Text = " " + Strings.Resources.DiscussChannel, Foreground = null });
                }
            }

            var forward = false;

            if (message.ForwardInfo != null && !sticker && !message.IsSaved())
            {
                if (paragraph.Inlines.Count > 0)
                {
                    paragraph.Inlines.Add(new LineBreak());
                }

                if (message.ForwardInfo.PublicServiceAnnouncementType.Length > 0)
                {
                    var type = LocaleService.Current.GetString("PsaMessage_" + message.ForwardInfo.PublicServiceAnnouncementType);
                    if (type.Length > 0)
                    {
                        paragraph.Inlines.Add(new Run { Text = type, FontWeight = FontWeights.Normal });
                    }
                    else
                    {
                        paragraph.Inlines.Add(new Run { Text = Strings.Resources.PsaMessageDefault, FontWeight = FontWeights.Normal });
                    }

                    FindName(nameof(PsaInfo));
                    PsaInfo.Visibility = Visibility.Visible;
                }
                else
                {
                    paragraph.Inlines.Add(new Run { Text = Strings.Resources.ForwardedMessage, FontWeight = FontWeights.Normal });

                    if (PsaInfo != null)
                    {
                        PsaInfo.Visibility = Visibility.Collapsed;
                    }
                }

                paragraph.Inlines.Add(new LineBreak());
                paragraph.Inlines.Add(new Run { Text = Strings.Resources.From + " ", FontWeight = FontWeights.Normal });

                var title = string.Empty;
                var bold = true;

                if (message.ForwardInfo?.Origin is MessageForwardOriginUser fromUser)
                {
                    title = message.ProtoService.GetUser(fromUser.SenderUserId)?.GetFullName();
                }
                else if (message.ForwardInfo?.Origin is MessageForwardOriginChat fromChat)
                {
                    title = message.ProtoService.GetTitle(message.ProtoService.GetChat(fromChat.SenderChatId));
                }
                else if (message.ForwardInfo?.Origin is MessageForwardOriginChannel fromChannel)
                {
                    title = message.ProtoService.GetTitle(message.ProtoService.GetChat(fromChannel.ChatId));
                }
                else if (message.ForwardInfo?.Origin is MessageForwardOriginHiddenUser fromHiddenUser)
                {
                    title = fromHiddenUser.SenderName;
                    bold = false;
                }

                var hyperlink = new Hyperlink();
                hyperlink.Inlines.Add(new Run { Text = title, FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal });
                hyperlink.UnderlineStyle = UnderlineStyle.None;
                hyperlink.Foreground = light ? new SolidColorBrush(Colors.White) : GetBrush("MessageHeaderForegroundBrush");
                hyperlink.Click += (s, args) => FwdFrom_Click(message);

                paragraph.Inlines.Add(hyperlink);
                forward = true;
            }
            else if (PsaInfo != null)
            {
                PsaInfo.Visibility = Visibility.Collapsed;
            }

            //if (message.HasViaBotId && message.ViaBot != null && !message.ViaBot.IsDeleted && message.ViaBot.HasUsername)
            var viaBot = message.ProtoService.GetUser(message.ViaBotUserId);
            if (viaBot != null && viaBot.Type is UserTypeBot && !string.IsNullOrEmpty(viaBot.Username))
            {
                var hyperlink = new Hyperlink();
                hyperlink.Inlines.Add(new Run { Text = (paragraph.Inlines.Count > 0 ? " via @" : "via @"), FontWeight = FontWeights.Normal });
                hyperlink.Inlines.Add(new Run { Text = viaBot.Username });
                hyperlink.UnderlineStyle = UnderlineStyle.None;
                hyperlink.Foreground = light ? new SolidColorBrush(Colors.White) : GetBrush("MessageHeaderForegroundBrush");
                hyperlink.Click += (s, args) => ViaBot_Click(message);

                if (paragraph.Inlines.Count > 0 && !forward)
                {
                    paragraph.Inlines.Insert(1, hyperlink);
                }
                else
                {
                    paragraph.Inlines.Add(hyperlink);
                }
            }

            if (paragraph.Inlines.Count > 0)
            {
                var title = message.Delegate.GetAdminTitle(message.Sender);
                if (admin != null && shown && !message.IsOutgoing && message.Delegate != null && !string.IsNullOrEmpty(title))
                {
                    admin.Visibility = Visibility.Visible;
                    admin.Text = title;
                }
                else if (admin != null && shown && !message.IsChannelPost && message.Sender is MessageSenderChat && message.ForwardInfo != null)
                {
                    admin.Visibility = Visibility.Visible;
                    admin.Text = Strings.Resources.DiscussChannel;
                }
                else if (admin != null)
                {
                    admin.Visibility = Visibility.Collapsed;
                }

                paragraph.Inlines.Add(new Run { Text = " " });
                paragraph.Visibility = Visibility.Visible;
                parent.Visibility = Visibility.Visible;
            }
            else
            {
                if (admin != null)
                {
                    admin.Visibility = Visibility.Collapsed;
                }

                paragraph.Visibility = Visibility.Collapsed;
                parent.Visibility = (message.ReplyToMessageId != 0 && message.ReplyToMessageState != ReplyToMessageState.Hidden) ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private void ViaBot_Click(MessageViewModel message)
        {
            message.Delegate.OpenViaBot(message.ViaBotUserId);
        }

        private void FwdFrom_Click(MessageViewModel message)
        {
            if (message.ForwardInfo?.Origin is MessageForwardOriginUser fromUser)
            {
                message.Delegate.OpenUser(fromUser.SenderUserId);
            }
            else if (message.ForwardInfo?.Origin is MessageForwardOriginChat fromChat)
            {
                message.Delegate.OpenChat(fromChat.SenderChatId, true);
            }
            else if (message.ForwardInfo?.Origin is MessageForwardOriginChannel fromChannel)
            {
                message.Delegate.OpenChat(fromChannel.ChatId, fromChannel.MessageId);
            }
            else if (message.ForwardInfo?.Origin is MessageForwardOriginHiddenUser fromHiddenUser)
            {
                Window.Current.ShowTeachingTip(HeaderLabel, Strings.Resources.HidAccount);
                //await MessagePopup.ShowAsync(Strings.Resources.HidAccount, Strings.Resources.AppName, Strings.Resources.OK);
            }
        }

        private void From_Click(MessageViewModel message)
        {
            if (message.ProtoService.TryGetChat(message.Sender, out Chat senderChat))
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
            else if (message.Sender is MessageSenderUser senderUser)
            {
                message.Delegate.OpenUser(senderUser.UserId);
            }
        }

        public void UpdateMessageState(MessageViewModel message)
        {
            Footer.UpdateMessageState(message);
        }

        public void UpdateMessageEdited(MessageViewModel message)
        {
            Footer.UpdateMessageEdited(message);
            UpdateMessageReplyMarkup(message);
        }

        private void UpdateMessageReplyMarkup(MessageViewModel message)
        {
            if (message.ReplyMarkup is ReplyMarkupInlineKeyboard)
            {
                if (Markup == null)
                {
                    FindName(nameof(Markup));
                }

                Markup.Update(message, message.ReplyMarkup);
            }
            else
            {
                if (Markup != null)
                {
                    UnloadObject(Markup);
                }
            }
        }

        public void UpdateMessageIsPinned(MessageViewModel message)
        {
            Footer.UpdateMessageIsPinned(message);
        }

        public void UpdateMessageInteractionInfo(MessageViewModel message)
        {
            var info = message.InteractionInfo?.ReplyInfo;
            if (info == null || !message.IsChannelPost || !message.CanGetMessageThread)
            {
                if (message.ChatId == message.ProtoService.Options.RepliesBotChatId)
                {
                    if (Thread == null)
                    {
                        FindName(nameof(Thread));
                    }

                    RecentRepliers.Children.Clear();
                    ThreadGlyph.Visibility = Visibility.Visible;
                    ThreadLabel.Text = Strings.Resources.ViewInChat;

                    AutomationProperties.SetName(Thread, Strings.Resources.ViewInChat);

                    Thread.Visibility = Visibility.Visible;
                }
                else if (Thread != null)
                {
                    Thread.Visibility = Visibility.Collapsed;
                }
            }
            else
            {
                if (Thread == null)
                {
                    FindName(nameof(Thread));
                }

                RecentRepliers.Children.Clear();

                foreach (var sender in info.RecentRepliers)
                {
                    var picture = new ProfilePicture();
                    picture.Width = 24;
                    picture.Height = 24;
                    picture.IsEnabled = false;

                    if (message.ProtoService.TryGetUser(sender, out User user))
                    {
                        picture.Source = PlaceholderHelper.GetUser(message.ProtoService, user, 24);
                    }
                    else if (message.ProtoService.TryGetChat(sender, out Chat chat))
                    {
                        picture.Source = PlaceholderHelper.GetChat(message.ProtoService, chat, 24);
                    }

                    if (RecentRepliers.Children.Count > 0)
                    {
                        picture.Margin = new Thickness(-10, 0, 0, 0);
                    }

                    Canvas.SetZIndex(picture, -RecentRepliers.Children.Count);
                    RecentRepliers.Children.Add(picture);
                }

                ThreadGlyph.Visibility = RecentRepliers.Children.Count > 0
                    ? Visibility.Collapsed
                    : Visibility.Visible;

                ThreadLabel.Text = info.ReplyCount > 0
                    ? Locale.Declension("Comments", info.ReplyCount)
                    : Strings.Resources.LeaveAComment;

                AutomationProperties.SetName(Thread, info.ReplyCount > 0
                    ? Locale.Declension("Comments", info.ReplyCount)
                    : Strings.Resources.LeaveAComment);

                Thread.Visibility = Visibility.Visible;
            }

            Footer.UpdateMessageInteractionInfo(message);
        }

        public void UpdateMessageContentOpened(MessageViewModel message)
        {
            if (Media.Child is IContentWithFile content && content.IsValid(message.GeneratedContent ?? message.Content, true))
            {
                content.UpdateMessageContentOpened(message);
            }
        }

        public void UpdateMessageContent(MessageViewModel message)
        {
            Panel.Content = message?.Content;

            var content = message.GeneratedContent ?? message.Content;
            if (content is MessageText text && text.WebPage == null)
            {
                ContentPanel.Padding = new Thickness(0, 4, 0, 0);
                Media.Margin = new Thickness(0);
                FooterToNormal();
                Grid.SetRow(Footer, 2);
                Grid.SetRow(Message, 2);
                Panel.Placeholder = true;
            }
            else if (IsFullMedia(content))
            {
                var top = 0;
                var bottom = 0;

                var chat = message.GetChat();
                if (message.IsFirst && !message.IsOutgoing && !message.IsChannelPost && (chat.Type is ChatTypeBasicGroup || chat.Type is ChatTypeSupergroup))
                {
                    top = 4;
                }
                if (message.IsFirst && message.IsSaved())
                {
                    top = 4;
                }
                if ((message.ForwardInfo != null && !message.IsSaved()) || message.ViaBotUserId != 0 || (message.ReplyToMessageId != 0 && message.ReplyToMessageState != ReplyToMessageState.Hidden) || message.IsChannelPost)
                {
                    top = 4;
                }

                var caption = content is MessageVenue || content.HasCaption();
                if (caption)
                {
                    FooterToNormal();
                    bottom = 4;
                }
                else if (content is MessageCall || (content is MessageLocation location && location.LivePeriod > 0 && BindConvert.Current.DateTime(message.Date + location.LivePeriod) > DateTime.Now))
                {
                    FooterToHidden();
                }
                else
                {
                    FooterToMedia();
                }

                ContentPanel.Padding = new Thickness(0, top, 0, 0);
                Media.Margin = new Thickness(0, top, 0, bottom);
                Grid.SetRow(Footer, caption ? 4 : 3);
                Grid.SetRow(Message, caption ? 4 : 2);
                Panel.Placeholder = caption;
            }
            else if (content is MessageSticker || content is MessageDice || content is MessageVideoNote)
            {
                ContentPanel.Padding = new Thickness(0);
                Media.Margin = new Thickness(0);
                FooterToLightMedia(message.IsOutgoing && !message.IsChannelPost);
                Grid.SetRow(Footer, 3);
                Grid.SetRow(Message, 2);
                Panel.Placeholder = false;
            }
            else if ((content is MessageText webPage && webPage.WebPage != null) || content is MessageGame || (content is MessageContact contact && !string.IsNullOrEmpty(contact.Contact.Vcard)))
            {
                ContentPanel.Padding = new Thickness(0, 4, 0, 0);
                Media.Margin = new Thickness(10, -6, 10, 0);
                FooterToNormal();
                Grid.SetRow(Footer, 4);
                Grid.SetRow(Message, 2);
                Panel.Placeholder = false;
            }
            else if (content is MessagePoll)
            {
                ContentPanel.Padding = new Thickness(0, 4, 0, 0);
                Media.Margin = new Thickness(0);
                FooterToNormal();
                Grid.SetRow(Footer, 4);
                Grid.SetRow(Message, 2);
                Panel.Placeholder = false;
            }
            else if (content is MessageInvoice invoice)
            {
                var caption = invoice.Photo == null;

                ContentPanel.Padding = new Thickness(0, 4, 0, 0);
                Media.Margin = new Thickness(10, 0, 10, 6);
                FooterToNormal();
                Grid.SetRow(Footer, caption ? 3 : 4);
                Grid.SetRow(Message, 2);
                Panel.Placeholder = caption;
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
                Grid.SetRow(Footer, caption ? 4 : 3);
                Grid.SetRow(Message, caption ? 4 : 2);
                Panel.Placeholder = caption;
            }

            UpdateMessageText(message);

            if (Media.Child is IContent media && media.IsValid(content, true))
            {
                media.UpdateMessage(message);
            }
            else
            {
                if (content is MessageText textMessage && textMessage.WebPage != null)
                {
                    if (textMessage.WebPage.IsSmallPhoto())
                    {
                        Media.Child = new WebPageSmallPhotoContent(message);
                    }
                    else
                    {
                        Media.Child = new WebPageContent(message);
                    }
                }
                else if (content is MessageAlbum)
                {
                    Media.Child = new AlbumContent(message);
                }
                else if (content is MessageAnimation)
                {
                    Media.Child = new AnimationContent(message);
                }
                else if (content is MessageAudio)
                {
                    Media.Child = new AudioContent(message);
                }
                else if (content is MessageCall)
                {
                    Media.Child = new CallContent(message);
                }
                else if (content is MessageContact)
                {
                    Media.Child = new ContactContent(message);
                }
                else if (content is MessageDice)
                {
                    Media.Child = new DiceContent(message);
                }
                else if (content is MessageDocument)
                {
                    Media.Child = new DocumentContent(message);
                }
                else if (content is MessageGame)
                {
                    Media.Child = new GameContent(message);
                }
                else if (content is MessageInvoice invoice)
                {
                    if (invoice.Photo == null)
                    {
                        Media.Child = new InvoiceContent(message);
                    }
                    else
                    {
                        Media.Child = new InvoicePhotoContent(message);
                    }
                }
                else if (content is MessageLocation)
                {
                    Media.Child = new LocationContent(message);
                }
                else if (content is MessagePhoto)
                {
                    Media.Child = new PhotoContent(message);
                }
                else if (content is MessagePoll)
                {
                    Media.Child = new PollContent(message);
                }
                else if (content is MessageSticker sticker)
                {
                    if (sticker.Sticker.IsAnimated)
                    {
                        Media.Child = new AnimatedStickerContent(message);
                    }
                    else
                    {
                        Media.Child = new StickerContent(message);
                    }
                }
                else if (content is MessageVenue)
                {
                    Media.Child = new VenueContent(message);
                }
                else if (content is MessageVideo)
                {
                    Media.Child = new VideoContent(message);
                }
                else if (content is MessageVideoNote)
                {
                    Media.Child = new VideoNoteContent(message);
                }
                else if (content is MessageVoiceNote)
                {
                    Media.Child = new VoiceNoteContent(message);
                }
                else
                {
                    Media.Child = null;
                }
            }
        }

        public Border GetPlaybackElement()
        {
            if (Media.Child is IContentWithPlayback content)
            {
                return content.GetPlaybackElement();
            }

            return null;
        }

        public void UpdateFile(MessageViewModel message, File file)
        {
            if (Media.Child is IContentWithFile content)
            {
                content.UpdateFile(message, file);
            }

            if (Reply != null)
            {
                Reply.UpdateFile(message, file);
            }
        }

        private void UpdateMessageText(MessageViewModel message)
        {
            Span.Inlines.Clear();

            var result = false;
            var adjust = false;

            var content = message.GeneratedContent ?? message.Content;
            if (content is MessageText text)
            {
                result = ReplaceEntities(message, Span, text.Text, out adjust, true);
            }
            else if (content is MessageAlbum album)
            {
                result = ReplaceEntities(message, Span, album.Caption, out adjust);
            }
            else if (content is MessageAnimation animation)
            {
                result = ReplaceEntities(message, Span, animation.Caption, out adjust);
            }
            else if (content is MessageAudio audio)
            {
                result = ReplaceEntities(message, Span, audio.Caption, out adjust);
            }
            else if (content is MessageDocument document)
            {
                result = ReplaceEntities(message, Span, document.Caption, out adjust);
            }
            else if (content is MessagePhoto photo)
            {
                result = ReplaceEntities(message, Span, photo.Caption, out adjust);
            }
            else if (content is MessageVideo video)
            {
                result = ReplaceEntities(message, Span, video.Caption, out adjust);
            }
            else if (content is MessageVoiceNote voiceNote)
            {
                result = ReplaceEntities(message, Span, voiceNote.Caption, out adjust);
            }
            else if (content is MessageUnsupported unsupported)
            {
                result = GetEntities(message, Span, Strings.Resources.UnsupportedMedia, out adjust);
            }
            else if (content is MessageVenue venue)
            {
                Span.Inlines.Add(new Run { Text = venue.Venue.Title, FontWeight = FontWeights.SemiBold });
                Span.Inlines.Add(new LineBreak());
                Span.Inlines.Add(new Run { Text = venue.Venue.Address });
                result = true;
            }

            Message.Visibility = result ? Visibility.Visible : Visibility.Collapsed;
            //Footer.HorizontalAlignment = adjust ? HorizontalAlignment.Left : HorizontalAlignment.Right;

            if (adjust)
            {
                Span.Inlines.Add(new LineBreak());
            }
        }

        private bool GetEntities(MessageViewModel message, Span span, string text, out bool adjust)
        {
            if (string.IsNullOrEmpty(text))
            {
                //Message.Visibility = Visibility.Collapsed;
                adjust = false;
                return false;
            }
            else
            {
                //Message.Visibility = Visibility.Visible;

                var response = message.ProtoService.Execute(new GetTextEntities(text));
                if (response is TextEntities entities)
                {
                    return ReplaceEntities(message, span, text, entities.Entities, out adjust);
                }

                Span.Inlines.Add(new Run { Text = text });

                adjust = false;
                return true;
            }
        }

        private bool ReplaceEntities(MessageViewModel message, Span span, FormattedText text, out bool adjust, bool fontSize = false)
        {
            if (text == null)
            {
                adjust = false;
                return false;
            }

            return ReplaceEntities(message, span, text.Text, text.Entities, out adjust, fontSize);
        }

        private bool ReplaceEntities(MessageViewModel message, Span span, string text, IList<TextEntity> entities, out bool adjust, bool fontSize = false)
        {
            if (string.IsNullOrEmpty(text))
            {
                adjust = false;
                return false;
            }

            var runs = TextStyleRun.GetRuns(text, entities);
            var previous = 0;

            foreach (var entity in runs)
            {
                if (entity.Offset > previous)
                {
                    span.Inlines.Add(new Run { Text = text.Substring(previous, entity.Offset - previous) });
                }

                if (entity.Length + entity.Offset > text.Length)
                {
                    previous = entity.Offset + entity.Length;
                    continue;
                }

                if (entity.HasFlag(TextStyle.Monospace))
                {
                    span.Inlines.Add(new Run { Text = text.Substring(entity.Offset, entity.Length), FontFamily = new FontFamily("Consolas") });
                }
                else
                {
                    var local = span;

                    if (entity.HasFlag(TextStyle.Mention) || entity.HasFlag(TextStyle.Url))
                    {
                        if (entity.Type is TextEntityTypeMentionName || entity.Type is TextEntityTypeTextUrl)
                        {
                            var hyperlink = new Hyperlink();
                            object data;
                            if (entity.Type is TextEntityTypeTextUrl textUrl)
                            {
                                data = textUrl.Url;
                                MessageHelper.SetEntityData(hyperlink, textUrl.Url);
                                MessageHelper.SetEntityType(hyperlink, entity.Type);

                                ToolTipService.SetToolTip(hyperlink, textUrl.Url);
                            }
                            else if (entity.Type is TextEntityTypeMentionName mentionName)
                            {
                                data = mentionName.UserId;
                            }

                            hyperlink.Click += (s, args) => Entity_Click(message, entity.Type, null);
                            hyperlink.Foreground = GetBrush("MessageForegroundLinkBrush");
                            //hyperlink.Foreground = foreground;

                            span.Inlines.Add(hyperlink);
                            local = hyperlink;
                        }
                        else
                        {
                            var hyperlink = new Hyperlink();
                            var original = entities.FirstOrDefault(x => x.Offset <= entity.Offset && x.Offset + x.Length >= entity.End);

                            var data = text.Substring(entity.Offset, entity.Length);

                            if (original != null)
                            {
                                data = text.Substring(original.Offset, original.Length);
                            }

                            hyperlink.Click += (s, args) => Entity_Click(message, entity.Type, data);
                            hyperlink.Foreground = GetBrush("MessageForegroundLinkBrush");
                            //hyperlink.Foreground = foreground;

                            //if (entity.Type is TextEntityTypeUrl || entity.Type is TextEntityTypeEmailAddress || entity.Type is TextEntityTypeBankCardNumber)
                            {
                                MessageHelper.SetEntityData(hyperlink, data);
                                MessageHelper.SetEntityType(hyperlink, entity.Type);
                            }

                            span.Inlines.Add(hyperlink);
                            local = hyperlink;
                        }
                    }

                    var run = new Run { Text = text.Substring(entity.Offset, entity.Length) };

                    if (entity.HasFlag(TextStyle.Bold))
                    {
                        run.FontWeight = FontWeights.SemiBold;
                    }
                    if (entity.HasFlag(TextStyle.Italic))
                    {
                        run.FontStyle |= FontStyle.Italic;
                    }
                    if (entity.HasFlag(TextStyle.Underline))
                    {
                        run.TextDecorations |= TextDecorations.Underline;
                    }
                    if (entity.HasFlag(TextStyle.Strikethrough))
                    {
                        run.TextDecorations |= TextDecorations.Strikethrough;
                    }

                    local.Inlines.Add(run);

                    if (entity.Type is TextEntityTypeHashtag)
                    {
                        var data = text.Substring(entity.Offset, entity.Length);
                        var hex = data.TrimStart('#');

                        if ((hex.Length == 6 || hex.Length == 8) && int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int rgba))
                        {
                            byte r, g, b, a;
                            if (hex.Length == 8)
                            {
                                r = (byte)((rgba & 0xff000000) >> 24);
                                g = (byte)((rgba & 0x00ff0000) >> 16);
                                b = (byte)((rgba & 0x0000ff00) >> 8);
                                a = (byte)(rgba & 0x000000ff);
                            }
                            else
                            {
                                r = (byte)((rgba & 0xff0000) >> 16);
                                g = (byte)((rgba & 0x00ff00) >> 8);
                                b = (byte)(rgba & 0x0000ff);
                                a = 0xFF;
                            }

                            var color = Color.FromArgb(a, r, g, b);
                            var border = new Border
                            {
                                Width = 12,
                                Height = 12,
                                Margin = new Thickness(4, 4, 0, -2),
                                Background = new SolidColorBrush(color)
                            };

                            span.Inlines.Add(new InlineUIContainer { Child = border });
                        }
                    }
                }

                previous = entity.Offset + entity.Length;
            }

            if (text.Length > previous)
            {
                span.Inlines.Add(new Run { Text = text.Substring(previous) });
            }

            if (string.IsNullOrWhiteSpace(_query))
            {
                Message.TextHighlighters.Clear();
            }
            else
            {
                var find = text.IndexOf(_query, StringComparison.OrdinalIgnoreCase);
                if (find != -1)
                {
                    var highligher = new TextHighlighter();
                    highligher.Foreground = new SolidColorBrush(Colors.White);
                    highligher.Background = new SolidColorBrush(Colors.Orange);
                    highligher.Ranges.Add(new TextRange { StartIndex = find, Length = _query.Length });

                    Message.TextHighlighters.Add(highligher);
                }
                else
                {
                    Message.TextHighlighters.Clear();
                }
            }

            if (AdjustEmojis(span, text, fontSize))
            {
                Message.FlowDirection = FlowDirection.LeftToRight;
                adjust = (message.ReplyToMessageId == 0 || message.ReplyToMessageState == ReplyToMessageState.Hidden) && message.Content is MessageText;
            }
            else if (ApiInfo.FlowDirection == FlowDirection.LeftToRight && MessageHelper.IsAnyCharacterRightToLeft(text))
            {
                //Footer.HorizontalAlignment = HorizontalAlignment.Left;
                //span.Inlines.Add(new LineBreak());
                Message.FlowDirection = FlowDirection.RightToLeft;
                adjust = true;
            }
            else if (ApiInfo.FlowDirection == FlowDirection.RightToLeft && !MessageHelper.IsAnyCharacterRightToLeft(text))
            {
                //Footer.HorizontalAlignment = HorizontalAlignment.Left;
                //span.Inlines.Add(new LineBreak());
                Message.FlowDirection = FlowDirection.LeftToRight;
                adjust = true;
            }
            else
            {
                //Footer.HorizontalAlignment = HorizontalAlignment.Right;
                Message.FlowDirection = ApiInfo.FlowDirection;
                adjust = false;
            }

            return true;
        }

        private bool AdjustEmojis(Span span, string text, bool fontSize)
        {
            if (fontSize && SettingsService.Current.IsLargeEmojiEnabled && Emoji.TryCountEmojis(text, out int count, 3))
            {
                switch (count)
                {
                    case 1:
                        //Message.TextAlignment = TextAlignment.Center;
                        span.FontSize = 32;
                        return true;
                    case 2:
                        //Message.TextAlignment = TextAlignment.Center;
                        span.FontSize = 28;
                        return true;
                    case 3:
                        //Message.TextAlignment = TextAlignment.Center;
                        span.FontSize = 24;
                        return true;
                }
            }

            span.FontSize = Theme.Current.MessageFontSize;
            return false;
        }

        private Brush GetBrush(string key)
        {
            if (Resources.TryGetValue(key, out object value))
            {
                return value as SolidColorBrush;
            }

            return App.Current.Resources[key] as SolidColorBrush;
        }

        private void Entity_Click(MessageViewModel message, TextEntityType type, string data)
        {
            if (type is TextEntityTypeBotCommand)
            {
                message.Delegate.SendBotCommand(data);
            }
            else if (type is TextEntityTypeEmailAddress)
            {
                message.Delegate.OpenUrl("mailto:" + data, false);
            }
            else if (type is TextEntityTypePhoneNumber)
            {
                message.Delegate.OpenUrl("tel:" + data, false);
            }
            else if (type is TextEntityTypeHashtag || type is TextEntityTypeCashtag)
            {
                message.Delegate.OpenHashtag(data);
            }
            else if (type is TextEntityTypeMention)
            {
                message.Delegate.OpenUsername(data);
            }
            else if (type is TextEntityTypeMentionName mentionName)
            {
                message.Delegate.OpenUser(mentionName.UserId);
            }
            else if (type is TextEntityTypeTextUrl textUrl)
            {
                message.Delegate.OpenUrl(textUrl.Url, true);
            }
            else if (type is TextEntityTypeUrl)
            {
                message.Delegate.OpenUrl(data, false);
            }
            else if (type is TextEntityTypeBankCardNumber)
            {
                message.Delegate.OpenBankCardNumber(data);
            }
        }

        private void FooterToLightMedia(bool isOut)
        {
            VisualStateManager.GoToState(LayoutRoot, "LightState" + (isOut ? "Out" : string.Empty), false);

            if (Reply != null)
            {
                VisualStateManager.GoToState(Reply.Content as UserControl, "LightState", false);
            }
        }

        private void FooterToMedia()
        {
            VisualStateManager.GoToState(LayoutRoot, "MediaState", false);

            if (Reply != null)
            {
                VisualStateManager.GoToState(Reply.Content as UserControl, "Normal", false);
            }
        }

        private void FooterToHidden()
        {
            VisualStateManager.GoToState(LayoutRoot, "HiddenState", false);

            if (Reply != null)
            {
                VisualStateManager.GoToState(Reply.Content as UserControl, "Normal", false);
            }
        }

        private void FooterToNormal()
        {
            VisualStateManager.GoToState(LayoutRoot, "Normal", false);

            if (Reply != null)
            {
                VisualStateManager.GoToState(Reply.Content as UserControl, "Normal", false);
            }
        }

        public void RegisterEvents()
        {
            ContentPanel.SizeChanged -= OnSizeChanged;
            ContentPanel.SizeChanged += OnSizeChanged;
        }

        public void UnregisterEvents()
        {
            ContentPanel.SizeChanged -= OnSizeChanged;
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            var message = _message;
            if (message == null || e.PreviousSize.Width < 1 || e.PreviousSize.Height < 1)
            {
                return;
            }

            var content = message.GeneratedContent ?? message.Content;
            if (content is MessageSticker || content is MessageDice || content is MessageVideoNote)
            {
                return;
            }

            var outgoing = message.IsOutgoing && !message.IsChannelPost;

            var prev = e.PreviousSize.ToVector2();
            var next = e.NewSize.ToVector2();

            var anim = Window.Current.Compositor.CreateVector3KeyFrameAnimation();
            anim.InsertKeyFrame(0, new Vector3(prev.X / next.X, prev.Y / next.Y, 1));
            anim.InsertKeyFrame(1, Vector3.One);
            //anim.Duration = TimeSpan.FromSeconds(1);

            var visual = ElementCompositionPreview.GetElementVisual(ContentPanel);
            visual.CenterPoint = new Vector3(outgoing ? next.X : 0, 0, 0);
            visual.StartAnimation("Scale", anim);
        }

        private void Footer_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (e.PreviousSize.Width > 0 && e.NewSize.Width != e.PreviousSize.Width)
            {
                Panel.InvalidateMeasure();
            }
        }

        private SpriteVisual _highlight;

        public void Highlight()
        {
            var message = _message;
            if (message == null)
            {
                return;
            }

            var overlay = _highlight;
            if (overlay == null)
            {
                _highlight = overlay = Window.Current.Compositor.CreateSpriteVisual();
            }

            FrameworkElement target;
            if (Media.Child is IContentWithMask)
            {
                ElementCompositionPreview.SetElementChildVisual(ContentPanel, null);
                ElementCompositionPreview.SetElementChildVisual(Media, overlay);
                target = Media;
            }
            else
            {
                ElementCompositionPreview.SetElementChildVisual(Media, null);
                ElementCompositionPreview.SetElementChildVisual(ContentPanel, overlay);
                target = ContentPanel;
            }

            //Media.Content is IContentWithMask ? Media : (FrameworkElement)ContentPanel;

            //var overlay = ElementCompositionPreview.GetElementChildVisual(target) as SpriteVisual;
            //if (overlay == null)
            //{
            //    overlay = ElementCompositionPreview.GetElementVisual(this).Compositor.CreateSpriteVisual();
            //    ElementCompositionPreview.SetElementChildVisual(target, overlay);
            //}

            var settings = new UISettings();
            var fill = overlay.Compositor.CreateColorBrush(settings.GetColorValue(UIColorType.Accent));
            var brush = (CompositionBrush)fill;

            if (Media.Child is IContentWithMask withMask)
            {
                var alpha = withMask.GetAlphaMask();
                if (alpha != null)
                {
                    var mask = overlay.Compositor.CreateMaskBrush();
                    mask.Source = brush;
                    mask.Mask = alpha;

                    brush = mask;
                }
            }

            overlay.Size = new System.Numerics.Vector2((float)target.ActualWidth, (float)target.ActualHeight);
            overlay.Opacity = 0f;
            overlay.Brush = brush;

            var animation = overlay.Compositor.CreateScalarKeyFrameAnimation();
            animation.Duration = TimeSpan.FromSeconds(2);
            animation.InsertKeyFrame(300f / 2000f, 0.4f);
            animation.InsertKeyFrame(1700f / 2000f, 0.4f);
            animation.InsertKeyFrame(1, 0);

            overlay.StartAnimation("Opacity", animation);
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
                type = Strings.Resources.PsaMessageInfoDefault;
            }

            var entities = message.ProtoService.Execute(new GetTextEntities(type)) as TextEntities;
            Window.Current.ShowTeachingTip(PsaInfo, new FormattedText(type, entities.Entities), TeachingTipPlacementMode.TopLeft);
        }

        private void Thread_Click(object sender, RoutedEventArgs e)
        {
            var message = _message;
            if (message == null)
            {
                return;
            }

            message.Delegate.OpenThread(message);
        }

        private void Reply_Click(object sender, RoutedEventArgs e)
        {
            var message = _message;
            if (message == null)
            {
                return;
            }

            message.Delegate.OpenReply(message);
        }

        private void ReplyMarkup_ButtonClick(object sender, ReplyMarkupInlineButtonClickEventArgs e)
        {
            var message = _message;
            if (message == null)
            {
                return;
            }

            message.Delegate.OpenInlineButton(message, e.Button);
        }

        #endregion

        public void Mockup(string message, bool outgoing, DateTime date, bool first = true, bool last = true)
        {
            UpdateMockup(outgoing, first, last);

            Header.Visibility = Visibility.Collapsed;

            Footer.Mockup(outgoing, date);
            Panel.Content = new MessageText { Text = new FormattedText(message, new TextEntity[0]) };

            Media.Margin = new Thickness(0);
            FooterToNormal();
            Grid.SetRow(Footer, 2);
            Grid.SetRow(Message, 2);
            Panel.Placeholder = true;

            Span.Inlines.Clear();
            Span.Inlines.Add(new Run { Text = message });

            if (ApiInfo.FlowDirection == FlowDirection.LeftToRight && MessageHelper.IsAnyCharacterRightToLeft(message))
            {
                Span.Inlines.Add(new LineBreak());
            }
            else if (ApiInfo.FlowDirection == FlowDirection.RightToLeft && !MessageHelper.IsAnyCharacterRightToLeft(message))
            {
                Span.Inlines.Add(new LineBreak());
            }

            UpdateMockup();
        }

        public void Mockup(string message, string forwarded, bool link, bool outgoing, DateTime date, bool first = true, bool last = true)
        {
            UpdateMockup(outgoing, first, last);

            Header.Visibility = Visibility.Collapsed;

            Footer.Mockup(outgoing, date);
            Panel.Content = new MessageText { Text = new FormattedText(message, new TextEntity[0]) };

            Media.Margin = new Thickness(0);
            FooterToNormal();
            Grid.SetRow(Footer, 2);
            Grid.SetRow(Message, 2);
            Panel.Placeholder = true;

            Span.Inlines.Clear();
            Span.Inlines.Add(new Run { Text = message });

            if (ApiInfo.FlowDirection == FlowDirection.LeftToRight && MessageHelper.IsAnyCharacterRightToLeft(message))
            {
                Span.Inlines.Add(new LineBreak());
            }
            else if (ApiInfo.FlowDirection == FlowDirection.RightToLeft && !MessageHelper.IsAnyCharacterRightToLeft(message))
            {
                Span.Inlines.Add(new LineBreak());
            }

            HeaderLabel.Inlines.Add(new Run { Text = Strings.Resources.ForwardedMessage, FontWeight = FontWeights.Normal });
            HeaderLabel.Inlines.Add(new LineBreak());
            HeaderLabel.Inlines.Add(new Run { Text = Strings.Resources.From + " ", FontWeight = FontWeights.Normal });

            var hyperlink = new Hyperlink();
            hyperlink.Inlines.Add(new Run { Text = forwarded });
            hyperlink.UnderlineStyle = UnderlineStyle.None;
            hyperlink.Foreground = GetBrush("MessageHeaderForegroundBrush");
            //hyperlink.Click += (s, args) => FwdFrom_Click(message);

            HeaderLabel.Inlines.Add(hyperlink);

            Header.Visibility = Visibility.Visible;
            HeaderLabel.Visibility = Visibility.Visible;

            UpdateMockup();
        }

        public void Mockup(string message, string sender, string reply, bool outgoing, DateTime date, bool first = true, bool last = true)
        {
            UpdateMockup(outgoing, first, last);

            Header.Visibility = Visibility.Visible;
            HeaderLabel.Visibility = Visibility.Collapsed;
            AdminLabel.Visibility = Visibility.Collapsed;

            FindName("Reply");

            Reply.Visibility = Visibility.Visible;
            Reply.Mockup(sender, reply);

            Footer.Mockup(outgoing, date);
            Panel.Content = new MessageText { Text = new FormattedText(message, new TextEntity[0]) };

            Media.Margin = new Thickness(0);
            FooterToNormal();
            Grid.SetRow(Footer, 2);
            Grid.SetRow(Message, 2);
            Panel.Placeholder = true;

            Span.Inlines.Clear();
            Span.Inlines.Add(new Run { Text = message });

            if (ApiInfo.FlowDirection == FlowDirection.LeftToRight && MessageHelper.IsAnyCharacterRightToLeft(message))
            {
                Span.Inlines.Add(new LineBreak());
            }
            else if (ApiInfo.FlowDirection == FlowDirection.RightToLeft && !MessageHelper.IsAnyCharacterRightToLeft(message))
            {
                Span.Inlines.Add(new LineBreak());
            }

            UpdateMockup();
        }

        public void Mockup(MessageContent content, bool outgoing, DateTime date, bool first = true, bool last = true)
        {
            UpdateMockup(outgoing, first, last);

            Header.Visibility = Visibility.Collapsed;
            Message.Visibility = Visibility.Collapsed;

            Footer.Mockup(outgoing, date);
            Panel.Content = content;

            Media.Margin = new Thickness(10, 4, 10, 2);
            FooterToNormal();
            Grid.SetRow(Footer, 3);
            Grid.SetRow(Message, 2);
            Panel.Placeholder = false;

            if (content is MessageVoiceNote voiceNote)
            {
                var presenter = new VoiceNoteContent();
                presenter.Mockup(voiceNote);

                Media.Child = presenter;
            }
            else if (content is MessageAudio audio)
            {
                var presenter = new AudioContent();
                presenter.Mockup(audio);

                Media.Child = presenter;
            }

            Span.Inlines.Clear();

            UpdateMockup();
        }

        public void Mockup(MessageContent content, string caption, bool outgoing, DateTime date, bool first = true, bool last = true)
        {
            UpdateMockup(outgoing, first, last);

            Header.Visibility = Visibility.Collapsed;

            Footer.Mockup(outgoing, date);
            Panel.Content = content;

            Media.Margin = new Thickness(0, 0, 0, 4);
            FooterToNormal();
            Grid.SetRow(Footer, 4);
            Grid.SetRow(Message, 4);
            Panel.Placeholder = true;

            if (content is MessagePhoto photo)
            {
                var presenter = new PhotoContent();
                presenter.Mockup(photo);

                Media.Child = presenter;
            }

            Span.Inlines.Clear();
            Span.Inlines.Add(new Run { Text = caption });

            if (ApiInfo.FlowDirection == FlowDirection.LeftToRight && MessageHelper.IsAnyCharacterRightToLeft(caption))
            {
                Span.Inlines.Add(new LineBreak());
            }
            else if (ApiInfo.FlowDirection == FlowDirection.RightToLeft && !MessageHelper.IsAnyCharacterRightToLeft(caption))
            {
                Span.Inlines.Add(new LineBreak());
            }

            UpdateMockup();
        }

        public void UpdateMockup()
        {
            Span.FontSize = (double)App.Current.Resources["MessageFontSize"];
            ContentPanel.CornerRadius = new CornerRadius(SettingsService.Current.Appearance.BubbleRadius);
        }

        private void UpdateMockup(bool outgoing, bool first, bool last)
        {
            var topLeft = 15d;
            var topRight = 15d;
            var bottomRight = 15d;
            var bottomLeft = 15d;

            if (outgoing)
            {
                if (first && last)
                {
                }
                else if (first)
                {
                    bottomRight = 4;
                }
                else if (last)
                {
                    topRight = 4;
                }
                else
                {
                    topRight = 4;
                    bottomRight = 4;
                }
            }
            else
            {
                if (first && last)
                {
                }
                else if (first)
                {
                    bottomLeft = 4;
                }
                else if (last)
                {
                    topLeft = 4;
                }
                else
                {
                    topLeft = 4;
                    bottomLeft = 4;
                }
            }

            ContentPanel.CornerRadius = new CornerRadius(topLeft, topRight, bottomRight, bottomLeft);
            Margin = new Thickness(outgoing ? 50 : 12, first ? 2 : 1, outgoing ? 12 : 50, last ? 2 : 1);
        }





        protected override Size MeasureOverride(Size availableSize)
        {
            var availableWidth = Math.Min(availableSize.Width, Math.Min(double.IsNaN(Width) ? double.PositiveInfinity : Width, 320));
            var availableHeight = Math.Min(availableSize.Height, Math.Min(double.IsNaN(Height) ? double.PositiveInfinity : Height, 420));

            var ttl = false;
            var width = 0.0;
            var height = 0.0;

            var constraint = Tag;
            if (constraint is MessageViewModel viewModel)
            {
                ttl = viewModel.Ttl > 0;
                constraint = viewModel.GeneratedContent ?? viewModel.Content;
            }
            else if (constraint is Message message)
            {
                ttl = message.Ttl > 0;
                constraint = message.Content;
            }

            if (constraint is MessageAnimation animationMessage)
            {
                constraint = animationMessage.Animation;
            }
            else if (constraint is MessageInvoice invoiceMessage)
            {
                constraint = invoiceMessage.Photo;
            }
            else if (constraint is MessageLocation locationMessage)
            {
                constraint = locationMessage.Location;
            }
            else if (constraint is MessagePhoto photoMessage)
            {
                constraint = photoMessage.Photo;
            }
            else if (constraint is MessageSticker stickerMessage)
            {
                constraint = stickerMessage.Sticker;
            }
            else if (constraint is MessageVenue venueMessage)
            {
                constraint = venueMessage.Venue;
            }
            else if (constraint is MessageVideo videoMessage)
            {
                constraint = videoMessage.Video;
            }
            else if (constraint is MessageVideoNote videoNoteMessage)
            {
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
                    var positions = album.GetPositionsForWidth(availableWidth + MessageAlbum.ITEM_MARGIN);
                    width = positions.Item2.Width - MessageAlbum.ITEM_MARGIN;
                    height = positions.Item2.Height;

                    goto Calculate;
                }
            }

            if (constraint is Animation animation)
            {
                width = animation.Width;
                height = animation.Height;

                goto Calculate;
            }
            else if (constraint is Location location)
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
                    width = photo.Sizes[photo.Sizes.Count - 1].Width;
                    height = photo.Sizes[photo.Sizes.Count - 1].Height;
                }

                goto Calculate;
            }
            else if (constraint is Sticker sticker)
            {
                // We actually don't have to calculate bubble width for stickers,
                // As it might be wider due to reply
                //width = sticker.Width;
                //height = sticker.Height;

                //goto Calculate;
            }
            else if (constraint is Venue venue)
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
            else if (constraint is VideoNote videoNote)
            {
                // We actually don't have to calculate bubble width for video notes,
                // As it might be wider due to reply/forward
                //width = 200;
                //height = 200;

                //goto Calculate;
            }
            else if (constraint is VoiceNote voiceNote)
            {
                width = Math.Min(Math.Max(4, voiceNote.Duration), 30) / 30d * availableSize.Width;
                height = 48;

                //return base.MeasureOverride(new Size(width, availableSize.Height));
            }

            return base.MeasureOverride(availableSize);

            Calculate:

            if (Footer.DesiredSize.IsEmpty)
            {
                Footer.Measure(availableSize);
            }

            width = Math.Max(Footer.DesiredSize.Width + /*margin left*/ 8 + /*padding right*/ 6 + /*margin right*/ 6, Math.Max(width, 96));

            if (width > availableWidth || height > availableHeight)
            {
                var ratioX = availableWidth / width;
                var ratioY = availableHeight / height;
                var ratio = Math.Min(ratioX, ratioY);

                return base.MeasureOverride(new Size(Math.Max(96, width * ratio), availableSize.Height));
            }
            else
            {
                return base.MeasureOverride(new Size(Math.Max(96, width), availableSize.Height));
            }
        }

        private void Message_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            e.Handled = true;
        }

        private static bool IsFullMedia(MessageContent content, bool width = false)
        {
            switch (content)
            {
                case MessageLocation _:
                case MessageVenue _:
                case MessagePhoto _:
                case MessageVideo _:
                case MessageAnimation _:
                    return true;
                case MessageAlbum album:
                    return album.IsMedia;
                case MessageInvoice invoice:
                    return width && invoice.Photo != null;
                default:
                    return false;
            }
        }
    }
}
