//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Telegram.Common;
using Telegram.Controls.Cells;
using Telegram.Controls.Chats;
using Telegram.Controls.Media;
using Telegram.Converters;
using Telegram.Services;
using Telegram.Streams;
using Telegram.Td;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Telegram.ViewModels.Delegates;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Shapes;

namespace Telegram.Controls.Messages
{
    public partial class MessageService : Button, IReactionsDelegate
    {
        private MessageViewModel _message;

        public MessageService()
        {
            DefaultStyleKey = typeof(MessageService);
        }

        public MessageViewModel Message => _message;

        #region ContentOpacity

        public double ContentOpacity
        {
            get { return (double)GetValue(ContentOpacityProperty); }
            set { SetValue(ContentOpacityProperty, value); }
        }

        public static readonly DependencyProperty ContentOpacityProperty =
            DependencyProperty.Register("ContentOpacity", typeof(double), typeof(MessageService), new PropertyMetadata(1));

        #endregion

        protected override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            var content = FindName("Text") as FormattedTextBlock;
            if (content != null)
            {
                content.TextEntityClick += Message_TextEntityClick;
            }

            if (_message != null)
            {
                UpdateMessageInteractionInfo(_message);
            }
        }

        private void Message_TextEntityClick(object sender, TextEntityClickEventArgs e)
        {
            if (_message is not MessageViewModel message || message.Delegate == null)
            {
                return;
            }

            if (e.Type is TextEntityTypeMention && e.Text is string username)
            {
                message.Delegate.OpenUsername(username);
            }
            else if (e.Type is TextEntityTypeMentionName mentionName)
            {
                message.Delegate.OpenUser(mentionName.UserId);
            }
            else if (e.Type is TextEntityTypeTextUrl textUrl)
            {
                message.Delegate.OpenUrl(textUrl.Url, true, new OpenUrlSourceChat(message.ChatId, message.SenderId));
            }
            else if (e.Type is TextEntityTypeUrl && e.Text is string url)
            {
                message.Delegate.OpenUrl(url, false, new OpenUrlSourceChat(message.ChatId, message.SenderId));
            }
        }

        public void UpdateMessage(MessageViewModel message)
        {
            _message = message;

            var content = FindName("Text") as FormattedTextBlock;
            if (content == null)
            {
                UpdateContent(message);
                return;
            }

            var entities = GetEntities(message, true);
            if (entities.Text != null)
            {
                content.SetText(message.ClientService, entities.Text, entities.Entities);
                AutomationProperties.SetName(this, entities.Text);
            }

            UpdateContent(message);
            UpdateMessageInteractionInfo(message);
        }

        public void UpdateMessageTopic()
        {
            if (_message is not MessageViewModel message)
            {
                return;
            }

            var title = FindName("TitleLabel") as TextBlock;
            var photo = FindName("Photo") as ProfilePicture;
            var iconRoot = FindName("IconRoot") as Grid;
            var iconPath = FindName("IconPath") as Path;
            var iconText = FindName("IconText") as TextBlock;
            var typeIcon = FindName("TypeIcon") as IdentityIcon;

            if (message.ClientService.TryGetForumTopic(message.ChatId, message.TopicId, out ForumTopic topic))
            {
                title.Text = topic.Info.Name;
                photo.Source = null;

                if (topic.Info.IsGeneral || topic.Info.Icon.CustomEmojiId != 0)
                {
                    typeIcon.SetStatus(message.ClientService, topic.Info.Icon);
                    iconRoot.Visibility = Visibility.Collapsed;
                }
                else
                {
                    typeIcon.ClearStatus();
                    iconRoot.Visibility = Visibility.Visible;

                    var brush = ForumTopicCell.GetIconGradient(topic.Info.Icon);

                    iconPath.Fill = brush;
                    iconPath.Stroke = new SolidColorBrush(brush.GradientStops[1].Color);
                    iconText.Text = InitialNameStringConverter.Convert(topic.Info.Name);
                }
            }
            else if (message.ClientService.TryGetDirectMessagesChatTopic(message.ChatId, message.TopicId, out DirectMessagesChatTopic directMessagesChatTopic))
            {
                title.Text = message.ClientService.GetTitle(directMessagesChatTopic.SenderId);
                photo.Source = ProfilePictureSource.MessageSender(message.ClientService, directMessagesChatTopic.SenderId);

                typeIcon.ClearStatus();
                iconRoot.Visibility = Visibility.Collapsed;
            }
        }

        protected virtual void UpdateContent(MessageViewModel message)
        {
            if (message.Content is MessageHeaderAccountInfo)
            {
                if (message.Chat.ActionBar is ChatActionBarReportAddBlock reportAddBlock && reportAddBlock.AccountInfo != null)
                {
                    if (message.ClientService.TryGetUser(message.Chat, out User user) && message.ClientService.TryGetUserFull(user.Id, out UserFullInfo fullInfo))
                    {
                        var info = FindName("AccountInfo") as ChatAccountInfo;
                        info.Update(message.ClientService, user, fullInfo, reportAddBlock.AccountInfo);
                    }
                }
            }
            else if (message.Content is MessageHeaderMessageTopic)
            {
                var title = FindName("TitleLabel") as TextBlock;
                var photo = FindName("Photo") as ProfilePicture;
                var iconRoot = FindName("IconRoot") as Grid;
                var iconPath = FindName("IconPath") as Path;
                var iconText = FindName("IconText") as TextBlock;
                var typeIcon = FindName("TypeIcon") as IdentityIcon;

                if (message.ClientService.TryGetForumTopic(message.ChatId, message.TopicId, out ForumTopic topic))
                {
                    title.Text = topic.Info.Name;
                    photo.Source = null;

                    if (topic.Info.IsGeneral || topic.Info.Icon.CustomEmojiId != 0)
                    {
                        typeIcon.SetStatus(message.ClientService, topic.Info.Icon);
                        iconRoot.Visibility = Visibility.Collapsed;
                    }
                    else
                    {
                        typeIcon.ClearStatus();
                        iconRoot.Visibility = Visibility.Visible;

                        var brush = ForumTopicCell.GetIconGradient(topic.Info.Icon);

                        iconPath.Fill = brush;
                        iconPath.Stroke = new SolidColorBrush(brush.GradientStops[1].Color);
                        iconText.Text = InitialNameStringConverter.Convert(topic.Info.Name);
                    }
                }
                else if (message.ClientService.TryGetDirectMessagesChatTopic(message.ChatId, message.TopicId, out DirectMessagesChatTopic directMessagesChatTopic))
                {
                    title.Text = message.ClientService.GetTitle(directMessagesChatTopic.SenderId);
                    photo.Source = ProfilePictureSource.MessageSender(message.ClientService, directMessagesChatTopic.SenderId);

                    typeIcon.ClearStatus();
                    iconRoot.Visibility = Visibility.Collapsed;
                }

                AutomationProperties.SetName(this, title.Text);
            }
            else if (message.Content is MessageGiveawayPrizeStars giveawayPrizeStars)
            {
                var title = FindName("Title") as TextBlock;
                title.Text = Strings.ActionStarGiveawayPrizeTitle;

                var animation = FindName("Animation") as AnimatedImage;
                animation.Source = DelayedFileSource.FromSticker(message.ClientService, giveawayPrizeStars.Sticker);
            }
            else if (message.Content is MessageUpgradedGift upgradedGift)
            {
                var user = message.ClientService.GetUser(message.Chat);
                var self = message.ClientService.GetUser(message.ClientService.Options.MyId);

                if (user == null || self == null)
                {
                    return;
                }

                var centerColor = upgradedGift.Gift.Backdrop.Colors.CenterColor.ToColor();
                var edgeColor = upgradedGift.Gift.Backdrop.Colors.EdgeColor.ToColor();

                var ribbonTop = FindName("RibbonTop") as GradientStop;
                var ribbonBottom = FindName("RibbonBottom") as GradientStop;

                ribbonTop.Color = centerColor.Darken();
                ribbonBottom.Color = edgeColor.Darken();

                var pattern = FindName("Pattern") as PatternBackground;
                pattern.Update(message.ClientService, upgradedGift.Gift);

                var animation = FindName("Animation") as AnimatedImage;
                animation.Source = DelayedFileSource.FromSticker(message.ClientService, upgradedGift.Gift.Model.Sticker);

                var title = FindName("Title") as TextBlock;
                var subtitle = FindName("Subtitle") as TextBlock;
                var info = FindName("AttributeInfo") as TextBlock;
                var text = FindName("AttributeText") as TextBlock;

                if (upgradedGift.ReceiverId.IsUser(message.ClientService.Options.MyId) && upgradedGift.ReceiverId.AreTheSame(upgradedGift.SenderId))
                {
                    title.Text = Strings.Gift2ActionSelfTitle;
                }
                else
                {
                    title.Text = string.Format(Strings.Gift2UniqueTitle, message.IsOutgoing ? self.FirstName : user.FullName(true));
                }

                subtitle.Text = upgradedGift.Gift.ToName();

                info.Text = Strings.Gift2AttributeModel + "\n" + Strings.Gift2AttributeBackdrop + "\n" + Strings.Gift2AttributeSymbol;
                text.Text = upgradedGift.Gift.Model.Name + "\n" + upgradedGift.Gift.Backdrop.Name + "\n" + upgradedGift.Gift.Symbol.Name;
            }
            else if (message.Content is MessageGift gift)
            {
                var title = FindName("Title") as TextBlock;
                var subtitle = FindName("Subtitle") as FormattedTextBlock;
                var publisherRoot = FindName("Publisher") as Border;
                var publisherLabel = FindName("PublisherLabel") as TextBlock;
                var view = FindName("View") as Border;
                var button = FindName("ViewLabel") as TextBlock;
                var ribbonRoot = FindName("RibbonRoot") as Grid;

                var user = message.ClientService.GetTitle(gift.SenderId, true);
                var self = message.ClientService.GetUser(message.ClientService.Options.MyId);

                if (user == null || self == null)
                {
                    return;
                }

                if (message.IsOutgoing)
                {
                    title.Text = gift.IsPrivate
                        ? string.Format(Strings.Gift2ActionTitleInAnonymous, user)
                        : string.Format(Strings.Gift2ActionTitle, self.FullName(true));

                    if (gift.Text.Text.Length > 0)
                    {
                        subtitle.SetText(message.ClientService, gift.Text);
                    }
                    else if (gift.PrepaidUpgradeStarCount > 0)
                    {
                        subtitle.SetText(message.ClientService, ClientEx.ParseMarkdown(string.Format(Strings.Gift2ActionUpgradeOut, user)));
                    }
                    else if (gift.SellStarCount > 0)
                    {
                        subtitle.SetText(message.ClientService, ClientEx.ParseMarkdown(Locale.Declension(Strings.R.Gift2ActionOutInfo, gift.SellStarCount, user)));
                    }
                    else
                    {
                        subtitle.SetText(message.ClientService, ClientEx.ParseMarkdown(string.Format(Strings.Gift2Info2OutExpired, user)));
                    }

                    view.Visibility = Visibility.Visible;
                    button.Text = Strings.ActionGiftPremiumView;
                }
                else
                {
                    title.Text = gift.IsPrivate
                        ? Strings.Gift2ActionTitleAnonymous
                        : string.Format(Strings.Gift2ActionTitle, user);

                    if (gift.Text.Text.Length > 0)
                    {
                        subtitle.SetText(message.ClientService, gift.Text);
                    }
                    else if (gift.PrepaidUpgradeStarCount > 0 && !gift.WasUpgraded)
                    {
                        subtitle.SetText(message.ClientService, ClientEx.ParseMarkdown(Strings.Gift2ActionUpgrade));
                    }
                    else if (gift.IsSaved)
                    {
                        subtitle.SetText(message.ClientService, ClientEx.ParseMarkdown(Strings.Gift2ActionSavedInfo));
                    }
                    else
                    {
                        subtitle.SetText(message.ClientService, ClientEx.ParseMarkdown(gift.WasConverted
                            ? Locale.Declension(Strings.R.Gift2ActionConvertedInfo, gift.SellStarCount)
                            : Locale.Declension(Strings.R.Gift2ActionInfo, gift.SellStarCount)));
                    }

                    view.Visibility = Visibility.Visible;
                    button.Text = gift.PrepaidUpgradeStarCount > 0 && !gift.WasUpgraded
                        ? Strings.Gift2Unpack
                        : Strings.ActionGiftPremiumView;
                }

                var animation = FindName("Animation") as AnimatedImage;
                animation.LoopCount = 0;
                animation.Source = new DelayedFileSource(message.ClientService, gift.Gift.Sticker);
                animation.Margin = new Thickness(0, 0, 0, 8);

                if (message.ClientService.TryGetChat(gift.Gift.PublisherChatId, out Chat publisherChat)
                    && message.ClientService.TryGetSupergroup(publisherChat, out Supergroup publisher)
                    && publisher.HasActiveUsername(out string username))
                {
                    publisherRoot.Visibility = Visibility.Visible;
                    TextBlockHelper.SetMarkdown(publisherLabel, string.Format(Strings.Gift2ActionReleasedBy, $"@{username}"));
                }
                else
                {
                    publisherRoot.Visibility = Visibility.Collapsed;
                }

                if (gift.Gift.OverallLimits != null)
                {
                    ribbonRoot.Visibility = Visibility.Visible;

                    var ribbon = FindName("Ribbon") as TextBlock;
                    ribbon.Text = string.Format(Strings.Gift2Limited1OfRibbon, gift.Gift.TotalText());
                }
                else
                {
                    ribbonRoot.Visibility = Visibility.Collapsed;
                }
            }
            else if (message.Content is MessagePremiumGiftCode premiumGiftCode)
            {
                var title = FindName("Title") as TextBlock;
                var subtitle = FindName("Subtitle") as FormattedTextBlock;
                var view = FindName("View") as Border;
                var button = FindName("ViewLabel") as TextBlock;
                var ribbonRoot = FindName("RibbonRoot") as Grid;

                if (premiumGiftCode.Text.Text.Length > 0)
                {
                    subtitle.SetText(message.ClientService, premiumGiftCode.Text);
                }
                else
                {
                    subtitle.SetText(message.ClientService, ClientEx.ParseMarkdown(Strings.ActionGiftPremiumText));
                }

                // TODO: confirm no days here
                title.Text = Locale.Declension(Strings.R.ActionGiftPremiumTitle2, premiumGiftCode.DayCount / 30);
                button.Text = Strings.GiftPremiumUseGiftBtn;
                view.Visibility = Visibility.Visible;

                var animation = FindName("Animation") as AnimatedImage;
                animation.LoopCount = 1;
                animation.Margin = new Thickness(0, -20, 0, 12);
                animation.Source = DelayedFileSource.FromSticker(message.ClientService, premiumGiftCode.Sticker);

                ribbonRoot.Visibility = Visibility.Collapsed;
            }
            else if (message.Content is MessageGiftedPremium giftedPremium)
            {
                var title = FindName("Title") as TextBlock;
                var subtitle = FindName("Subtitle") as FormattedTextBlock;
                var view = FindName("View") as Border;
                var button = FindName("ViewLabel") as TextBlock;
                var ribbonRoot = FindName("RibbonRoot") as Grid;

                if (giftedPremium.Text.Text.Length > 0)
                {
                    subtitle.SetText(message.ClientService, giftedPremium.Text);
                }
                else
                {
                    subtitle.SetText(message.ClientService, ClientEx.ParseMarkdown(Strings.ActionGiftPremiumText));
                }

                // TODO: confirm no days here
                title.Text = Locale.Declension(Strings.R.ActionGiftPremiumTitle2, giftedPremium.DayCount / 30);
                button.Text = Strings.ActionGiftPremiumView;
                view.Visibility = Visibility.Visible;

                var animation = FindName("Animation") as AnimatedImage;
                animation.LoopCount = 1;
                animation.Margin = new Thickness(0, -20, 0, 12);
                animation.Source = DelayedFileSource.FromSticker(message.ClientService, giftedPremium.Sticker);

                ribbonRoot.Visibility = Visibility.Collapsed;
            }
            else if (message.Content is MessageGiftedStars giftedStars)
            {
                var title = FindName("Title") as TextBlock;
                var subtitle = FindName("Subtitle") as FormattedTextBlock;
                var view = FindName("View") as Border;
                var button = FindName("ViewLabel") as TextBlock;
                var ribbonRoot = FindName("RibbonRoot") as Grid;

                title.Text = Locale.Declension(Strings.R.ActionGiftStarsTitle, giftedStars.StarCount);
                button.Text = Strings.ActionGiftStarsView;
                view.Visibility = Visibility.Visible;

                if (giftedStars.ReceiverUserId == 0)
                {
                    subtitle.SetText(message.ClientService, ClientEx.ParseMarkdown(Strings.ActionGiftStarsSubtitleYou));
                }
                else if (message.ClientService.TryGetUser(giftedStars.ReceiverUserId, out User receiver))
                {
                    subtitle.SetText(message.ClientService, ClientEx.ParseMarkdown(string.Format(Strings.ActionGiftStarsSubtitle, receiver.FullName(true))));
                }

                var animation = FindName("Animation") as AnimatedImage;
                animation.LoopCount = 1;
                animation.Source = DelayedFileSource.FromSticker(message.ClientService, giftedStars.Sticker);
                animation.Margin = new Thickness(0, -20, 0, 12);

                ribbonRoot.Visibility = Visibility.Collapsed;
            }
            else if (message.Content is MessageChatChangePhoto chatChangePhoto)
            {
                var segments = FindName("Segments") as ActiveStoriesSegments;
                var photo = segments.Content as ProfilePicture;
                var view = FindName("View") as Border;

                Width = 216;

                segments.Visibility = Visibility.Visible;
                view.Visibility = Visibility.Visible;

                segments.SetChat(null, null, 120);
                photo.Source = ProfilePictureSource.ChatPhoto(message.ClientService, message.Chat, chatChangePhoto.Photo, true);

                if (view.Child is TextBlock label)
                {
                    label.Text = chatChangePhoto.Photo.Animation != null
                        ? Strings.ViewVideoAction
                        : Strings.ViewPhotoAction;
                }
            }
            else if (message.Content is MessageSuggestBirthdate suggestBirthdate)
            {
                var dateRoot = FindName("DateRoot") as Grid;
                var dayTitle = FindName("DateDayTitle") as TextBlock;
                var monthTitle = FindName("DateMonthTitle") as TextBlock;
                var yearTitle = FindName("DateYearTitle") as TextBlock;
                var dayValue = FindName("DateDayValue") as TextBlock;
                var monthValue = FindName("DateMonthValue") as TextBlock;
                var yearValue = FindName("DateYearValue") as TextBlock;
                var view = FindName("View") as Border;

                LocaleService.Current.GetDatePositions(out int dayPosition, out int monthPosition, out int yearPosition);

                Grid.SetColumn(dayTitle, dayPosition);
                Grid.SetColumn(dayValue, dayPosition);

                Grid.SetColumn(monthTitle, monthPosition);
                Grid.SetColumn(monthValue, monthPosition);

                Grid.SetColumn(yearTitle, yearPosition);
                Grid.SetColumn(yearValue, yearPosition);

                dayValue.Text = suggestBirthdate.Birthdate.Day.ToString();
                monthValue.Text = LocaleService.Current.CurrentCulture.DateTimeFormat.GetMonthName(suggestBirthdate.Birthdate.Month);
                yearValue.Text = suggestBirthdate.Birthdate.Year.ToString();

                if (suggestBirthdate.Birthdate.Year == 0)
                {
                    dateRoot.ColumnDefinitions[yearPosition].Width = new GridLength(0, GridUnitType.Pixel);
                }
                else
                {
                    dateRoot.ColumnDefinitions[yearPosition].Width = new GridLength(1, GridUnitType.Star);
                }

                view.Visibility = message.IsOutgoing
                    ? Visibility.Collapsed
                    : Visibility.Visible;
            }
            else if (message.Content is MessageSuggestProfilePhoto suggestProfilePhoto)
            {
                var segments = FindName("Segments") as ActiveStoriesSegments;
                var photo = segments.Content as ProfilePicture;
                var view = FindName("View") as Border;

                Width = 216;

                segments.Visibility = Visibility.Visible;
                view.Visibility = Visibility.Visible;

                // TODO: Here it should probably be the user, but it's not critical
                segments.SetChat(null, null, 120);
                photo.Source = ProfilePictureSource.ChatPhoto(message.ClientService, message.Chat, suggestProfilePhoto.Photo, true);

                if (view.Child is TextBlock label)
                {
                    label.Text = suggestProfilePhoto.Photo.Animation != null
                        ? Strings.ViewVideoAction
                        : Strings.ViewPhotoAction;
                }
            }
            else if (message.Content is MessageAsyncStory story)
            {
                var segments = FindName("Segments") as ActiveStoriesSegments;
                var photo = segments.Content as ProfilePicture;
                var view = FindName("View") as Border;

                if (story.State == MessageStoryState.Expired)
                {
                    Width = double.NaN;

                    segments.Visibility = Visibility.Collapsed;
                    view.Visibility = Visibility.Collapsed;
                }
                else
                {
                    Width = 216;

                    segments.Visibility = Visibility.Visible;
                    view.Visibility = Visibility.Visible;

                    if (message.ClientService.TryGetUser(message.SenderId, out User user) && message.ClientService.TryGetActiveStoriesFromUser(user.Id, out ChatActiveStories activeStories))
                    {
                        segments.UpdateSegments(120, story.Story?.PrivacySettings is StoryPrivacySettingsCloseFriends, activeStories.MaxReadStoryId < story.StoryId);
                    }
                    else
                    {
                        segments.UpdateSegments(120, story.Story?.PrivacySettings is StoryPrivacySettingsCloseFriends, false);
                    }

                    if (story.Story == null)
                    {
                        photo.Source = ProfilePictureSource.Chat(message.ClientService, message.Chat);
                    }
                    else
                    {
                        photo.Source = ProfilePictureSource.Story(message.ClientService, story.Story);
                    }

                    if (view.Child is TextBlock label)
                    {
                        label.Text = Strings.StoryMentionedAction;
                    }
                }
            }
            else if (message.Content is MessageChatSetBackground chatSetBackground)
            {
                var photo = FindName("Photo") as ChatBackgroundPresenter;
                var view = FindName("View") as Border;

                if (photo == null)
                {
                    return;
                }

                photo.UpdateSource(message.ClientService, chatSetBackground.Background.Background, true);
                view.Visibility = message.IsOutgoing || message.Chat.Type is not ChatTypePrivate
                    ? Visibility.Collapsed
                    : Visibility.Visible;

                if (message.IsOutgoing || view.Child is not TextBlock label)
                {
                    return;
                }

                var userFull = message.ClientService.GetUserFull(message.Chat);
                var sameBackground = chatSetBackground.Background.Background.Id == message.Chat.Background?.Background.Id;

                if (sameBackground && (userFull == null || userFull.SetChatBackground))
                {
                    label.Text = Strings.RemoveWallpaperAction;
                }
                else
                {
                    label.Text = Strings.ViewWallpaperAction;
                }
            }
            else if (message.Content is MessageChatEvent { Action: ChatEventBackgroundChanged backgroundChanged })
            {
                var photo = FindName("Photo") as ChatBackgroundPresenter;
                var view = FindName("View") as Border;

                if (photo == null || backgroundChanged.NewBackground == null)
                {
                    return;
                }

                photo.UpdateSource(message.ClientService, backgroundChanged.NewBackground.Background, true);
                view.Visibility = Visibility.Collapsed;
            }
        }

        public static string GetText(MessageWithOwner message)
        {
            return GetEntities(message, false).Text;
        }

        private static FormattedText GetEntities(MessageWithOwner message, bool history)
        {
            return message.Content switch
            {
                MessageBasicGroupChatCreate basicGroupChatCreate => UpdateBasicGroupChatCreate(message, basicGroupChatCreate, history),
                MessageBotWriteAccessAllowed botWriteAccessAllowed => UpdateBotWriteAccessAllowed(message, botWriteAccessAllowed, history),
                MessageChatAddMembers chatAddMembers => UpdateChatAddMembers(message, chatAddMembers, history),
                MessageChatChangePhoto chatChangePhoto => UpdateChatChangePhoto(message, chatChangePhoto, history),
                MessageChatChangeTitle chatChangeTitle => UpdateChatChangeTitle(message, chatChangeTitle, history),
                MessageChatSetTheme chatSetTheme => UpdateChatSetTheme(message, chatSetTheme, history),
                MessageChatDeleteMember chatDeleteMember => UpdateChatDeleteMember(message, chatDeleteMember, history),
                MessageChatDeletePhoto chatDeletePhoto => UpdateChatDeletePhoto(message, chatDeletePhoto, history),
                MessageChatHasProtectedContentToggled chatHasProtectedContentToggled => UpdateChatHasProtectedContentToggled(message, chatHasProtectedContentToggled, history),
                MessageChatHasProtectedContentDisableRequested chatHasProtectedContentDisableRequested => UpdateChatHasProtectedContentDisableRequested(message, chatHasProtectedContentDisableRequested, history),
                MessageChatJoinByLink chatJoinByLink => UpdateChatJoinByLink(message, chatJoinByLink, history),
                MessageChatJoinByRequest chatJoinByRequest => UpdateChatJoinByRequest(message, chatJoinByRequest, history),
                MessageChatSetBackground chatSetBackground => UpdateChatSetBackground(message, chatSetBackground, history),
                MessageChatSetMessageAutoDeleteTime chatSetMessageAutoDeleteTime => UpdateChatSetMessageAutoDeleteTime(message, chatSetMessageAutoDeleteTime, history),
                MessageChatShared chatShared => UpdateChatShared(message, chatShared, history),
                MessageChatUpgradeFrom chatUpgradeFrom => UpdateChatUpgradeFrom(message, chatUpgradeFrom, history),
                MessageChatUpgradeTo chatUpgradeTo => UpdateChatUpgradeTo(message, chatUpgradeTo, history),
                MessageContactRegistered contactRegistered => UpdateContactRegistered(message, contactRegistered, history),
                MessageCustomServiceAction customServiceAction => UpdateCustomServiceAction(message, customServiceAction, history),
                MessageDirectMessagePriceChanged directMessagePriceChanged => UpdateDirectMessagePriceChanged(message, directMessagePriceChanged, history),
                MessageForumTopicCreated forumTopicCreated => UpdateForumTopicCreated(message, forumTopicCreated, history),
                MessageForumTopicEdited forumTopicEdited => UpdateForumTopicEdited(message, forumTopicEdited, history),
                MessageForumTopicIsClosedToggled forumTopicIsClosedToggled => UpdateForumTopicIsClosedToggled(message, forumTopicIsClosedToggled, history),
                MessageForumTopicIsHiddenToggled forumTopicIsHiddenToggled => UpdateForumTopicIsHiddenToggled(message, forumTopicIsHiddenToggled, history),
                MessageGameScore gameScore => UpdateGameScore(message, gameScore, history),
                MessageGift gift => UpdateGift(message, gift, history),
                MessageGiftedPremium giftedPremium => UpdateGiftedPremium(message, giftedPremium, history),
                MessageGiftedStars giftedStars => UpdateGiftedStars(message, giftedStars, history),
                MessageGiveawayCreated giveawayCreated => UpdateGiveawayCreated(message, giveawayCreated, history),
                MessageGiveawayCompleted giveawayCompleted => UpdateGiveawayCompleted(message, giveawayCompleted, history),
                MessageGiveawayPrizeStars giveawayPrizeStars => UpdateGiveawayPrizeStars(message, giveawayPrizeStars, history),
                MessageInviteVideoChatParticipants inviteVideoChatParticipants => UpdateInviteVideoChatParticipants(message, inviteVideoChatParticipants, history),
                MessageProximityAlertTriggered proximityAlertTriggered => UpdateProximityAlertTriggered(message, proximityAlertTriggered, history),
                MessagePremiumGiftCode premiumGiftCode => UpdatePremiumGiftCode(message, premiumGiftCode, history),
                MessagePaidMessagePriceChanged paidMessagePriceChanged => UpdatePaidMessagePriceChanged(message, paidMessagePriceChanged, history),
                MessagePaidMessagesRefunded paidMessagesRefunded => UpdatePaidMessagesRefunded(message, paidMessagesRefunded, history),
                MessagePassportDataSent passportDataSent => UpdatePassportDataSent(message, passportDataSent, history),
                MessagePaymentSuccessful paymentSuccessful => UpdatePaymentSuccessful(message, paymentSuccessful, history),
                MessagePaymentRefunded paymentRefunded => UpdatePaymentRefunded(message, paymentRefunded, history),
                MessagePinMessage pinMessage => UpdatePinMessage(message, pinMessage, history),
                MessageScreenshotTaken screenshotTaken => UpdateScreenshotTaken(message, screenshotTaken, history),
                MessageSuggestBirthdate suggestBirthdate => UpdateSuggestBirthdate(message, suggestBirthdate, history),
                MessageSuggestProfilePhoto suggestProfilePhoto => UpdateSuggestProfilePhoto(message, suggestProfilePhoto, history),
                MessageSupergroupChatCreate supergroupChatCreate => UpdateSupergroupChatCreate(message, supergroupChatCreate, history),
                MessageUpgradedGift upgradedGift => UpdateUpgradedGift(message, upgradedGift, history),
                MessageUpgradedGiftPurchaseOffer upgradedGiftPurchaseOffer => UpdateUpgradedGiftPurchaseOffer(message, upgradedGiftPurchaseOffer, history),
                MessageUpgradedGiftPurchaseOfferRejected upgradedGiftPurchaseOfferRejected => UpdateUpgradedGiftPurchaseOfferRejected(message, upgradedGiftPurchaseOfferRejected, history),
                MessageUsersShared usersShared => UpdateUsersShared(message, usersShared, history),
                MessageVideoChatEnded videoChatEnded => UpdateVideoChatEnded(message, videoChatEnded, history),
                MessageVideoChatScheduled videoChatScheduled => UpdateVideoChatScheduled(message, videoChatScheduled, history),
                MessageVideoChatStarted videoChatStarted => UpdateVideoChatStarted(message, videoChatStarted, history),
                MessageWebAppDataSent webAppDataSent => UpdateWebAppDataSent(message, webAppDataSent, history),
                MessageExpiredPhoto expiredPhoto => UpdateExpiredPhoto(message, expiredPhoto, history),
                MessageExpiredVideo expiredVideo => UpdateExpiredVideo(message, expiredVideo, history),
                MessageExpiredVideoNote expiredVideoNote => UpdateExpiredVideoNote(message, expiredVideoNote, history),
                MessageExpiredVoiceNote expiredVoiceNote => UpdateExpiredVoiceNote(message, expiredVoiceNote, history),
                MessageChatBoost chatBoost => UpdateChatBoost(message, chatBoost, history),
                MessageChecklistTasksAdded checklistTasksAdded => UpdateChecklistTasksAdded(message, checklistTasksAdded, history),
                MessageChecklistTasksDone checklistTasksDone => UpdateChecklistTasksDone(message, checklistTasksDone, history),
                MessagePollOptionAdded pollOptionAdded => UpdatePollOptionAdded(message, pollOptionAdded, history),
                MessagePollOptionDeleted pollOptionDeleted => UpdatePollOptionDeleted(message, pollOptionDeleted, history),
                MessageSuggestedPostPaid suggestedPostPaid => UpdateSuggestedPostPaid(message, suggestedPostPaid, history),
                MessageSuggestedPostRefunded suggestedPostRefunded => UpdateSuggestedPostRefunded(message, suggestedPostRefunded, history),
                MessageAsyncStory story => UpdateStory(message, story, history),
                MessageStory story => UpdateStory(message, story, history),
                // Local types:
                MessageChatEvent chatEvent => chatEvent.Action switch
                {
                    ChatEventAutomaticTranslationToggled automaticTranslationToggled => UpdateAutomaticTranslationToggled(message, automaticTranslationToggled, history),
                    ChatEventAvailableReactionsChanged availableReactionsChanged => UpdateAvailableReactionsChanged(message, availableReactionsChanged, history),
                    ChatEventHasProtectedContentToggled hasProtectedContentToggled => UpdateHasProtectedContentToggled(message, hasProtectedContentToggled, history),
                    ChatEventSignMessagesToggled signMessagesToggled => UpdateSignMessagesToggled(message, signMessagesToggled, history),
                    ChatEventShowMessageSenderToggled showMessageSenderToggled => UpdateShowMessageSenderToggled(message, showMessageSenderToggled, history),
                    ChatEventStickerSetChanged stickerSetChanged => UpdateStickerSetChanged(message, stickerSetChanged, history),
                    ChatEventCustomEmojiStickerSetChanged customemojiStickerSetChanged => UpdateCustomEmojiStickerSetChanged(message, customemojiStickerSetChanged, history),
                    ChatEventInvitesToggled invitesToggled => UpdateInvitesToggled(message, invitesToggled, history),
                    ChatEventIsAllHistoryAvailableToggled isAllHistoryAvailableToggled => UpdateIsAllHistoryAvailableToggled(message, isAllHistoryAvailableToggled, history),
                    ChatEventLinkedChatChanged linkedChatChanged => UpdateLinkedChatChanged(message, linkedChatChanged, history),
                    ChatEventLocationChanged locationChanged => UpdateLocationChanged(message, locationChanged, history),
                    ChatEventMemberJoinedByInviteLink memberJoinedByInviteLink => UpdateMemberJoinedByInviteLink(message, memberJoinedByInviteLink, history),
                    ChatEventMessageUnpinned messageUnpinned => UpdateMessageUnpinned(message, messageUnpinned, history),
                    ChatEventMessageDeleted messageDeleted => UpdateMessageDeleted(message, messageDeleted, history),
                    ChatEventMessageEdited messageEdited => UpdateMessageEdited(message, messageEdited, history),
                    ChatEventMessageAutoDeleteTimeChanged messageAutoDeleteTimeChanged => UpdateMessageAutoDeleteTimeChanged(message, messageAutoDeleteTimeChanged, history),
                    ChatEventDescriptionChanged descriptionChanged => UpdateDescriptionChanged(message, descriptionChanged, history),
                    ChatEventInviteLinkDeleted inviteLinkDeleted => UpdateInviteLinkDeleted(message, inviteLinkDeleted, history),
                    ChatEventInviteLinkEdited inviteLinkEdited => UpdateInviteLinkEdited(message, inviteLinkEdited, history),
                    ChatEventInviteLinkRevoked inviteLinkRevoked => UpdateInviteLinkRevoked(message, inviteLinkRevoked, history),
                    ChatEventMessagePinned messagePinned => UpdateMessagePinned(message, messagePinned, history),
                    ChatEventUsernameChanged usernameChanged => UpdateUsernameChanged(message, usernameChanged, history),
                    ChatEventPollStopped pollStopped => UpdatePollStopped(message, pollStopped, history),
                    ChatEventSlowModeDelayChanged slowModeDelayChanged => UpdateSlowModeDelayChanged(message, slowModeDelayChanged, history),
                    ChatEventVideoChatCreated videoChatCreated => UpdateVideoChatCreated(message, videoChatCreated, history),
                    ChatEventVideoChatEnded videoChatEnded => UpdateVideoChatEnded(message, videoChatEnded, history),
                    ChatEventVideoChatMuteNewParticipantsToggled videoChatMuteNewParticipantsToggled => UpdateVideoChatMuteNewParticipantsToggled(message, videoChatMuteNewParticipantsToggled, history),
                    ChatEventVideoChatParticipantIsMutedToggled videoChatParticipantIsMutedToggled => UpdateVideoChatParticipantIsMutedToggled(message, videoChatParticipantIsMutedToggled, history),
                    ChatEventVideoChatParticipantVolumeLevelChanged videoChatParticipantVolumeLevelChanged => UpdateVideoChatParticipantVolumeLevelChanged(message, videoChatParticipantVolumeLevelChanged, history),
                    ChatEventIsForumToggled isForumToggled => UpdateChatEventIsForumToggled(message, isForumToggled, history),
                    ChatEventForumTopicCreated forumTopicCreated => UpdateChatEventForumTopicCreated(message, forumTopicCreated, history),
                    ChatEventForumTopicDeleted forumTopicDeleted => UpdateChatEventForumTopicDeleted(message, forumTopicDeleted, history),
                    ChatEventForumTopicEdited forumTopicEdited => UpdateChatEventForumTopicEdited(message, forumTopicEdited, history),
                    ChatEventForumTopicPinned forumTopicPinned => UpdateChatEventForumTopicPinned(message, forumTopicPinned, history),
                    ChatEventForumTopicToggleIsClosed forumTopicToggleIsClosed => UpdateChatEventForumTopicToggleIsClosed(message, forumTopicToggleIsClosed, history),
                    ChatEventAccentColorChanged accentColorChanged => UpdateChatEventAccentColorChanged(message, accentColorChanged, history),
                    ChatEventProfileAccentColorChanged profileAccentColorChanged => UpdateChatEventProfileAccentColorChanged(message, profileAccentColorChanged, history),
                    ChatEventEmojiStatusChanged emojiStatusChanged => UpdateChatEventEmojiStatusChanged(message, emojiStatusChanged, history),
                    ChatEventMemberTagChanged memberTagChanged => UpdateChatEventMemberTagChanged(message, memberTagChanged, history),
                    ChatEventBackgroundChanged backgroundChanged => UpdateChatEventBackgroundChanged(message, backgroundChanged, history),
                    //ChatEventActiveUsernamesChanged activeUsernamesChanged => UpdateChatEventActiveUsernames(messageUsernamesChanged),
                    _ => _emptyString
                },
                MessageHeaderDate headerDate => UpdateHeaderDate(message, headerDate),
                _ => _emptyString
            };
        }

        #region Local

        private static FormattedText UpdateHeaderDate(MessageWithOwner message, MessageHeaderDate headerDate)
        {
            if (message.SchedulingState is MessageSchedulingStateSendAtDate sendAtDate)
            {
                return string.Format(Strings.MessageScheduledOn, Formatter.DayGrouping(Formatter.ToLocalTime(sendAtDate.SendDate))).AsFormattedText();
            }
            else if (message.SchedulingState is MessageSchedulingStateSendWhenVideoProcessed sendWhenVideoProcessed)
            {
                return string.Format(Strings.MessageScheduledOn, Formatter.DayGrouping(Formatter.ToLocalTime(sendWhenVideoProcessed.SendDate))).AsFormattedText();
            }
            else if (message.SchedulingState is MessageSchedulingStateSendWhenOnline)
            {
                return Strings.MessageScheduledUntilOnline.AsFormattedText();
            }

            return Formatter.DayGrouping(Formatter.ToLocalTime(headerDate.Date)).AsFormattedText();
        }

        #endregion

        #region Event log

        private static FormattedText UpdateChatEventAccentColorChanged(MessageWithOwner message, ChatEventAccentColorChanged accentColorChanged, bool history)
        {
            FormattedText oldEmoji;
            FormattedText newEmoji;

            if (accentColorChanged.OldBackgroundCustomEmojiId != 0)
            {
                oldEmoji = new FormattedText("{0}", new[] { new TextEntity(0, 3, new TextEntityTypeCustomEmoji(accentColorChanged.OldBackgroundCustomEmojiId)) });
            }
            else
            {
                oldEmoji = Strings.EventLogEmojiNone.AsFormattedText();
            }

            if (accentColorChanged.NewBackgroundCustomEmojiId != 0)
            {
                newEmoji = new FormattedText("{1}", new[] { new TextEntity(0, 3, new TextEntityTypeCustomEmoji(accentColorChanged.NewBackgroundCustomEmojiId)) });
            }
            else
            {
                newEmoji = Strings.EventLogEmojiNone.AsFormattedText();
            }

            return ReplaceWithLink(ClientEx.Format(Strings.EventLogChangedPeerColorIcon, oldEmoji, newEmoji), message.GetSender());
        }

        private static FormattedText UpdateChatEventProfileAccentColorChanged(MessageWithOwner message, ChatEventProfileAccentColorChanged profileAccentColorChanged, bool history)
        {
            FormattedText oldEmoji;
            FormattedText newEmoji;

            if (profileAccentColorChanged.OldProfileBackgroundCustomEmojiId != 0)
            {
                oldEmoji = new FormattedText("{0}", new[] { new TextEntity(0, 3, new TextEntityTypeCustomEmoji(profileAccentColorChanged.OldProfileBackgroundCustomEmojiId)) });
            }
            else
            {
                oldEmoji = Strings.EventLogEmojiNone.AsFormattedText();
            }

            if (profileAccentColorChanged.NewProfileBackgroundCustomEmojiId != 0)
            {
                newEmoji = new FormattedText("{1}", new[] { new TextEntity(0, 3, new TextEntityTypeCustomEmoji(profileAccentColorChanged.NewProfileBackgroundCustomEmojiId)) });
            }
            else
            {
                newEmoji = Strings.EventLogEmojiNone.AsFormattedText();
            }

            return ReplaceWithLink(ClientEx.Format(Strings.EventLogChangedProfileColorIcon, oldEmoji, newEmoji), message.GetSender());
        }

        private static FormattedText UpdateChatEventEmojiStatusChanged(MessageWithOwner message, ChatEventEmojiStatusChanged emojiStatusChanged, bool history)
        {
            return _emptyString;

            //FormattedText oldEmoji;
            //FormattedText newEmoji;

            //if (emojiStatusChanged.NewEmojiStatus != null)
            //{
            //    // TODO: FormatTtl may not return the right value
            //    if (emojiStatusChanged.NewEmojiStatus.ExpirationDate != 0)
            //    {
            //        if (emojiStatusChanged.OldEmojiStatus != null)
            //        {
            //            content = ReplaceWithLink(Strings.EventLogChangedEmojiStatusFromFor, "un1", fromUser, entities);
            //            content = string.Format(content, "{0}", "{1}", Locale.FormatTtl(emojiStatusChanged.NewEmojiStatus.ExpirationDate - message.Date));
            //        }
            //        else
            //        {
            //            content = ReplaceWithLink(Strings.EventLogChangedEmojiStatusFor, "un1", fromUser, entities);
            //            content = string.Format(content, "{0}", "{1}", Locale.FormatTtl(emojiStatusChanged.NewEmojiStatus.ExpirationDate - message.Date));
            //        }
            //    }
            //    else if (emojiStatusChanged.OldEmojiStatus != null)
            //    {
            //        content = ReplaceWithLink(Strings.EventLogChangedEmojiStatusFrom, "un1", fromUser, entities);
            //    }
            //    else
            //    {
            //        content = ReplaceWithLink(Strings.EventLogChangedEmojiStatus, "un1", fromUser, entities);
            //    }
            //}
            //else
            //{
            //    content = ReplaceWithLink(Strings.EventLogChangedEmojiStatusFrom, "un1", fromUser, entities);
            //}

            //var index1 = content.IndexOf("{0}");
            //if (index1 != -1)
            //{
            //    if (emojiStatusChanged.OldEmojiStatus?.Type is EmojiStatusTypeCustomEmoji oldCustomEmoji)
            //    {
            //        entities.Add(new TextEntity(index1, 3, new TextEntityTypeCustomEmoji(oldCustomEmoji.CustomEmojiId)));
            //    }
            //    else if (emojiStatusChanged.OldEmojiStatus?.Type is EmojiStatusTypeUpgradedGift oldUpgradedGift)
            //    {
            //        entities.Add(new TextEntity(index1, 3, new TextEntityTypeCustomEmoji(oldUpgradedGift.ModelCustomEmojiId)));
            //    }
            //    else
            //    {
            //        content = content.Remove(index1, 3);
            //        content = content.Insert(index1, Strings.EventLogEmojiNone);
            //    }
            //}

            //var index2 = content.IndexOf("{1}");
            //if (index2 != -1)
            //{
            //    if (emojiStatusChanged.NewEmojiStatus?.Type is EmojiStatusTypeCustomEmoji newCustomEmoji)
            //    {
            //        entities.Add(new TextEntity(index2, 3, new TextEntityTypeCustomEmoji(newCustomEmoji.CustomEmojiId)));
            //    }
            //    else if (emojiStatusChanged.NewEmojiStatus?.Type is EmojiStatusTypeUpgradedGift newUpgradedGift)
            //    {
            //        entities.Add(new TextEntity(index2, 3, new TextEntityTypeCustomEmoji(newUpgradedGift.ModelCustomEmojiId)));
            //    }
            //    else
            //    {
            //        content = content.Remove(index2, 3);
            //        content = content.Insert(index2, Strings.EventLogEmojiNone);
            //    }
            //}

            //return new FormattedText(content, entities);
        }

        private static FormattedText UpdateChatEventMemberTagChanged(MessageWithOwner message, ChatEventMemberTagChanged memberTagChanged, bool history)
        {
            var newValue = !string.IsNullOrEmpty(memberTagChanged.NewTag);
            var oldValue = !string.IsNullOrEmpty(memberTagChanged.OldTag);

            var outgoing = message.SenderId.IsUser(memberTagChanged.UserId);

            if (newValue && oldValue)
            {
                return outgoing
                    ? FormattedText.Format(ReplaceWithLink(Strings.EventLogRankSelfEdit, message.GetSender()), memberTagChanged.OldTag, memberTagChanged.NewTag)
                    : FormattedText.Format(ReplaceWithLink(Strings.EventLogRankEdit, message.GetSender(), message.ClientService.GetUser(memberTagChanged.UserId)), memberTagChanged.OldTag, memberTagChanged.NewTag);
            }
            else if (newValue && !oldValue)
            {
                return outgoing
                    ? FormattedText.Format(ReplaceWithLink(Strings.EventLogRankSelfAdd, message.GetSender()), memberTagChanged.NewTag)
                    : FormattedText.Format(ReplaceWithLink(Strings.EventLogRankAdd, message.GetSender(), message.ClientService.GetUser(memberTagChanged.UserId)), memberTagChanged.NewTag);
            }

            return outgoing
                ? FormattedText.Format(ReplaceWithLink(Strings.EventLogRankSelfRemove, message.GetSender()), memberTagChanged.OldTag)
                : FormattedText.Format(ReplaceWithLink(Strings.EventLogRankRemove, message.GetSender(), message.ClientService.GetUser(memberTagChanged.UserId)), memberTagChanged.OldTag);
        }

        private static FormattedText UpdateChatEventBackgroundChanged(MessageWithOwner message, ChatEventBackgroundChanged backgroundChanged, bool history)
        {
            if (backgroundChanged.NewBackground != null)
            {
                return ReplaceWithLink(Strings.EventLogChangedWallpaper, message.GetSender());
            }
            else
            {
                return ReplaceWithLink(Strings.EventLogRemovedWallpaper, message.GetSender());
            }
        }

        private static FormattedText UpdateSlowModeDelayChanged(MessageWithOwner message, ChatEventSlowModeDelayChanged slowModeDelayChanged, bool history)
        {
            if (slowModeDelayChanged.NewSlowModeDelay > 0)
            {
                if (slowModeDelayChanged.NewSlowModeDelay < 60)
                {
                    return ReplaceWithLink(string.Format(Strings.EventLogToggledSlowmodeOn, string.Format(Strings.SlowmodeSeconds, slowModeDelayChanged.NewSlowModeDelay)), message.GetSender());
                }
                else if (slowModeDelayChanged.NewSlowModeDelay < 60 * 60)
                {
                    return ReplaceWithLink(string.Format(Strings.EventLogToggledSlowmodeOn, string.Format(Strings.SlowmodeMinutes, slowModeDelayChanged.NewSlowModeDelay / 60)), message.GetSender());
                }
                else
                {
                    return ReplaceWithLink(string.Format(Strings.EventLogToggledSlowmodeOn, string.Format(Strings.SlowmodeHours, slowModeDelayChanged.NewSlowModeDelay / 60 / 60)), message.GetSender());
                }
            }
            else
            {
                return ReplaceWithLink(Strings.EventLogToggledSlowmodeOff, message.GetSender());
            }
        }

        private static FormattedText UpdateAutomaticTranslationToggled(MessageWithOwner message, ChatEventAutomaticTranslationToggled automaticTranslationToggled, bool history)
        {
            if (automaticTranslationToggled.HasAutomaticTranslation)
            {
                return ReplaceWithLink(Strings.EventLogToggledAutotranslationOn, message.GetSender());
            }
            else
            {
                return ReplaceWithLink(Strings.EventLogToggledAutotranslationOff, message.GetSender());
            }
        }

        private static FormattedText UpdateAvailableReactionsChanged(MessageWithOwner message, ChatEventAvailableReactionsChanged availableReactionsChanged, bool history)
        {
            var oldAllOrNone = availableReactionsChanged.OldAvailableReactions is ChatAvailableReactionsAll or ChatAvailableReactionsSome { Reactions.Count: 0 };
            var newAllOrNone = availableReactionsChanged.NewAvailableReactions is ChatAvailableReactionsAll or ChatAvailableReactionsSome { Reactions.Count: 0 };

            static FormattedText ToString(ChatAvailableReactions reactions)
            {
                if (reactions is ChatAvailableReactionsAll || reactions is not ChatAvailableReactionsSome some)
                {
                    return Strings.AllReactions.AsFormattedText();
                }

                if (some.Reactions.Count > 0)
                {
                    var content = new StringBuilder();
                    var entities = new List<TextEntity>();

                    foreach (var item in some.Reactions)
                    {
                        if (item is ReactionTypeEmoji emoji)
                        {
                            content.Append(emoji.Emoji);
                        }
                        else if (item is ReactionTypeCustomEmoji customEmoji)
                        {
                            entities.Add(new TextEntity(content.Length, 2, new TextEntityTypeCustomEmoji(customEmoji.CustomEmojiId)));
                            content.Append("\U0001F921");
                        }
                    }

                    return new FormattedText(content.ToString(), entities);
                }

                return Strings.NoReactions.AsFormattedText();
            }

            if (oldAllOrNone || newAllOrNone)
            {
                var oldText = ToString(availableReactionsChanged.OldAvailableReactions);
                var newText = ToString(availableReactionsChanged.NewAvailableReactions);

                var content = ClientEx.Format(Strings.ActionReactionsChanged, oldText, newText);
                return ReplaceWithLink(content, message.GetSender());
            }
            else
            {
                var content = ClientEx.Format(Strings.ActionReactionsChangedList, ToString(availableReactionsChanged.NewAvailableReactions));
                return ReplaceWithLink(content, message.GetSender());
            }
        }

        private static FormattedText UpdateHasProtectedContentToggled(MessageWithOwner message, ChatEventHasProtectedContentToggled hasProtectedContentToggled, bool history)
        {
            if (hasProtectedContentToggled.HasProtectedContent)
            {
                return ReplaceWithLink(message.IsChannelPost
                    ? Strings.ActionForwardsRestrictedChannel
                    : Strings.ActionForwardsRestrictedGroup, message.GetSender());
            }
            else
            {
                return ReplaceWithLink(message.IsChannelPost
                    ? Strings.ActionForwardsEnabledChannel
                    : Strings.ActionForwardsEnabledGroup, message.GetSender());
            }
        }

        private static FormattedText UpdateSignMessagesToggled(MessageWithOwner message, ChatEventSignMessagesToggled signMessagesToggled, bool history)
        {
            if (signMessagesToggled.SignMessages)
            {
                return ReplaceWithLink(Strings.EventLogToggledSignaturesOn, message.GetSender());
            }
            else
            {
                return ReplaceWithLink(Strings.EventLogToggledSignaturesOff, message.GetSender());
            }
        }

        private static FormattedText UpdateShowMessageSenderToggled(MessageWithOwner message, ChatEventShowMessageSenderToggled showMessageSenderToggled, bool history)
        {
            if (showMessageSenderToggled.ShowMessageSender)
            {
                return ReplaceWithLink(Strings.EventLogToggledSignaturesProfilesOn, message.GetSender());
            }
            else
            {
                return ReplaceWithLink(Strings.EventLogToggledSignaturesProfilesOff, message.GetSender());
            }
        }

        private static FormattedText UpdateStickerSetChanged(MessageWithOwner message, ChatEventStickerSetChanged stickerSetChanged, bool history)
        {
            if (stickerSetChanged.NewStickerSetId == 0)
            {
                return ReplaceWithLink(Strings.EventLogRemovedStickersSet, message.GetSender());
            }
            else
            {
                return ReplaceWithLink(Strings.EventLogChangedStickersSet, message.GetSender());
            }
        }

        private static FormattedText UpdateCustomEmojiStickerSetChanged(MessageWithOwner message, ChatEventCustomEmojiStickerSetChanged customEmojiStickerSetChanged, bool history)
        {
            if (customEmojiStickerSetChanged.NewStickerSetId == 0)
            {
                return ReplaceWithLink(Strings.EventLogRemovedEmojiPack, message.GetSender());
            }
            else
            {
                return ReplaceWithLink(Strings.EventLogChangedEmojiPack, message.GetSender());
            }
        }

        private static FormattedText UpdateInvitesToggled(MessageWithOwner message, ChatEventInvitesToggled invitesToggled, bool history)
        {
            if (invitesToggled.CanInviteUsers)
            {
                return ReplaceWithLink(Strings.EventLogToggledInvitesOn, message.GetSender());
            }
            else
            {
                return ReplaceWithLink(Strings.EventLogToggledInvitesOff, message.GetSender());
            }
        }

        private static FormattedText UpdateIsAllHistoryAvailableToggled(MessageWithOwner message, ChatEventIsAllHistoryAvailableToggled isAllHistoryAvailableToggled, bool history)
        {
            if (isAllHistoryAvailableToggled.IsAllHistoryAvailable)
            {
                return ReplaceWithLink(Strings.EventLogToggledInvitesHistoryOn, message.GetSender());
            }
            else
            {
                return ReplaceWithLink(Strings.EventLogToggledInvitesHistoryOff, message.GetSender());
            }
        }

        private static FormattedText UpdateLinkedChatChanged(MessageWithOwner message, ChatEventLinkedChatChanged linkedChatChanged, bool history)
        {
            if (message.IsChannelPost)
            {
                if (linkedChatChanged.NewLinkedChatId != 0)
                {
                    return ReplaceWithLink(Strings.EventLogChangedLinkedGroup, message.GetSender(), message.ClientService.GetChat(linkedChatChanged.NewLinkedChatId));
                }
                else
                {
                    return ReplaceWithLink(Strings.EventLogRemovedLinkedGroup, message.GetSender(), message.ClientService.GetChat(linkedChatChanged.OldLinkedChatId));
                }
            }
            else
            {
                if (linkedChatChanged.NewLinkedChatId != 0)
                {
                    return ReplaceWithLink(Strings.EventLogChangedLinkedChannel, message.GetSender(), message.ClientService.GetChat(linkedChatChanged.NewLinkedChatId));
                }
                else
                {
                    return ReplaceWithLink(Strings.EventLogRemovedLinkedChannel, message.GetSender(), message.ClientService.GetChat(linkedChatChanged.OldLinkedChatId));
                }
            }
        }

        private static FormattedText UpdateLocationChanged(MessageWithOwner message, ChatEventLocationChanged locationChanged, bool history)
        {
            if (locationChanged.NewLocation != null)
            {
                return ReplaceWithLink(string.Format(Strings.EventLogChangedLocation, locationChanged.NewLocation.Address), message.GetSender());
            }
            else
            {
                return ReplaceWithLink(Strings.EventLogRemovedLocation, message.GetSender());
            }
        }

        private static FormattedText UpdateMemberJoinedByInviteLink(MessageWithOwner message, ChatEventMemberJoinedByInviteLink memberJoinedByInviteLink, bool history)
        {
            if (message.IsOutgoing)
            {
                return Strings.ActionInviteYou.AsFormattedText();
            }
            else
            {
                if (memberJoinedByInviteLink.ViaChatFolderInviteLink)
                {
                    return ReplaceWithLink(Strings.ActionInviteUserFolder, message.GetSender());
                }
                else
                {
                    return ReplaceWithLink(Strings.ActionInviteUser, message.GetSender());
                }
            }
        }

        private static FormattedText UpdateMessageUnpinned(MessageWithOwner message, ChatEventMessageUnpinned messageUnpinned, bool history)
        {
            return ReplaceWithLink(Strings.EventLogUnpinnedMessages, message.GetSender());
        }

        private static FormattedText UpdateMessageDeleted(MessageWithOwner message, ChatEventMessageDeleted messageDeleted, bool history)
        {
            return ReplaceWithLink(Strings.EventLogDeletedMessages, message.GetSender());
        }

        private static FormattedText UpdateMessageEdited(MessageWithOwner message, ChatEventMessageEdited messageEdited, bool history)
        {
            if (messageEdited.NewMessage.Content is MessageText)
            {
                return ReplaceWithLink(Strings.EventLogEditedMessages, message.GetSender());
            }
            else
            {
                return ReplaceWithLink(Strings.EventLogEditedCaption, message.GetSender());
            }
        }

        private static FormattedText UpdateMessageAutoDeleteTimeChanged(MessageWithOwner message, ChatEventMessageAutoDeleteTimeChanged messageAutoDeleteTimeChanged, bool history)
        {
            if (messageAutoDeleteTimeChanged.NewMessageAutoDeleteTime > 0)
            {
                return ReplaceWithLink(string.Format(Strings.ActionTTLChanged, Locale.FormatTtl(messageAutoDeleteTimeChanged.NewMessageAutoDeleteTime)), message.GetSender());
            }
            else
            {
                return ReplaceWithLink(Strings.ActionTTLDisabled, message.GetSender());
            }
        }

        private static FormattedText UpdateDescriptionChanged(MessageWithOwner message, ChatEventDescriptionChanged descriptionChanged, bool history)
        {
            if (message.IsChannelPost)
            {
                return ReplaceWithLink(Strings.EventLogEditedChannelDescription, message.GetSender());
            }
            else
            {
                return ReplaceWithLink(Strings.EventLogEditedGroupDescription, message.GetSender());
            }
        }

        private static FormattedText UpdateInviteLinkDeleted(MessageWithOwner message, ChatEventInviteLinkDeleted inviteLinkDeleted, bool history)
        {
            return ReplaceWithLink(string.Format(Strings.ActionDeletedInviteLink, inviteLinkDeleted.InviteLink.InviteLink), message.GetSender());
        }

        private static FormattedText UpdateInviteLinkEdited(MessageWithOwner message, ChatEventInviteLinkEdited inviteLinkEdited, bool history)
        {
            //if (inviteLinkEdited.)
            //{
            //}
            //else
            {
                return ReplaceWithLink(string.Format(Strings.ActionEditedInviteLinkToSame, inviteLinkEdited.NewInviteLink.InviteLink), message.GetSender());
            }
        }

        private static FormattedText UpdateInviteLinkRevoked(MessageWithOwner message, ChatEventInviteLinkRevoked inviteLinkRevoked, bool history)
        {
            return ReplaceWithLink(string.Format(Strings.ActionRevokedInviteLink, inviteLinkRevoked.InviteLink.InviteLink), message.GetSender());
        }

        private static FormattedText UpdateMessagePinned(MessageWithOwner message, ChatEventMessagePinned messagePinned, bool history)
        {
            return ReplaceWithLink(Strings.EventLogPinnedMessages, message.GetSender());
        }

        private static FormattedText UpdateUsernameChanged(MessageWithOwner message, ChatEventUsernameChanged usernameChanged, bool history)
        {
            if (string.IsNullOrEmpty(usernameChanged.NewUsername))
            {
                return ReplaceWithLink(Strings.EventLogRemovedGroupLink, message.GetSender());
            }
            else
            {
                return ReplaceWithLink(Strings.EventLogChangedGroupLink, message.GetSender());
            }
        }

        private static FormattedText UpdatePollStopped(MessageWithOwner message, ChatEventPollStopped pollStopped, bool history)
        {
            var fromUser = message.GetSender();

            var poll = pollStopped.Message.Content as MessagePoll;
            if (poll.Poll.Type is PollTypeRegular)
            {
                return ReplaceWithLink(Strings.EventLogStopPoll, message.GetSender());
            }
            else if (poll.Poll.Type is PollTypeQuiz)
            {
                return ReplaceWithLink(Strings.EventLogStopQuiz, message.GetSender());
            }

            return _emptyString;
        }

        private static FormattedText UpdateVideoChatCreated(MessageWithOwner message, ChatEventVideoChatCreated videoChatCreated, bool history)
        {
            if (message.IsChannelPost)
            {
                return ReplaceWithLink(Strings.EventLogStartedLiveStream, message.GetSender());
            }
            else
            {
                return ReplaceWithLink(Strings.EventLogStartedVoiceChat, message.GetSender());
            }
        }

        private static FormattedText UpdateVideoChatEnded(MessageWithOwner message, ChatEventVideoChatEnded videoChatEnded, bool history)
        {
            if (message.IsChannelPost)
            {
                return ReplaceWithLink(Strings.EventLogEndedLiveStream, message.GetSender());
            }
            else
            {
                return ReplaceWithLink(Strings.EventLogEndedVoiceChat, message.GetSender());
            }
        }

        private static FormattedText UpdateVideoChatMuteNewParticipantsToggled(MessageWithOwner message, ChatEventVideoChatMuteNewParticipantsToggled videoChatMuteNewParticipantsToggled, bool history)
        {
            if (videoChatMuteNewParticipantsToggled.MuteNewParticipants)
            {
                return ReplaceWithLink(Strings.EventLogVoiceChatNotAllowedToSpeak, message.GetSender());
            }
            else
            {
                return ReplaceWithLink(Strings.EventLogVoiceChatAllowedToSpeak, message.GetSender());
            }
        }

        private static FormattedText UpdateVideoChatParticipantIsMutedToggled(MessageWithOwner message, ChatEventVideoChatParticipantIsMutedToggled videoChatParticipantIsMutedToggled, bool history)
        {
            var fromUser = message.GetSender();
            var whoUser = message.ClientService.GetMessageSender(videoChatParticipantIsMutedToggled.ParticipantId);

            if (videoChatParticipantIsMutedToggled.IsMuted)
            {
                return ReplaceWithLink(Strings.EventLogVoiceChatMuted, fromUser, whoUser);
            }
            else
            {
                return ReplaceWithLink(Strings.EventLogVoiceChatUnmuted, fromUser, whoUser);
            }
        }

        private static FormattedText UpdateVideoChatParticipantVolumeLevelChanged(MessageWithOwner message, ChatEventVideoChatParticipantVolumeLevelChanged videoChatParticipantVolumeLevelChanged, bool history)
        {
            var fromUser = message.GetSender();
            var whoUser = message.ClientService.GetMessageSender(videoChatParticipantVolumeLevelChanged.ParticipantId);

            return ReplaceWithLink(string.Format(Strings.ActionVolumeChanged, videoChatParticipantVolumeLevelChanged.VolumeLevel), fromUser, whoUser);
        }

        private static FormattedText UpdateChatEventIsForumToggled(MessageWithOwner message, ChatEventIsForumToggled isForumToggled, bool history)
        {
            return ReplaceWithLink(isForumToggled.IsForum
                ? Strings.EventLogSwitchToForum
                : Strings.EventLogSwitchToGroup, message.GetSender());
        }

        private static FormattedText UpdateChatEventForumTopicCreated(MessageWithOwner message, ChatEventForumTopicCreated forumTopicCreated, bool history)
        {
            return ReplaceWithLink(Strings.EventLogCreateTopic, message.GetSender(), forumTopicCreated.TopicInfo);
        }

        private static FormattedText UpdateChatEventForumTopicDeleted(MessageWithOwner message, ChatEventForumTopicDeleted forumTopicDeleted, bool history)
        {
            return ReplaceWithLink(Strings.EventLogDeleteTopic, message.GetSender(), forumTopicDeleted.TopicInfo);
        }

        private static FormattedText UpdateChatEventForumTopicEdited(MessageWithOwner message, ChatEventForumTopicEdited forumTopicEdited, bool history)
        {
            return ReplaceWithLink(Strings.EventLogEditTopic, message.GetSender(), forumTopicEdited.OldTopicInfo, forumTopicEdited.NewTopicInfo);
        }

        private static FormattedText UpdateChatEventForumTopicPinned(MessageWithOwner message, ChatEventForumTopicPinned forumTopicPinned, bool history)
        {
            if (forumTopicPinned.NewTopicInfo != null)
            {
                return ReplaceWithLink(Strings.EventLogPinTopic, message.GetSender(), forumTopicPinned.NewTopicInfo);
            }
            else if (forumTopicPinned.OldTopicInfo != null)
            {
                return ReplaceWithLink(Strings.EventLogUnpinTopic, message.GetSender(), forumTopicPinned.OldTopicInfo);
            }

            return _emptyString;
        }

        private static FormattedText UpdateChatEventForumTopicToggleIsClosed(MessageWithOwner message, ChatEventForumTopicToggleIsClosed forumTopicToggleIsClosed, bool history)
        {
            // TODO
            return _emptyString;
        }

        //private static FormattedText UpdateChatEventActiveUsernames(MessageWithOwner message, ChatEventActiveUsernamesChanged activeUsernamesChanged)
        //{
        //    //var content = string.Empty;
        //    //var entities = active ? new List<TextEntity>() : null;

        //    //var fromUser = message.GetSender();

        //    //content = ReplaceWithLink(isForumToggled.IsForum
        //    //    ? Strings.EventLogSwitchToForum
        //    //    : Strings.EventLogSwitchToGroup, "un1", fromUser, entities);

        //    //return (content, entities);
        //}

        #endregion

        private static FormattedText UpdateBasicGroupChatCreate(MessageWithOwner message, MessageBasicGroupChatCreate basicGroupChatCreate, bool history)
        {
            if (message.IsOutgoing)
            {
                return Strings.ActionYouCreateGroup.AsFormattedText();
            }
            else
            {
                return ReplaceWithLink(Strings.ActionCreateGroup, message.GetSender());
            }
        }

        private static FormattedText UpdateBotWriteAccessAllowed(MessageWithOwner message, MessageBotWriteAccessAllowed botWriteAccessAllowed, bool history)
        {
            if (botWriteAccessAllowed.Reason is BotWriteAccessAllowReasonConnectedWebsite websiteConnected)
            {
                var content = Strings.ActionBotAllowed;
                var entities = new List<TextEntity>();

                var start = content.IndexOf("{0}");
                content = string.Format(content, websiteConnected.DomainName);

                if (start >= 0)
                {
                    entities.Add(new TextEntity(start, websiteConnected.DomainName.Length, new TextEntityTypeUrl()));
                }

                return new FormattedText(content, entities);
            }

            return Strings.ActionBotAllowedWebapp.AsFormattedText();
        }

        private static FormattedText UpdateChatAddMembers(MessageWithOwner message, MessageChatAddMembers chatAddMembers, bool history)
        {
            try
            {
                long singleUserId = 0;
                if (chatAddMembers.MemberUserIds.Count == 1)
                {
                    singleUserId = chatAddMembers.MemberUserIds[0];
                }

                if (singleUserId != 0)
                {
                    if (message.SenderId is MessageSenderUser senderUser && singleUserId == senderUser.UserId)
                    {
                        if (message.Chat.Type is ChatTypeSupergroup { IsChannel: true })
                        {
                            if (singleUserId == message.ClientService.Options.MyId)
                            {
                                return Strings.ChannelJoined.AsFormattedText();
                            }
                            else
                            {
                                return ReplaceWithLink(Strings.EventLogChannelJoined, message.GetSender());
                            }
                        }
                        else if (message.Chat.Type is ChatTypeSupergroup)
                        {
                            if (singleUserId == message.ClientService.Options.MyId)
                            {
                                return Strings.ChannelMegaJoined.AsFormattedText();
                            }
                            else
                            {
                                return ReplaceWithLink(Strings.ActionAddUserSelfMega, message.GetSender());
                            }
                        }
                        else if (message.IsOutgoing)
                        {
                            return Strings.ActionAddUserSelfYou.AsFormattedText();
                        }
                        else
                        {
                            return ReplaceWithLink(Strings.ActionAddUserSelf, message.GetSender());
                        }
                    }
                    else
                    {
                        var whoUser = message.ClientService.GetUser(singleUserId);

                        if (message.IsOutgoing)
                        {
                            return ReplaceWithLink(Strings.ActionYouAddUser, "un2", whoUser);
                        }
                        else if (singleUserId == message.ClientService.Options.MyId)
                        {
                            if (message.Chat?.Type is ChatTypeSupergroup { IsChannel: true })
                            {
                                return ReplaceWithLink(Strings.ChannelAddedBy, message.GetSender());
                            }
                            else if (message.Chat?.Type is ChatTypeSupergroup)
                            {
                                return ReplaceWithLink(Strings.MegaAddedBy, message.GetSender());
                            }
                            else
                            {
                                return ReplaceWithLink(Strings.ActionAddUserYou, message.GetSender());
                            }
                        }
                        else
                        {
                            return ReplaceWithLink(Strings.ActionAddUser, message.GetSender(), whoUser);
                        }
                    }
                }
                else
                {
                    if (message.IsOutgoing)
                    {
                        return ReplaceWithLinks(Strings.ActionYouAddUser, "un2", chatAddMembers.MemberUserIds, message.ClientService);
                    }
                    else
                    {
                        var content = ReplaceWithLink(Strings.ActionAddUser, message.GetSender());
                        return ReplaceWithLinks(content, "un2", chatAddMembers.MemberUserIds, message.ClientService);
                    }
                }
            }
            catch
            {
                Logger.Info(message.Content);
                throw;
            }
        }

        private static FormattedText UpdateChatChangePhoto(MessageWithOwner message, MessageChatChangePhoto chatChangePhoto, bool history)
        {
            if (message.IsChannelPost)
            {
                return chatChangePhoto.Photo.Animation != null
                    ? Strings.ActionChannelChangedVideo.AsFormattedText()
                    : Strings.ActionChannelChangedPhoto.AsFormattedText();
            }
            else
            {
                if (message.IsOutgoing)
                {
                    return chatChangePhoto.Photo.Animation != null
                        ? Strings.ActionYouChangedVideo.AsFormattedText()
                        : Strings.ActionYouChangedPhoto.AsFormattedText();
                }
                else
                {
                    return chatChangePhoto.Photo.Animation != null
                        ? ReplaceWithLink(Strings.ActionChangedVideo, message.GetSender())
                        : ReplaceWithLink(Strings.ActionChangedPhoto, message.GetSender());
                }
            }
        }

        private static FormattedText UpdateChatChangeTitle(MessageWithOwner message, MessageChatChangeTitle chatChangeTitle, bool history)
        {
            if (message.IsChannelPost)
            {
                return ReplaceWithLink(Strings.ActionChannelChangedTitle, "un2", chatChangeTitle.Title);
            }
            else
            {
                if (message.IsOutgoing)
                {
                    return ReplaceWithLink(Strings.ActionYouChangedTitle, "un2", chatChangeTitle.Title);
                }
                else
                {
                    return ReplaceWithLink(Strings.ActionChangedTitle.Replace("un2", chatChangeTitle.Title), message.GetSender());
                }
            }
        }

        private static FormattedText UpdateChatSetTheme(MessageWithOwner message, MessageChatSetTheme chatSetTheme, bool history)
        {
            if (message.IsOutgoing)
            {
                if (chatSetTheme.Theme is ChatThemeEmoji emoji)
                {
                    return string.Format(Strings.ChatThemeChangedYou, emoji.Name).AsFormattedText();
                }
                else if (chatSetTheme.Theme is ChatThemeGift gift)
                {
                    return string.Format(Strings.ChatThemeChangedYou, gift.GiftTheme.Gift.ToName()).AsFormattedText();
                }

                return Strings.ChatThemeDisabledYou.AsFormattedText();
            }
            else
            {
                if (chatSetTheme.Theme is ChatThemeEmoji emoji)
                {
                    return ReplaceWithLink(string.Format(Strings.ChatThemeChangedTo, "un1", emoji.Name), message.GetSender());
                }
                else if (chatSetTheme.Theme is ChatThemeGift gift)
                {
                    return ReplaceWithLink(string.Format(Strings.ChatThemeChangedTo, "un1", gift.GiftTheme.Gift.ToName()), message.GetSender());
                }

                return ReplaceWithLink(string.Format(Strings.ChatThemeDisabled, "un1"), message.GetSender());
            }
        }

        private static FormattedText UpdateChatDeleteMember(MessageWithOwner message, MessageChatDeleteMember chatDeleteMember, bool history)
        {
            if (message.SenderId is MessageSenderUser senderUser && chatDeleteMember.UserId == senderUser.UserId)
            {
                if (message.IsOutgoing)
                {
                    return Strings.ActionYouLeftUser.AsFormattedText();
                }
                else
                {
                    if (message.IsChannelPost)
                    {
                        return ReplaceWithLink(Strings.EventLogLeftChannel, message.GetSender());
                    }
                    else
                    {
                        return ReplaceWithLink(Strings.ActionLeftUser, message.GetSender());
                    }
                }
            }
            else
            {
                var whoUser = message.ClientService.GetUser(chatDeleteMember.UserId);
                if (message.IsOutgoing)
                {
                    return ReplaceWithLink(Strings.ActionYouKickUser, "un2", whoUser);
                }
                else if (chatDeleteMember.UserId == message.ClientService.Options.MyId)
                {
                    return ReplaceWithLink(Strings.ActionKickUserYou, message.GetSender());
                }
                else
                {
                    return ReplaceWithLink(Strings.ActionKickUser, message.GetSender(), whoUser);
                }
            }
        }

        private static FormattedText UpdateChatDeletePhoto(MessageWithOwner message, MessageChatDeletePhoto chatDeletePhoto, bool history)
        {
            if (message.IsChannelPost)
            {
                return Strings.ActionChannelRemovedPhoto.AsFormattedText();
            }
            else if (message.IsOutgoing)
            {
                return Strings.ActionYouRemovedPhoto.AsFormattedText();
            }
            else
            {
                return ReplaceWithLink(Strings.ActionRemovedPhoto, message.GetSender());
            }
        }


        private static FormattedText UpdateChatHasProtectedContentToggled(MessageWithOwner message, MessageChatHasProtectedContentToggled chatHasProtectedContentToggled, bool history)
        {
            if (chatHasProtectedContentToggled.NewHasProtectedContent == chatHasProtectedContentToggled.OldHasProtectedContent)
            {
                return chatHasProtectedContentToggled.NewHasProtectedContent
                    ? Strings.DisableSharingActionStillDisabled.AsFormattedText()
                    : Strings.DisableSharingActionStillEnabled.AsFormattedText();
            }

            if (message.IsOutgoing)
            {
                return chatHasProtectedContentToggled.NewHasProtectedContent
                    ? Strings.DisableSharingActionYou.AsFormattedText()
                    : Strings.EnableSharingActionYou.AsFormattedText();
            }

            return ReplaceWithName(chatHasProtectedContentToggled.NewHasProtectedContent ? Strings.DisableSharingActionOther : Strings.EnableSharingActionOther, message.GetSender());
        }

        private static FormattedText UpdateChatHasProtectedContentDisableRequested(MessageWithOwner message, MessageChatHasProtectedContentDisableRequested chatHasProtectedContentDisableRequested, bool history)
        {
            return message.IsOutgoing
                ? Strings.SharingOfferEnableHeaderYou.AsFormattedText()
                : ClientEx.ParseMarkdown(ReplaceWithName(Strings.SharingOfferEnableHeaderOther, message.GetSender()));
        }

        private static FormattedText UpdateChatJoinByLink(MessageWithOwner message, MessageChatJoinByLink chatJoinByLink, bool history)
        {
            if (message.IsOutgoing)
            {
                return Strings.ActionInviteYou.AsFormattedText();
            }
            else
            {
                return ReplaceWithLink(Strings.ActionInviteUser, message.GetSender());
            }
        }

        private static FormattedText UpdateChatJoinByRequest(MessageWithOwner message, MessageChatJoinByRequest chatJoinByRequest, bool history)
        {
            return ReplaceWithLink(Strings.UserAcceptedToGroupAction, message.GetSender());
        }

        private static FormattedText UpdateChatSetBackground(MessageWithOwner message, MessageChatSetBackground chatSetBackground, bool history)
        {
            if (message.IsChannelPost)
            {
                return Strings.ActionSetWallpaperForThisChannel.AsFormattedText();
            }
            else if (chatSetBackground.OldBackgroundMessageId != 0)
            {
                if (message.IsOutgoing)
                {
                    return Strings.ActionSetSameWallpaperForThisChatSelf.AsFormattedText();
                }
                else if (message.ClientService.TryGetUser(message.SenderId, out User user))
                {
                    return string.Format(Strings.ActionSetSameWallpaperForThisChat, user.FullName(true)).AsFormattedText();
                }
            }
            else if (message.IsOutgoing)
            {
                if (chatSetBackground.OnlyForSelf)
                {
                    return Strings.ActionSetWallpaperForThisChatSelf.AsFormattedText();
                }
                else if (message.ClientService.TryGetUser(message.Chat, out User user))
                {
                    return string.Format(Strings.ActionSetWallpaperForThisChatSelfBoth, user.FullName(true)).AsFormattedText();
                }
            }
            else if (message.ClientService.TryGetUser(message.SenderId, out User user))
            {
                return chatSetBackground.OnlyForSelf
                    ? string.Format(Strings.ActionSetWallpaperForThisChat, user.FullName(true)).AsFormattedText()
                    : string.Format(Strings.ActionSetWallpaperForThisChatBoth, user.FullName(true)).AsFormattedText();
            }
            else
            {
                return Strings.ActionSetWallpaperForThisGroup.AsFormattedText();
            }

            return _emptyString;
        }

        private static FormattedText UpdateChatSetMessageAutoDeleteTime(MessageWithOwner message, MessageChatSetMessageAutoDeleteTime chatSetMessageAutoDeleteTime, bool history)
        {
            var chat = message.Chat;
            if (chat?.Type is ChatTypeSecret)
            {
                if (chatSetMessageAutoDeleteTime.MessageAutoDeleteTime != 0)
                {
                    if (message.IsOutgoing)
                    {
                        return string.Format(Strings.MessageLifetimeChangedOutgoing, Locale.FormatTtl(chatSetMessageAutoDeleteTime.MessageAutoDeleteTime)).AsFormattedText();
                    }
                    else
                    {
                        return ReplaceWithLink(string.Format(Strings.MessageLifetimeChanged, "un1", Locale.FormatTtl(chatSetMessageAutoDeleteTime.MessageAutoDeleteTime)), message.GetSender());
                    }
                }
                else
                {
                    if (message.IsOutgoing)
                    {
                        return Strings.MessageLifetimeYouRemoved.AsFormattedText();
                    }
                    else
                    {
                        return ReplaceWithLink(string.Format(Strings.MessageLifetimeRemoved, "un1"), message.GetSender());
                    }
                }
            }
            else if (message.IsChannelPost)
            {
                if (chatSetMessageAutoDeleteTime.MessageAutoDeleteTime != 0)
                {
                    return string.Format(Strings.ActionTTLChannelChanged, Locale.FormatTtl(chatSetMessageAutoDeleteTime.MessageAutoDeleteTime)).AsFormattedText();
                }
                else
                {
                    return Strings.ActionTTLChannelDisabled.AsFormattedText();
                }
            }
            else
            {
                if (chatSetMessageAutoDeleteTime.MessageAutoDeleteTime != 0)
                {
                    if (chatSetMessageAutoDeleteTime.FromUserId == message.ClientService.Options.MyId)
                    {
                        return string.Format(Strings.AutoDeleteGlobalActionFromYou, Locale.FormatTtl(chatSetMessageAutoDeleteTime.MessageAutoDeleteTime)).AsFormattedText();
                    }
                    else if (chatSetMessageAutoDeleteTime.FromUserId != 0 && message.ClientService.TryGetUser(chatSetMessageAutoDeleteTime.FromUserId, out User fromUser))
                    {
                        return ReplaceWithLink(string.Format(Strings.AutoDeleteGlobalAction, Locale.FormatTtl(chatSetMessageAutoDeleteTime.MessageAutoDeleteTime)), fromUser);
                    }
                    else if (message.IsOutgoing)
                    {
                        return string.Format(Strings.ActionTTLYouChanged, Locale.FormatTtl(chatSetMessageAutoDeleteTime.MessageAutoDeleteTime)).AsFormattedText();
                    }
                    else
                    {
                        return ReplaceWithLink(string.Format(Strings.ActionTTLChanged, Locale.FormatTtl(chatSetMessageAutoDeleteTime.MessageAutoDeleteTime)), message.GetSender());
                    }
                }
                else
                {
                    if (message.IsOutgoing)
                    {
                        return Strings.ActionTTLYouDisabled.AsFormattedText();
                    }
                    else
                    {
                        return ReplaceWithLink(Strings.ActionTTLDisabled, message.GetSender());
                    }
                }
            }
        }

        private static FormattedText UpdateChatUpgradeFrom(MessageWithOwner message, MessageChatUpgradeFrom chatUpgradeFrom, bool history)
        {
            return (history ? Strings.GroupUpgradedFrom : Strings.GroupUpgradedTo).AsFormattedText();
        }

        private static FormattedText UpdateChatUpgradeTo(MessageWithOwner message, MessageChatUpgradeTo chatUpgradeTo, bool history)
        {
            return Strings.GroupUpgradedTo.AsFormattedText();
        }

        private static FormattedText UpdateContactRegistered(MessageWithOwner message, MessageContactRegistered contactRegistered, bool history)
        {
            if (message.ClientService.TryGetUser(message.SenderId, out User senderUser))
            {
                return string.Format(Strings.NotificationContactJoined, senderUser.FullName()).AsFormattedText();
            }

            return _emptyString;
        }

        private static FormattedText UpdateCustomServiceAction(MessageWithOwner message, MessageCustomServiceAction customServiceAction, bool history)
        {
            return customServiceAction.Text.AsFormattedText();
        }

        private static FormattedText UpdateForumTopicCreated(MessageWithOwner message, MessageForumTopicCreated forumTopicCreated, bool history)
        {
            // TopicWasCreatedAction
            // TopicCreated
            var content = string.Empty;
            var entities = new List<TextEntity>();

            if (true)
            {
                var topicName = new FormattedText($"\U0001F4C3 {forumTopicCreated.Name}", new[]
                {
                    new TextEntity(0, 2, new TextEntityTypeCustomEmoji(forumTopicCreated.Icon.CustomEmojiId))
                });

                return ClientEx.Format(Strings.TopicWasCreatedAction, topicName);
            }
            else
            {
                content = Strings.TopicCreated;
            }

            return new FormattedText(content, entities);
        }

        private static FormattedText UpdateForumTopicEdited(MessageWithOwner message, MessageForumTopicEdited forumTopicEdited, bool history)
        {
            // TopicWasIconChangedToAction, TopicWasRenamedToAction TopicWasRenamedToAction2
            // TopicIconChangedToAction, TopicRenamedToAction
            FormattedText content;

            if (forumTopicEdited.EditIconCustomEmojiId && forumTopicEdited.Name.Length > 0)
            {
                content = ReplaceWithLink(string.Format(Strings.TopicWasRenamedToAction2, "un1", $"\U0001F4C3 {forumTopicEdited.Name}"), message.GetSender());
            }
            else if (forumTopicEdited.EditIconCustomEmojiId)
            {
                content = ReplaceWithLink(string.Format(Strings.TopicWasIconChangedToAction, "un1", "\U0001F4C3"), message.GetSender());
            }
            else
            {
                content = ReplaceWithLink(string.Format(Strings.TopicWasRenamedToAction, "un1", forumTopicEdited.Name), message.GetSender());
            }

            var index = content.Text.IndexOf("\U0001F4C3");
            if (index != -1)
            {
                content.Entities.Add(new TextEntity(index, 2, new TextEntityTypeCustomEmoji(forumTopicEdited.IconCustomEmojiId)));
            }

            return content;
        }

        private static FormattedText UpdateForumTopicIsClosedToggled(MessageWithOwner message, MessageForumTopicIsClosedToggled forumTopicIsClosedToggled, bool history)
        {
            // TopicWasClosedAction, TopicWasReopenedAction
            // TopicClosed2, TopicRestarted2

            var content = string.Format(forumTopicIsClosedToggled.IsClosed
                ? Strings.TopicClosed2
                : Strings.TopicRestarted2, "un1");
            return ReplaceWithLink(content, message.GetSender());
        }

        private static FormattedText UpdateForumTopicIsHiddenToggled(MessageWithOwner message, MessageForumTopicIsHiddenToggled forumTopicIsHiddenToggled, bool history)
        {
            return ReplaceWithLink(forumTopicIsHiddenToggled.IsHidden
                ? Strings.TopicHidden2
                : Strings.TopicShown2, message.GetSender());
        }

        private static FormattedText UpdateGameScore(MessageWithOwner message, MessageGameScore gameScore, bool history)
        {
            var game = GetGame(message as MessageViewModel);
            if (game == null)
            {
                if (message.ClientService.TryGetUser(message.SenderId, out User senderUser))
                {
                    if (senderUser.Id == message.ClientService.Options.MyId)
                    {
                        return string.Format(Strings.ActionYouScored, Locale.Declension(Strings.R.Points, gameScore.Score)).AsFormattedText();
                    }
                    else
                    {
                        return ReplaceWithLink(string.Format(Strings.ActionUserScored, Locale.Declension(Strings.R.Points, gameScore.Score)), senderUser);
                    }
                }
            }
            else
            {
                if (message.ClientService.TryGetUser(message.SenderId, out User senderUser))
                {
                    if (senderUser.Id == message.ClientService.Options.MyId)
                    {
                        return ReplaceWithLink(string.Format(Strings.ActionYouScoredInGame, Locale.Declension(Strings.R.Points, gameScore.Score)), "un2", game);
                    }
                    else
                    {
                        return ReplaceWithLink(string.Format(Strings.ActionUserScoredInGame, Locale.Declension(Strings.R.Points, gameScore.Score)), senderUser, game);
                    }
                }
            }

            return _emptyString;
        }

        private static FormattedText UpdateGift(MessageWithOwner message, MessageGift gift, bool history)
        {
            // TODO: markdown

            if (message.ChatId == message.ClientService.Options.MyId)
            {
                return ReplaceWithLink(Strings.ActionGiftSelf, "un2", gift);
            }
            if (message.IsOutgoing)
            {
                if (gift.IsPrepaidUpgrade && message.ClientService.TryGetMessageSender(gift.ReceiverId, out Object receiver))
                {
                    return ReplaceWithLink(Strings.ActionPrepaidGiftOutbound, receiver, gift);
                }

                return ReplaceWithLink(Strings.ActionGiftOutbound, "un2", gift);
            }
            else if (message.ClientService.TryGetMessageSender(gift.SenderId, out Object sender))
            {
                if (gift.ReceiverId.IsUser(message.ClientService.Options.MyId))
                {
                    if (gift.IsPrepaidUpgrade)
                    {
                        return ReplaceWithLink(Strings.ActionPrepaidGiftInbound, sender, gift);
                    }

                    return ReplaceWithLink(Strings.ActionGiftInbound, sender, gift);
                }
                else if (message.ClientService.TryGetMessageSender(gift.ReceiverId, out Object outboundUser))
                {
                    return ReplaceWithLink(Locale.Declension(Strings.R.ActionGiftChannel, gift.Gift.StarCount + gift.PrepaidUpgradeStarCount), sender, outboundUser);
                }
            }
            else
            {
                return ReplaceWithLink(Strings.ActionGift2Received, "un2", gift);
            }

            return _emptyString;
        }

        private static FormattedText UpdateGiftedPremium(MessageWithOwner message, MessageGiftedPremium giftedPremium, bool history)
        {
            // TODO: markdown

            if (message.IsOutgoing)
            {
                return ReplaceWithLink(Strings.ActionGiftOutbound, "un2", giftedPremium);
            }
            else if (message.ChatId == message.ClientService.Options.TelegramServiceNotificationsChatId)
            {
                return ReplaceWithLink(Strings.ActionGift2Received, "un2", giftedPremium);
            }
            else
            {
                return ReplaceWithLink(Strings.ActionGiftInbound, message.GetSender(), giftedPremium);
            }
        }

        private static FormattedText UpdateGiftedStars(MessageWithOwner message, MessageGiftedStars giftedStars, bool history)
        {
            // TODO: markdown

            if (message.IsOutgoing)
            {
                return ReplaceWithLink(Strings.ActionGiftOutbound, "un2", giftedStars);
            }
            else if (message.ClientService.TryGetUser(giftedStars.GifterUserId, out User senderUser))
            {
                return ReplaceWithLink(Strings.ActionGiftInbound, senderUser, giftedStars);
            }
            else
            {
                return ReplaceWithLink(Strings.ActionGiftInbound, Strings.StarsTransactionUnknown, giftedStars);
            }
        }

        private static FormattedText UpdateVideoChatEnded(MessageWithOwner message, MessageVideoChatEnded videoChatEnded, bool history)
        {
            if (message.IsOutgoing)
            {
                return string.Format(Strings.ActionGroupCallEndedByYou, videoChatEnded.GetDuration()).AsFormattedText();
            }
            else if (message.ClientService.TryGetUser(message.SenderId, out User senderUser))
            {
                return ReplaceWithLink(string.Format(Strings.ActionGroupCallEndedBy, videoChatEnded.GetDuration()), senderUser);
            }
            else
            {
                return string.Format(Strings.ActionGroupCallEnded, videoChatEnded.GetDuration()).AsFormattedText();
            }
        }

        private static FormattedText UpdateVideoChatScheduled(MessageWithOwner message, MessageVideoChatScheduled videoChatScheduled, bool history)
        {
            if (message.IsChannelPost)
            {
                return string.Format(Strings.ActionChannelCallScheduled, videoChatScheduled.GetStartsAt()).AsFormattedText();
            }
            else
            {
                return string.Format(Strings.ActionGroupCallScheduled, videoChatScheduled.GetStartsAt()).AsFormattedText();
            }
        }

        private static FormattedText UpdateVideoChatStarted(MessageWithOwner message, MessageVideoChatStarted videoChatStarted, bool history)
        {
            if (message.IsChannelPost)
            {
                return Strings.ActionChannelCallJustStarted.AsFormattedText();
            }
            else if (message.SenderId.IsUser(message.ClientService.Options.MyId))
            {
                return Strings.ActionGroupCallStartedByYou.AsFormattedText();
            }
            else
            {
                return ReplaceWithLink(Strings.ActionGroupCallStarted, message.GetSender());
            }
        }

        private static FormattedText UpdateInviteVideoChatParticipants(MessageWithOwner message, MessageInviteVideoChatParticipants inviteVideoChatParticipants, bool history)
        {
            long singleUserId = 0;
            if (singleUserId == 0 && inviteVideoChatParticipants.UserIds.Count == 1)
            {
                singleUserId = inviteVideoChatParticipants.UserIds[0];
            }

            if (singleUserId != 0)
            {
                var whoUser = message.ClientService.GetUser(singleUserId);
                if (message.IsOutgoing)
                {
                    return ReplaceWithLink(Strings.ActionGroupCallYouInvited, "un2", whoUser);
                }
                else if (singleUserId == message.ClientService.Options.MyId)
                {
                    return ReplaceWithLink(Strings.ActionGroupCallInvitedYou, message.GetSender());
                }
                else
                {
                    return ReplaceWithLink(Strings.ActionGroupCallInvited, message.GetSender(), whoUser);
                }
            }
            else
            {
                if (message.IsOutgoing)
                {
                    return ReplaceWithLinks(Strings.ActionGroupCallYouInvited, "un2", inviteVideoChatParticipants.UserIds, message.ClientService);
                }
                else
                {
                    var content = ReplaceWithLink(Strings.ActionGroupCallInvited, message.GetSender());
                    return ReplaceWithLinks(content, "un2", inviteVideoChatParticipants.UserIds, message.ClientService);
                }
            }
        }

        private static FormattedText UpdateProximityAlertTriggered(MessageWithOwner message, MessageProximityAlertTriggered proximityAlertTriggered, bool history)
        {
            message.ClientService.TryGetUser(proximityAlertTriggered.TravelerId, out User traveler);
            message.ClientService.TryGetUser(proximityAlertTriggered.WatcherId, out User watcher);

            if (traveler != null && watcher != null)
            {
                if (traveler.Id == message.ClientService.Options.MyId)
                {
                    return ReplaceWithLink(string.Format(Strings.ActionUserWithinYouRadius, Formatter.Distance(proximityAlertTriggered.Distance, false)), watcher);
                }
                else if (watcher.Id == message.ClientService.Options.MyId)
                {
                    return ReplaceWithLink(string.Format(Strings.ActionUserWithinRadius, Formatter.Distance(proximityAlertTriggered.Distance, false)), traveler);
                }
                else
                {
                    return ReplaceWithLink(string.Format(Strings.ActionUserWithinOtherRadius, Formatter.Distance(proximityAlertTriggered.Distance, false)), traveler, watcher);
                }
            }

            return _emptyString;
        }

        private static FormattedText UpdateGiveawayCreated(MessageWithOwner message, MessageGiveawayCreated giveawayCreated, bool history)
        {
            if (giveawayCreated.StarCount > 0)
            {
                return Locale.Declension(message.IsChannelPost
                    ? Strings.R.BoostingStarsGiveawayJustStarted
                    : Strings.R.BoostingStarsGiveawayJustStartedGroup, giveawayCreated.StarCount, message.Chat.Title).AsFormattedText();
            }
            else
            {
                return string.Format(message.IsChannelPost
                    ? Strings.BoostingGiveawayJustStarted
                    : Strings.BoostingGiveawayJustStartedGroup, message.Chat.Title).AsFormattedText();
            }
        }

        private static FormattedText UpdateGiveawayCompleted(MessageWithOwner message, MessageGiveawayCompleted giveawayCompleted, bool history)
        {
            var content = Locale.Declension(Strings.R.BoostingGiveawayServiceWinnersSelected, giveawayCompleted.WinnerCount);

            if (giveawayCompleted.UnclaimedPrizeCount > 0)
            {
                content = string.Format("{0} {1}", content, Locale.Declension(Strings.R.BoostingGiveawayServiceUndistributed, giveawayCompleted.UnclaimedPrizeCount));
            }

            return content.AsFormattedText();
        }

        private static FormattedText UpdateGiveawayPrizeStars(MessageWithOwner message, MessageGiveawayPrizeStars giveawayPrizeStars, bool history)
        {
            var boostedChat = message.ClientService.GetChat(giveawayPrizeStars.BoostedChatId);

            var content = Locale.Declension(Strings.R.ActionStarGiveawayPrize, giveawayPrizeStars.StarCount);
            return ReplaceWithLink(content, boostedChat);
        }

        private static FormattedText UpdatePremiumGiftCode(MessageWithOwner message, MessagePremiumGiftCode premiumGiftCode, bool history)
        {
            // TODO: parse markdown
            if (message.IsOutgoing)
            {
                return ReplaceWithLink(Strings.ActionGiftOutbound, "un2", premiumGiftCode);
            }
            else if (message.ChatId == message.ClientService.Options.TelegramServiceNotificationsChatId)
            {
                if (premiumGiftCode.Amount > 0)
                {
                    return ReplaceWithLink(Strings.ActionGift2Received, "un2", premiumGiftCode);
                }
                else
                {
                    return Strings.BoostingReceivedGiftNoName.AsFormattedText();
                }
            }
            else
            {
                return ReplaceWithLink(Strings.ActionGiftInbound, message.GetSender(), premiumGiftCode);
            }
        }

        private static FormattedText UpdateDirectMessagePriceChanged(MessageWithOwner message, MessageDirectMessagePriceChanged directMessagePriceChanged, bool history)
        {
            if (directMessagePriceChanged.IsEnabled)
            {
                if (directMessagePriceChanged.PaidMessageStarCount > 0)
                {
                    return ReplaceWithLink(Locale.Declension(Strings.R.PostSuggestionsPriceUpdated, directMessagePriceChanged.PaidMessageStarCount), message.GetSender());
                }
                else
                {
                    return ReplaceWithLink(Strings.PostSuggestionsEnabledUpdated, message.GetSender());
                }
            }
            else
            {
                return ReplaceWithLink(Strings.PostSuggestionsDisabledUpdated, message.GetSender());
            }
        }

        private static FormattedText UpdatePaidMessagePriceChanged(MessageWithOwner message, MessagePaidMessagePriceChanged paidMessagePriceChanged, bool history)
        {
            if (message.IsOutgoing)
            {
                return Locale.Declension(Strings.R.PaidMessagesPriceUpdatedOut, paidMessagePriceChanged.PaidMessageStarCount).AsFormattedText();
            }
            else
            {
                return ReplaceWithLink(Locale.Declension(Strings.R.PaidMessagesPriceUpdated, paidMessagePriceChanged.PaidMessageStarCount), message.GetSender());
            }
        }

        private static FormattedText UpdatePaidMessagesRefunded(MessageWithOwner message, MessagePaidMessagesRefunded paidMessagesRefunded, bool history)
        {
            if (message.IsOutgoing && message.ClientService.TryGetUser(message.Chat, out User receiverUser))
            {
                var content = Locale.Declension(Strings.R.PaidMessagesRefundedOut, paidMessagesRefunded.StarCount);
                return ReplaceWithLink(content, receiverUser);
            }
            else if (message.ClientService.TryGetMessageSender(message.SenderId, out Object senderUser))
            {
                var content = Locale.Declension(Strings.R.PaidMessagesRefunded, paidMessagesRefunded.StarCount);
                return ReplaceWithLink(content, senderUser);
            }

            return _emptyString;
        }

        private static FormattedText UpdatePassportDataSent(MessageWithOwner message, MessagePassportDataSent passportDataSent, bool history)
        {
            string content;

            StringBuilder str = new();
            for (int a = 0, size = passportDataSent.Types.Count; a < size; a++)
            {
                var type = passportDataSent.Types[a];
                if (str.Length > 0)
                {
                    str.Append(", ");
                }
                if (type is PassportElementTypePhoneNumber)
                {
                    str.Append(Strings.ActionBotDocumentPhone);
                }
                else if (type is PassportElementTypeEmailAddress)
                {
                    str.Append(Strings.ActionBotDocumentEmail);
                }
                else if (type is PassportElementTypeAddress)
                {
                    str.Append(Strings.ActionBotDocumentAddress);
                }
                else if (type is PassportElementTypePersonalDetails)
                {
                    str.Append(Strings.ActionBotDocumentIdentity);
                }
                else if (type is PassportElementTypePassport)
                {
                    str.Append(Strings.ActionBotDocumentPassport);
                }
                else if (type is PassportElementTypeDriverLicense)
                {
                    str.Append(Strings.ActionBotDocumentDriverLicence);
                }
                else if (type is PassportElementTypeIdentityCard)
                {
                    str.Append(Strings.ActionBotDocumentIdentityCard);
                }
                else if (type is PassportElementTypeUtilityBill)
                {
                    str.Append(Strings.ActionBotDocumentUtilityBill);
                }
                else if (type is PassportElementTypeBankStatement)
                {
                    str.Append(Strings.ActionBotDocumentBankStatement);
                }
                else if (type is PassportElementTypeRentalAgreement)
                {
                    str.Append(Strings.ActionBotDocumentRentalAgreement);
                }
                else if (type is PassportElementTypeInternalPassport)
                {
                    str.Append(Strings.ActionBotDocumentInternalPassport);
                }
                else if (type is PassportElementTypePassportRegistration)
                {
                    str.Append(Strings.ActionBotDocumentPassportRegistration);
                }
                else if (type is PassportElementTypeTemporaryRegistration)
                {
                    str.Append(Strings.ActionBotDocumentTemporaryRegistration);
                }
            }

            var chat = message.Chat;
            content = string.Format(Strings.ActionBotDocuments, chat?.Title ?? string.Empty, str.ToString());

            return content.AsFormattedText();
        }

        private static FormattedText UpdatePaymentSuccessful(MessageWithOwner message, MessagePaymentSuccessful paymentSuccessful, bool history)
        {
            var content = string.Empty;

            var invoice = GetInvoice(message as MessageViewModel);
            var chat = message.Chat;

            if (invoice != null)
            {
                return string.Format(Strings.PaymentSuccessfullyPaid, Locale.FormatCurrency(paymentSuccessful.TotalAmount, paymentSuccessful.Currency), message.ClientService.GetTitle(chat), invoice.ProductInfo.Title).AsFormattedText();
            }
            else
            {
                return string.Format(Strings.PaymentSuccessfullyPaidNoItem, Locale.FormatCurrency(paymentSuccessful.TotalAmount, paymentSuccessful.Currency), message.ClientService.GetTitle(chat)).AsFormattedText();
            }
        }

        private static FormattedText UpdatePaymentRefunded(MessageWithOwner message, MessagePaymentRefunded paymentRefunded, bool history)
        {
            return ReplaceWithLink(string.Format(Strings.ActionRefunded, Locale.FormatCurrency(paymentRefunded.TotalAmount, paymentRefunded.Currency)), message.GetSender());
        }

        private static FormattedText UpdatePinMessage(MessageWithOwner message, MessagePinMessage pinMessage, bool history)
        {
            if (message is MessageViewModel { ReplyToItem: MessageViewModel reply })
            {
                if (reply.Content is MessageAnimatedEmoji animatedEmoji)
                {
                    if (animatedEmoji.AnimatedEmoji.Sticker?.FullType is StickerFullTypeCustomEmoji customEmoji)
                    {
                        var emoji = new FormattedText(animatedEmoji.Emoji, new[] { new TextEntity(0, animatedEmoji.Emoji.Length, new TextEntityTypeCustomEmoji(customEmoji.CustomEmojiId)) });
                        return ReplaceWithLink(ClientEx.Format(Strings.ActionPinnedText, emoji), message.GetSender());
                    }

                    return ReplaceWithLink(string.Format(Strings.ActionPinnedText, animatedEmoji.Emoji), message.GetSender());
                }
                else if (reply.Content is MessageAudio)
                {
                    return ReplaceWithLink(Strings.ActionPinnedMusic, message.GetSender());
                }
                else if (reply.Content is MessageVideo)
                {
                    return ReplaceWithLink(Strings.ActionPinnedVideo, message.GetSender());
                }
                else if (reply.Content is MessageAnimation)
                {
                    return ReplaceWithLink(Strings.ActionPinnedGif, message.GetSender());
                }
                else if (reply.Content is MessageVoiceNote)
                {
                    return ReplaceWithLink(Strings.ActionPinnedVoice, message.GetSender());
                }
                else if (reply.Content is MessageVideoNote)
                {
                    return ReplaceWithLink(Strings.ActionPinnedRound, message.GetSender());
                }
                else if (reply.Content is MessageSticker)
                {
                    return ReplaceWithLink(Strings.ActionPinnedSticker, message.GetSender());
                }
                else if (reply.Content is MessageDocument)
                {
                    return ReplaceWithLink(Strings.ActionPinnedFile, message.GetSender());
                }
                else if (reply.Content is MessageLiveLocation)
                {
                    return ReplaceWithLink(Strings.ActionPinnedGeoLive, message.GetSender());
                }
                else if (reply.Content is MessageLocation)
                {
                    return ReplaceWithLink(Strings.ActionPinnedGeo, message.GetSender());
                }
                else if (reply.Content is MessageVenue)
                {
                    return ReplaceWithLink(Strings.ActionPinnedGeo, message.GetSender());
                }
                else if (reply.Content is MessageContact)
                {
                    return ReplaceWithLink(Strings.ActionPinnedContact, message.GetSender());
                }
                else if (reply.Content is MessagePhoto)
                {
                    return ReplaceWithLink(Strings.ActionPinnedPhoto, message.GetSender());
                }
                else if (reply.Content is MessagePoll poll)
                {
                    if (poll.Poll.Type is PollTypeRegular)
                    {
                        return ReplaceWithLink(Strings.ActionPinnedPoll, message.GetSender());
                    }
                    else if (poll.Poll.Type is PollTypeQuiz)
                    {
                        return ReplaceWithLink(Strings.ActionPinnedQuiz, message.GetSender());
                    }
                }
                else if (reply.Content is MessageGame game)
                {
                    return ReplaceWithLink(string.Format(Strings.ActionPinnedGame, "\uD83C\uDFAE " + game.Game.Title), message.GetSender());
                }
                else if (reply.Content is MessageRichMessage richMessage)
                {
                    var mess = richMessage.Message.ToFormattedText().Replace("\n", " ");
                    if (mess.Text.Length > 20)
                    {
                        mess = TdExtensions.Concat(mess.Substring(0, 20), "...".AsFormattedText());
                    }

                    return ReplaceWithLink(ClientEx.Format(Strings.ActionPinnedText, mess), message.GetSender());
                }
                else if (reply.Content is MessageText text)
                {
                    var mess = text.Text.Clone().Replace("\n", " ");
                    if (mess.Text.Length > 20)
                    {
                        mess = TdExtensions.Concat(mess.Substring(0, 20), "...".AsFormattedText());
                    }

                    return ReplaceWithLink(ClientEx.Format(Strings.ActionPinnedText, mess), message.GetSender());
                }
                else
                {
                    return ReplaceWithLink(Strings.ActionPinnedNoText, message.GetSender());
                }
            }
            else
            {
                return ReplaceWithLink(Strings.ActionPinnedNoText, message.GetSender());
            }

            return _emptyString;
        }

        private static FormattedText UpdateScreenshotTaken(MessageWithOwner message, MessageScreenshotTaken screenshotTaken, bool history)
        {
            if (message.IsOutgoing)
            {
                return Strings.ActionTakeScreenshootYou.AsFormattedText();
            }
            else
            {
                return ReplaceWithLink(Strings.ActionTakeScreenshoot, message.GetSender());
            }
        }

        private static FormattedText UpdateSuggestBirthdate(MessageWithOwner message, MessageSuggestBirthdate suggestBirthdate, bool history)
        {
            if (message.IsOutgoing)
            {
                return Strings.ActionYouSuggestBirthday.AsFormattedText();
            }
            else
            {
                return ReplaceWithLink(Strings.ActionSuggestBirthday, message.GetSender());
            }
        }

        private static FormattedText UpdateSuggestProfilePhoto(MessageWithOwner message, MessageSuggestProfilePhoto suggestProfilePhoto, bool history)
        {
            var content = string.Empty;
            var entities = new List<TextEntity>();

            if (message.IsOutgoing)
            {
                if (message.ClientService.TryGetUser(message.Chat, out User user))
                {
                    content = string.Format(Strings.ActionSuggestPhotoFromYouDescription, user.FirstName);
                    entities?.Add(new TextEntity(Strings.ActionSuggestPhotoFromYouDescription.IndexOf("{0}"), user.FirstName.Length, new TextEntityTypeBold()));
                }
            }
            else
            {
                if (message.ClientService.TryGetUser(message.SenderId, out User user))
                {
                    content = string.Format(Strings.ActionSuggestPhotoToYouDescription, user.FirstName);
                    entities?.Add(new TextEntity(Strings.ActionSuggestPhotoToYouDescription.IndexOf("{0}"), user.FirstName.Length, new TextEntityTypeBold()));
                }
            }

            return new FormattedText(content, entities);
        }

        private static FormattedText UpdateSupergroupChatCreate(MessageWithOwner message, MessageSupergroupChatCreate supergroupChatCreate, bool history)
        {
            if (message.IsChannelPost)
            {
                return Strings.ActionCreateChannel.AsFormattedText();
            }
            else
            {
                return Strings.ActionCreateMega.AsFormattedText();
            }
        }

        private static FormattedText UpdateUpgradedGift(MessageWithOwner message, MessageUpgradedGift upgradedGift, bool history)
        {
            if (upgradedGift.Origin is UpgradedGiftOriginUpgrade)
            {
                if (upgradedGift.ReceiverId.IsUser(message.ClientService.Options.MyId))
                {
                    if (!upgradedGift.ReceiverId.AreTheSame(upgradedGift.SenderId) && message.ClientService.TryGetMessageSender(upgradedGift.SenderId, out Object outboundUser))
                    {
                        return ReplaceWithLink(Strings.ActionUniqueGiftUpgradeOutbound, outboundUser);
                    }
                    else
                    {
                        return Strings.ActionUniqueGiftUpgradeSelf.AsFormattedText();
                    }
                }
                else if (message.ClientService.TryGetMessageSender(upgradedGift.ReceiverId, out Object inboundUser))
                {
                    return ReplaceWithLink(Strings.ActionUniqueGiftUpgradeInbound, inboundUser);
                }
            }
            else if (upgradedGift.ReceiverId.IsUser(message.ClientService.Options.MyId))
            {
                if (message.ClientService.TryGetMessageSender(upgradedGift.SenderId, out Object inboundUser))
                {
                    return ReplaceWithLink(Strings.ActionUniqueGiftTransferInbound, inboundUser);
                }
            }
            else if (message.IsOutgoing)
            {
                return ReplaceWithLink(Strings.ActionUniqueGiftTransferOutbound, message.ClientService.GetMessageSender(upgradedGift.ReceiverId));
            }
            else if (message.ClientService.TryGetMessageSender(upgradedGift.ReceiverId, out Object outboundUser)
                && message.ClientService.TryGetMessageSender(upgradedGift.SenderId, out Object inboundUser))
            {
                return ReplaceWithLink(Strings.ActionUniqueGiftTransferService, inboundUser, outboundUser);
            }

            return _emptyString;
        }

        private static FormattedText UpdateUpgradedGiftPurchaseOffer(MessageWithOwner message, MessageUpgradedGiftPurchaseOffer upgradedGift, bool history)
        {
            message.ClientService.TryGetUser(message.Chat, out User user);

            var content = string.Empty;

            if (upgradedGift.Price is GiftResalePriceStar resalePriceStar)
            {
                if (message.IsOutgoing)
                {
                    content = string.Format(Strings.GiftOfferOfferedTextStarsOut, user.FullName(true), resalePriceStar.StarCount.ToString("N0"), upgradedGift.Gift.ToName());
                }
                else
                {
                    content = string.Format(Strings.GiftOfferOfferedTextStars, user.FullName(true), resalePriceStar.StarCount.ToString("N0"), upgradedGift.Gift.ToName());
                }
            }
            else if (upgradedGift.Price is GiftResalePriceTon resalePriceTon)
            {
                if (message.IsOutgoing)
                {
                    content = string.Format(Strings.GiftOfferOfferedTextTONOut, user.FullName(true), resalePriceTon.ToncoinCentCount, upgradedGift.Gift.ToName());
                }
                else
                {
                    content = string.Format(Strings.GiftOfferOfferedTextTON, user.FullName(true), resalePriceTon.ToncoinCentCount, upgradedGift.Gift.ToName());
                }
            }

            if (history)
            {
                if (upgradedGift.State is GiftPurchaseOfferStatePending)
                {
                    var now = DateTime.Now.ToTimestamp();
                    if (now >= upgradedGift.ExpirationDate)
                    {
                        content += "\n\n" + Strings.GiftOfferStatusExpired;
                    }
                    else
                    {
                        content += "\n\n" + string.Format(Strings.GiftOfferStatusPending, Formatter.ShortDuration(upgradedGift.ExpirationDate - now));
                    }
                }
                else if (upgradedGift.State is GiftPurchaseOfferStateAccepted)
                {
                    content += "\n\n" + Strings.GiftOfferStatusAccepted;
                }
                else if (upgradedGift.State is GiftPurchaseOfferStateRejected)
                {
                    content += "\n\n" + Strings.GiftOfferStatusRejected;
                }
            }

            return ClientEx.ParseMarkdown(content);
        }

        private static FormattedText UpdateUpgradedGiftPurchaseOfferRejected(MessageWithOwner message, MessageUpgradedGiftPurchaseOfferRejected upgradedGift, bool history)
        {
            message.ClientService.TryGetUser(message.Chat, out User user);

            if (upgradedGift.WasExpired)
            {
                if (upgradedGift.Price is GiftResalePriceStar resalePriceStar)
                {
                    if (message.IsOutgoing)
                    {
                        return ClientEx.ParseMarkdown(string.Format(Strings.GiftOfferOfferedTextStarsRejectedOut, user.FullName(true), upgradedGift.Gift.ToName(), resalePriceStar.StarCount.ToString("N0")));
                    }
                    else
                    {
                        return ClientEx.ParseMarkdown(string.Format(Strings.GiftOfferOfferedTextStarsRejected, user.FullName(true), resalePriceStar.StarCount.ToString("N0"), upgradedGift.Gift.ToName()));
                    }
                }
                else if (upgradedGift.Price is GiftResalePriceTon resalePriceTon)
                {
                    if (message.IsOutgoing)
                    {
                        return ClientEx.ParseMarkdown(string.Format(Strings.GiftOfferOfferedTextTONRejectedOut, user.FullName(true), upgradedGift.Gift.ToName(), resalePriceTon.ToncoinCentCount));
                    }
                    else
                    {
                        return ClientEx.ParseMarkdown(string.Format(Strings.GiftOfferOfferedTextTONRejected, user.FullName(true), resalePriceTon.ToncoinCentCount, upgradedGift.Gift.ToName()));
                    }
                }
            }
            else
            {
                if (upgradedGift.Price is GiftResalePriceStar resalePriceStar)
                {
                    if (message.IsOutgoing)
                    {
                        return ClientEx.ParseMarkdown(string.Format(Strings.GiftOfferOfferedTextStarsRejectedOut, user.FullName(true), upgradedGift.Gift.ToName(), resalePriceStar.StarCount.ToString("N0")));
                    }
                    else
                    {
                        return ClientEx.ParseMarkdown(string.Format(Strings.GiftOfferOfferedTextStarsRejected, user.FullName(true), resalePriceStar.StarCount.ToString("N0"), upgradedGift.Gift.ToName()));
                    }
                }
                else if (upgradedGift.Price is GiftResalePriceTon resalePriceTon)
                {
                    if (message.IsOutgoing)
                    {
                        return ClientEx.ParseMarkdown(string.Format(Strings.GiftOfferOfferedTextTONRejectedOut, user.FullName(true), upgradedGift.Gift.ToName(), resalePriceTon.ToncoinCentCount));
                    }
                    else
                    {
                        return ClientEx.ParseMarkdown(string.Format(Strings.GiftOfferOfferedTextTONRejected, user.FullName(true), resalePriceTon.ToncoinCentCount, upgradedGift.Gift.ToName()));
                    }
                }
            }

            return _emptyString;
        }

        private static FormattedText UpdateChatShared(MessageWithOwner message, MessageChatShared chatShared, bool history)
        {
            var chat = message.Chat;
            if (chat != null && message.ClientService.TryGetChat(chatShared.Chat.ChatId, out Chat sharedChat))
            {
                if (message.ClientService.TryGetSupergroup(sharedChat, out Supergroup supergroup) && supergroup.IsChannel)
                {
                    return ReplaceWithLink(Strings.ActionRequestedPeerChannel, "un2", chat);
                }
                else
                {
                    return ReplaceWithLink(Strings.ActionRequestedPeerChat, "un2", chat);
                }
            }

            return _emptyString;
        }

        private static FormattedText UpdateUsersShared(MessageWithOwner message, MessageUsersShared usersShared, bool history)
        {
            var chat = message.Chat;
            if (chat != null)
            {
                var content = ReplaceWithLinks(Strings.ActionRequestedPeer, "un1", usersShared.Users.Select(x => x.UserId), message.ClientService);
                return ReplaceWithLink(content, "un2", chat);
            }
            else if (chat != null)
            {
                return ReplaceWithLink(Strings.ActionRequestedPeerUser, "un2", chat);
            }

            return _emptyString;
        }

        private static FormattedText UpdateWebAppDataSent(MessageWithOwner message, MessageWebAppDataSent webAppDataSent, bool history)
        {
            return string.Format(Strings.ActionBotWebViewData, webAppDataSent.ButtonText).AsFormattedText();
        }

        private static FormattedText UpdateExpiredPhoto(MessageWithOwner message, MessageExpiredPhoto expiredPhoto, bool history)
        {
            return Strings.AttachPhotoExpired.AsFormattedText();
        }

        private static FormattedText UpdateExpiredVideo(MessageWithOwner message, MessageExpiredVideo expiredVideo, bool history)
        {
            return Strings.AttachVideoExpired.AsFormattedText();
        }

        private static FormattedText UpdateExpiredVideoNote(MessageWithOwner message, MessageExpiredVideoNote expiredVideoNote, bool history)
        {
            return Strings.AttachRoundExpired.AsFormattedText();
        }

        private static FormattedText UpdateExpiredVoiceNote(MessageWithOwner message, MessageExpiredVoiceNote expiredVoiceNote, bool history)
        {
            return Strings.AttachVoiceExpired.AsFormattedText();
        }

        private static FormattedText UpdateChecklistTasksAdded(MessageWithOwner message, MessageChecklistTasksAdded checklistTasksAdded, bool history)
        {
            Checklist checklist = null;
            if (message is MessageViewModel { ReplyToItem: MessageViewModel { Content: MessageChecklist checklistContent } })
            {
                checklist = checklistContent.List;
            }

            FormattedText formatted;
            if (checklist == null)
            {
                if (checklistTasksAdded.Tasks.Count > 1)
                {
                    var text = message.IsOutgoing
                        ? Locale.Declension(Strings.R.TodoAddedTasksOutUnknown, checklistTasksAdded.Tasks.Count)
                        : Locale.Declension(Strings.R.TodoAddedTasksUnknown, checklistTasksAdded.Tasks.Count);
                    formatted = text.AsFormattedText();
                }
                else
                {
                    var text = message.IsOutgoing
                        ? Strings.TodoAddedTaskOutUnknown
                        : Strings.TodoAddedTaskUnknown;
                    formatted = ClientEx.Format(text, checklistTasksAdded.Tasks[0].Text);
                }
            }
            else if (checklistTasksAdded.Tasks.Count > 1)
            {
                var text = message.IsOutgoing
                    ? Locale.Declension(Strings.R.TodoAddedTasksOut, checklistTasksAdded.Tasks.Count, "{0}")
                    : Locale.Declension(Strings.R.TodoAddedTasks, checklistTasksAdded.Tasks.Count, "{0}");
                formatted = ClientEx.Format(text, checklist.Title);
            }
            else
            {
                var text = message.IsOutgoing
                    ? Strings.TodoAddedTaskOut
                    : Strings.TodoAddedTask;
                formatted = ClientEx.Format(text, checklistTasksAdded.Tasks[0].Text, checklist.Title);
            }

            formatted = ClientEx.ParseMarkdown(formatted);
            formatted = TdExtensions.Concat(ClientEx.CustomEmoji("\uEAD2 "), formatted);

            if (message.IsOutgoing)
            {
                return formatted;
            }

            return ReplaceWithLink(formatted, message.GetSender());
        }

        private static FormattedText UpdateChecklistTasksDone(MessageWithOwner message, MessageChecklistTasksDone checklistTasksDone, bool history)
        {
            var markedAsDone = checklistTasksDone.MarkedAsDoneTaskIds.Count > 0;
            var taskIds = markedAsDone
                ? checklistTasksDone.MarkedAsDoneTaskIds
                : checklistTasksDone.MarkedAsNotDoneTaskIds;

            var taskId = taskIds[0];

            ChecklistTask task = null;
            if (message is MessageViewModel { ReplyToItem: MessageViewModel { Content: MessageChecklist checklist } })
            {
                foreach (var item in checklist.List.Tasks)
                {
                    if (item.Id == taskId)
                    {
                        task = item;
                        break;
                    }
                }
            }

            if (task == null || taskIds.Count > 1)
            {
                string text;
                if (markedAsDone)
                {
                    text = message.IsOutgoing
                        ? Locale.Declension(Strings.R.TodoTasksCompletedOut, checklistTasksDone.MarkedAsDoneTaskIds.Count)
                        : Locale.Declension(Strings.R.TodoTasksCompleted, checklistTasksDone.MarkedAsDoneTaskIds.Count);
                }
                else
                {
                    text = message.IsOutgoing
                        ? Locale.Declension(Strings.R.TodoTasksNotCompletedOut, checklistTasksDone.MarkedAsNotDoneTaskIds.Count)
                        : Locale.Declension(Strings.R.TodoTasksNotCompleted, checklistTasksDone.MarkedAsNotDoneTaskIds.Count);
                }

                var formatted = ClientEx.ParseMarkdown(text);
                formatted = TdExtensions.Concat(ClientEx.CustomEmoji(markedAsDone ? "\uEAD3 " : "\uEAD4 "), formatted);

                return ReplaceWithLink(formatted, message.GetSender());
            }
            else
            {
                string text;
                if (markedAsDone)
                {
                    text = message.IsOutgoing
                        ? Strings.TodoTaskCompletedOut
                        : Strings.TodoTaskCompleted;
                }
                else
                {
                    text = message.IsOutgoing
                        ? Strings.TodoTaskNotCompletedOut
                        : Strings.TodoTaskNotCompleted;
                }

                var formatted = ClientEx.Format(text, task.Text);
                formatted = ClientEx.ParseMarkdown(formatted);
                formatted = TdExtensions.Concat(ClientEx.CustomEmoji(markedAsDone ? "\uEAD3 " : "\uEAD4 "), formatted);

                return ReplaceWithLink(formatted, message.GetSender());
            }
        }
        private static FormattedText UpdatePollOptionAdded(MessageWithOwner message, MessagePollOptionAdded pollOptionAdded, bool history)
        {
            FormattedText formatted;
            var text = message.IsOutgoing
                ? Strings.PollAddingActionYou
                : Strings.PollAddingActionOther;
            formatted = ClientEx.Format(text, pollOptionAdded.Text);
            formatted = ClientEx.ParseMarkdown(formatted);
            //formatted = TdExtensions.Concat(ClientEx.CustomEmoji("\uEAD2 "), formatted);

            if (message.IsOutgoing)
            {
                return formatted;
            }

            return ReplaceWithLink(formatted, message.GetSender());
        }

        private static FormattedText UpdatePollOptionDeleted(MessageWithOwner message, MessagePollOptionDeleted pollOptionDeleted, bool history)
        {
            FormattedText formatted;
            var text = message.IsOutgoing
                ? Strings.PollRemovedActionYou
                : Strings.PollRemovedActionOther;
            formatted = ClientEx.Format(text, pollOptionDeleted.Text);
            formatted = ClientEx.ParseMarkdown(formatted);
            //formatted = TdExtensions.Concat(ClientEx.CustomEmoji("\uEAD2 "), formatted);

            if (message.IsOutgoing)
            {
                return formatted;
            }

            return ReplaceWithLink(formatted, message.GetSender());
        }

        private static FormattedText UpdateSuggestedPostPaid(MessageWithOwner message, MessageSuggestedPostPaid suggestedPostPaid, bool history)
        {
            var sender = message.ClientService.GetTitle(message.SenderId);

            if (suggestedPostPaid.StarAmount.IsPositive())
            {
                return string.Format(Strings.SuggestedOfferCompleteAmountF.ReplaceStar(Icons.Premium), sender, suggestedPostPaid.StarAmount.ToValue()).AsFormattedText();
            }
            else if (suggestedPostPaid.TonAmount > 0)
            {
                return string.Format(Strings.SuggestedOfferCompleteAmountF.ReplaceStar(Icons.Ton), sender, suggestedPostPaid.TonAmount / Constants.ToncoinMin).AsFormattedText();
            }

            return string.Format(Strings.SuggestedOfferCompleteAmountUnknown, sender).AsFormattedText();
        }

        private static FormattedText UpdateSuggestedPostRefunded(MessageWithOwner message, MessageSuggestedPostRefunded suggestedPostRefunded, bool history)
        {
            var sender = message.ClientService.GetTitle(message.SenderId);

            if (suggestedPostRefunded.Reason is SuggestedPostRefundReasonPostDeleted)
            {
                if (message is MessageViewModel { ReplyToItem: MessageViewModel replyTo })
                {
                    if (replyTo.SuggestedPostInfo.Price is SuggestedPostPriceStar priceStar)
                    {
                        return string.Format(Strings.SuggestedOfferRefundByAdminAmountF.ReplaceStar(Icons.Premium), sender, message.Chat.Title, priceStar.StarCount).AsFormattedText();
                    }
                    else if (replyTo.SuggestedPostInfo.Price is SuggestedPostPriceTon priceTon)
                    {
                        return string.Format(Strings.SuggestedOfferRefundByAdminAmountF.ReplaceStar(Icons.Ton), sender, message.Chat.Title, priceTon.ToncoinCentCount).AsFormattedText();
                    }
                }
                else
                {
                    return string.Format(Strings.SuggestedOfferRefundByAdminAmountUnknown, sender, message.Chat.Title).AsFormattedText();
                }
            }
            else if (message is MessageViewModel { ReplyToItem: MessageViewModel replyTo })
            {
                if (replyTo.SuggestedPostInfo.Price is SuggestedPostPriceStar priceStar)
                {
                    return string.Format(Strings.SuggestedOfferRefundByUserAmountF.ReplaceStar(Icons.Premium), sender, message.Chat.Title, priceStar.StarCount).AsFormattedText();
                }
                else if (replyTo.SuggestedPostInfo.Price is SuggestedPostPriceTon priceTon)
                {
                    return string.Format(Strings.SuggestedOfferRefundByUserAmountF.ReplaceStar(Icons.Ton), sender, message.Chat.Title, priceTon.ToncoinCentCount).AsFormattedText();
                }
            }
            else
            {
                return string.Format(Strings.SuggestedOfferRefundByUserAmountUnknown, sender, message.Chat.Title).AsFormattedText();
            }

            return _emptyString;
        }

        private static FormattedText UpdateChatBoost(MessageWithOwner message, MessageChatBoost chatBoost, bool history)
        {
            var content = string.Empty;

            if (message.ClientService.TryGetUser(message.SenderId, out User user))
            {
                content = user.FullName(true);
            }
            else if (message.ClientService.TryGetChat(message.SenderId, out Chat chat))
            {
                content = chat.Title;
            }

            if (message.IsChannelPost)
            {
                if (chatBoost.BoostCount > 1)
                {
                    return Locale.Declension(message.IsOutgoing ? Strings.R.BoostingBoostsChannelByYouServiceMsgCount : Strings.R.BoostingBoostsChannelByUserServiceMsgCount, chatBoost.BoostCount, content).AsFormattedText();
                }
                else
                {
                    return string.Format(message.IsOutgoing ? Strings.BoostingBoostsChannelByYouServiceMsg : Strings.BoostingBoostsChannelByUserServiceMsg, content).AsFormattedText();
                }
            }
            else
            {
                if (chatBoost.BoostCount > 1)
                {
                    return Locale.Declension(message.IsOutgoing ? Strings.R.BoostingBoostsGroupByYouServiceMsgCount : Strings.R.BoostingBoostsGroupByUserServiceMsgCount, chatBoost.BoostCount, content).AsFormattedText();
                }
                else
                {
                    return string.Format(message.IsOutgoing ? Strings.BoostingBoostsGroupByYouServiceMsg : Strings.BoostingBoostsGroupByUserServiceMsg, content).AsFormattedText();
                }
            }
        }

        private static FormattedText UpdateStory(MessageWithOwner message, MessageAsyncStory story, bool history)
        {
            string content = string.Empty;

            if (message.ClientService.TryGetUser(message.Chat, out User user))
            {
                if (message.IsOutgoing)
                {
                    content = string.Format(story.State == MessageStoryState.Expired ? Icons.ExpiredStory + "\u00A0" + Strings.ExpiredStoryMentioned : Strings.StoryYouMentionedTitle, user.FullName(true));
                }
                else
                {
                    content = string.Format(story.State == MessageStoryState.Expired ? Icons.ExpiredStory + "\u00A0" + Strings.ExpiredStoryMention : Strings.StoryMentionedTitle, user.FullName(true));
                }
            }

            return ClientEx.ParseMarkdown(content);
        }

        private readonly static FormattedText _emptyString = new(string.Empty, Array.Empty<TextEntity>());

        private static FormattedText UpdateStory(MessageWithOwner message, MessageStory story, bool history)
        {
            if (message.IsOutgoing)
            {
                if (message.ClientService.TryGetUser(message.Chat, out User user))
                {
                    return string.Format(Strings.StoryYouMentionInDialog, user.FullName(true)).AsFormattedText();
                }
            }
            else
            {
                return Strings.StoryMentionInDialog.AsFormattedText();
            }

            return _emptyString;
        }

        public static FormattedText ReplaceWithLink(string source, params object[] args)
        {
            return ReplaceWithLink(new FormattedText(source, new List<TextEntity>(args.Length)), args);
        }

        public static FormattedText ReplaceWithLink(FormattedText source, params object[] args)
        {
            source.Text = source.Text.Replace("**", string.Empty);

            if (source.Entities.IsReadOnly)
            {
                source.Entities = new List<TextEntity>(source.Entities);
            }

            for (int i = 0; i < args.Length; i++)
            {
                var obj = args[i];
                var param = "un" + (i + 1);
                var index = source.Text.IndexOf(param);

                if (index >= 0)
                {
                    String name;
                    TextEntityType id = null;
                    if (obj is User user)
                    {
                        name = user.FullName();
                        id = new TextEntityTypeMentionName(user.Id);
                    }
                    else if (obj is Chat chat)
                    {
                        name = chat.Title;
                    }
                    else if (obj is Game game)
                    {
                        name = game.Title;
                    }
                    else if (obj is MessageGift gift)
                    {
                        name = Locale.Declension(Strings.R.StarsCount, gift.Gift.StarCount + gift.PrepaidUpgradeStarCount);
                    }
                    else if (obj is MessageGiftedPremium giftedPremium)
                    {
                        name = Locale.FormatCurrency(giftedPremium.Amount, giftedPremium.Currency);
                    }
                    else if (obj is MessagePremiumGiftCode premiumGiftCode)
                    {
                        name = Locale.FormatCurrency(premiumGiftCode.Amount, premiumGiftCode.Currency);
                    }
                    else if (obj is MessageGiftedStars giftedStars)
                    {
                        name = Locale.FormatCurrency(giftedStars.Amount, giftedStars.Currency);
                    }
                    else if (obj is ForumTopicInfo forumTopicInfo)
                    {
                        name = $"\U0001F4C3 {forumTopicInfo.Name}";

                        // TODO: build text url
                        id = new TextEntityTypeTextUrl("tg-topic://");
                    }
                    else if (obj is string value)
                    {
                        name = value;
                        id = null;
                    }
                    else
                    {
                        name = "";
                        id = null;
                    }

                    foreach (var entity in source.Entities)
                    {
                        if (entity.Offset > index)
                        {
                            entity.Offset += name.Length - param.Length;
                        }
                    }

                    source.Text = source.Text.Remove(index, param.Length);
                    source.Text = source.Text.Insert(index, name);

                    source.Entities.Add(new TextEntity(index, name.Length, id ?? new TextEntityTypeBold()));
                }
            }

            return source;
        }

        public static FormattedText ReplaceWithName(string source, params object[] args)
        {
            for (int i = 0; i < args.Length; i++)
            {
                var obj = args[i];
                if (obj is User user)
                {
                    args[i] = user.FullName();
                }
                else if (obj is Chat chat)
                {
                    args[i] = chat.Title;
                }
                else if (obj is Game game)
                {
                    args[i] = game.Title;
                }
                else if (obj is MessageGift gift)
                {
                    args[i] = Locale.Declension(Strings.R.StarsCount, gift.Gift.StarCount + gift.PrepaidUpgradeStarCount);
                }
                else if (obj is MessageGiftedPremium giftedPremium)
                {
                    args[i] = Locale.FormatCurrency(giftedPremium.Amount, giftedPremium.Currency);
                }
                else if (obj is MessagePremiumGiftCode premiumGiftCode)
                {
                    args[i] = Locale.FormatCurrency(premiumGiftCode.Amount, premiumGiftCode.Currency);
                }
                else if (obj is MessageGiftedStars giftedStars)
                {
                    args[i] = Locale.FormatCurrency(giftedStars.Amount, giftedStars.Currency);
                }
                else if (obj is ForumTopicInfo forumTopicInfo)
                {
                    args[i] = $"\U0001F4C3 {forumTopicInfo.Name}";
                }
            }

            return string.Format(source, args).AsFormattedText();
        }

        private static FormattedText ReplaceWithLinks(string source, string param, IEnumerable<long> uids, IClientService clientService)
        {
            return ReplaceWithLinks(new FormattedText(source, new List<TextEntity>()), param, uids, clientService);
        }

        private static FormattedText ReplaceWithLinks(FormattedText source, string param, IEnumerable<long> uids, IClientService clientService)
        {
            if (source.Entities.IsReadOnly)
            {
                source.Entities = new List<TextEntity>(source.Entities);
            }

            int index;
            int start = index = source.Text.IndexOf(param);
            if (start >= 0)
            {
                var names = new StringBuilder();
                var entities = new List<TextEntity>();

                foreach (var user in clientService.GetUsers(uids))
                {
                    var name = user.FullName();
                    if (names.Length != 0)
                    {
                        names.Append(", ");
                    }

                    start = index + names.Length;
                    names.Append(name);

                    entities.Add(new TextEntity(start, name.Length, new TextEntityTypeMentionName(user.Id)));
                }

                foreach (var entity in source.Entities)
                {
                    if (entity.Offset > start)
                    {
                        entity.Offset += names.Length - param.Length;
                    }
                }

                source.Text = source.Text.Remove(index, param.Length);
                source.Text = source.Text.Insert(index, names.ToString());

                source.Entities.AddRange(entities);
            }

            return source;
        }

        private void ReplaceEntities(MessageViewModel message, Span span, string text, IList<TextEntity> entities)
        {
            if (entities == null)
            {
                span.Inlines.Add(new Run { Text = text });
                return;
            }

            var previous = 0;

            foreach (var entity in entities.OrderBy(x => x.Offset))
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

                if (entity.Type is TextEntityTypeBold)
                {
                    span.Inlines.Add(new Run { Text = text.Substring(entity.Offset, entity.Length), FontWeight = FontWeights.SemiBold });
                }
                else if (entity.Type is TextEntityTypeItalic)
                {
                    span.Inlines.Add(new Run { Text = text.Substring(entity.Offset, entity.Length), FontStyle = FontStyle.Italic });
                }
                else if (entity.Type is TextEntityTypeCode)
                {
                    span.Inlines.Add(new Run { Text = text.Substring(entity.Offset, entity.Length), FontFamily = new FontFamily("Consolas") });
                }
                else if (entity.Type is TextEntityTypePre || entity.Type is TextEntityTypePreCode)
                {
                    // TODO any additional
                    span.Inlines.Add(new Run { Text = text.Substring(entity.Offset, entity.Length), FontFamily = new FontFamily("Consolas") });
                }
                else if (entity.Type is TextEntityTypeUrl || entity.Type is TextEntityTypeEmailAddress || entity.Type is TextEntityTypePhoneNumber || entity.Type is TextEntityTypeMention || entity.Type is TextEntityTypeHashtag || entity.Type is TextEntityTypeCashtag || entity.Type is TextEntityTypeBotCommand)
                {
                    var data = text.Substring(entity.Offset, entity.Length);

                    var hyperlink = new Hyperlink();
                    hyperlink.Click += (s, args) => Entity_Click(message, entity.Type, data);
                    hyperlink.Inlines.Add(new Run { Text = data });
                    //hyperlink.Foreground = foreground;
                    hyperlink.UnderlineStyle = UnderlineStyle.None;
                    hyperlink.Foreground = new SolidColorBrush(Colors.White);
                    hyperlink.FontWeight = FontWeights.SemiBold;
                    span.Inlines.Add(hyperlink);

                    //if (entity is TLMessageEntityUrl)
                    //{
                    //    SetEntity(hyperlink, (string)data);
                    //}
                }
                else if (entity.Type is TextEntityTypeTextUrl || entity.Type is TextEntityTypeMentionName)
                {
                    object data;
                    if (entity.Type is TextEntityTypeTextUrl textUrl)
                    {
                        data = textUrl.Url;
                    }
                    else if (entity.Type is TextEntityTypeMentionName mentionName)
                    {
                        data = mentionName.UserId;
                    }

                    var hyperlink = new Hyperlink();
                    hyperlink.Click += (s, args) => Entity_Click(message, entity.Type, null);
                    hyperlink.Inlines.Add(new Run { Text = text.Substring(entity.Offset, entity.Length) });
                    //hyperlink.Foreground = foreground;
                    hyperlink.UnderlineStyle = UnderlineStyle.None;
                    hyperlink.Foreground = new SolidColorBrush(Colors.White);
                    hyperlink.FontWeight = FontWeights.SemiBold;
                    span.Inlines.Add(hyperlink);

                    //if (entity is TLMessageEntityTextUrl textUrl)
                    //{
                    //    SetEntity(hyperlink, textUrl.Url);
                    //    ToolTipService.SetToolTip(hyperlink, textUrl.Url);
                    //}
                }

                previous = entity.Offset + entity.Length;
            }

            if (text.Length > previous)
            {
                span.Inlines.Add(new Run { Text = text.Substring(previous) });
            }
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
                message.Delegate.OpenUrl(textUrl.Url, true, new OpenUrlSourceChat(message.ChatId, message.SenderId));
            }
            else if (type is TextEntityTypeUrl)
            {
                message.Delegate.OpenUrl(data, false, new OpenUrlSourceChat(message.ChatId, message.SenderId));
            }
        }



        private static Game GetGame(MessageViewModel message)
        {
            var reply = message?.ReplyToItem as MessageViewModel;
            if (reply == null)
            {
                return null;
            }

            var game = reply.Content as MessageGame;
            if (game == null)
            {
                return null;
            }

            return game.Game;
        }

        private static MessageInvoice GetInvoice(MessageViewModel message)
        {
            var reply = message?.ReplyToItem as MessageViewModel;
            if (reply == null)
            {
                return null;
            }

            var invoice = reply.Content as MessageInvoice;
            if (invoice == null)
            {
                return null;
            }

            return invoice;
        }

        public void UpdateMessageInteractionInfo(MessageViewModel message)
        {
            UpdateMessageReactions(message, false);
        }

        public void UpdateMessageReactions(MessageViewModel message, bool animate)
        {
            // TODO: Name
            var reactions = GetTemplateChild("Reactions") as ReactionsPanel;
            reactions?.UpdateMessageReactions(message, animate);
        }
    }

    public partial class DashedLine : Path
    {
        private readonly LineGeometry _geometry;

        public DashedLine()
        {
            _geometry = new LineGeometry();
            Data = _geometry;
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            var size = base.MeasureOverride(availableSize);
            return new Size(size.Width, 2);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            _geometry.StartPoint = new Windows.Foundation.Point(0, 1);
            _geometry.EndPoint = new Windows.Foundation.Point(finalSize.Width, 1);

            var size = base.ArrangeOverride(finalSize);
            return new Size(size.Width, 2);
        }
    }
}
