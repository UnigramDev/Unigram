//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
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
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation;
using Windows.UI.Xaml.Controls;
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

            Telegram.Common.Instrumentation.Register(this);
        }

        public MessageViewModel Message => _message;

        #region ContentOpacity

        public double ContentOpacity
        {
            get { return (double)GetValue(ContentOpacityProperty); }
            set { SetValue(ContentOpacityProperty, value); }
        }

        public static readonly DependencyProperty ContentOpacityProperty =
            DependencyProperty.Register("ContentOpacity", typeof(double), typeof(MessageService), new PropertyMetadata(1d));

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

            var entities = MessageServiceText.GetEntities(message, true);
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

            AutomationProperties.SetName(this, title.Text);
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
                UpdateMessageTopic();
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
