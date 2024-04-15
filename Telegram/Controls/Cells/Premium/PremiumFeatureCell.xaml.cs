//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using Telegram.Services;
using Telegram.Streams;
using Telegram.Td.Api;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Telegram.Controls.Cells.Premium
{
    public interface IPremiumFeatureCell
    {
        void PlayAnimation();
        void StopAnimation();
    }

    public sealed partial class PremiumFeatureCell : UserControl, IPremiumFeatureCell
    {
        public PremiumFeatureCell()
        {
            InitializeComponent();
        }

        public void UpdateFeature(IClientService clientService, PremiumFeature feature, Animation value)
        {
            var bottom = feature switch
            {
                PremiumFeatureIncreasedUploadFileSize => true,
                PremiumFeatureDisabledAds => true,
                PremiumFeatureCustomEmoji => true,
                _ => false
            };

            if (bottom)
            {
                FrameOutside.Margin = new Thickness(0, -157, 0, 0);
                FrameInside.Margin = new Thickness(0, -157, 0, 0);

                Canvas.SetTop(FrameScreen, 0);
                Canvas.SetTop(Player, 0);

                FrameHole.Visibility = Visibility.Collapsed;
            }
            else
            {
                FrameOutside.Margin = new Thickness(0, 0, 0, 0);
                FrameInside.Margin = new Thickness(0, 0, 0, 0);

                Canvas.SetTop(FrameScreen, 28);
                Canvas.SetTop(Player, 28);

                FrameHole.Visibility = Visibility.Visible;
            }

            var titleValue = string.Empty;
            var subtitleValue = string.Empty;

            switch (feature)
            {
                case PremiumFeatureAccentColor:
                    titleValue = Strings.PremiumPreviewProfileColor;
                    subtitleValue = Strings.PremiumPreviewProfileColorDescription;
                    break;
                case PremiumFeatureBackgroundForBoth:
                    titleValue = Strings.PremiumPreviewWallpaper;
                    subtitleValue = Strings.PremiumPreviewWallpaperDescription;
                    break;
                case PremiumFeatureAdvancedChatManagement:
                    titleValue = Strings.PremiumPreviewAdvancedChatManagement;
                    subtitleValue = Strings.PremiumPreviewAdvancedChatManagementDescription;
                    break;
                case PremiumFeatureAnimatedProfilePhoto:
                    titleValue = Strings.PremiumPreviewAnimatedProfiles;
                    subtitleValue = Strings.PremiumPreviewAnimatedProfilesDescription;
                    break;
                case PremiumFeatureAppIcons:
                    titleValue = Strings.PremiumPreviewAppIcon;
                    subtitleValue = Strings.PremiumPreviewAppIconDescription;
                    break;
                case PremiumFeatureCustomEmoji:
                    titleValue = Strings.PremiumPreviewEmoji;
                    subtitleValue = Strings.PremiumPreviewEmojiDescription;
                    break;
                case PremiumFeatureDisabledAds:
                    titleValue = Strings.PremiumPreviewNoAds;
                    subtitleValue = Strings.PremiumPreviewNoAdsDescription;
                    break;
                case PremiumFeatureEmojiStatus:
                    titleValue = Strings.PremiumPreviewEmojiStatus;
                    subtitleValue = Strings.PremiumPreviewEmojiStatusDescription;
                    break;
                case PremiumFeatureImprovedDownloadSpeed:
                    titleValue = Strings.PremiumPreviewDownloadSpeed;
                    subtitleValue = Strings.PremiumPreviewDownloadSpeedDescription;
                    break;
                case PremiumFeatureIncreasedUploadFileSize:
                    titleValue = Strings.PremiumPreviewUploads;
                    subtitleValue = Strings.PremiumPreviewUploadsDescription;
                    break;
                case PremiumFeatureProfileBadge:
                    titleValue = Strings.PremiumPreviewProfileBadge;
                    subtitleValue = Strings.PremiumPreviewProfileBadgeDescription;
                    break;
                case PremiumFeatureRealTimeChatTranslation:
                    titleValue = Strings.PremiumPreviewTranslations;
                    subtitleValue = Strings.PremiumPreviewTranslationsDescription;
                    break;
                case PremiumFeatureSavedMessagesTags:
                    titleValue = Strings.PremiumPreviewTags2;
                    subtitleValue = Strings.PremiumPreviewTagsDescription2;
                    break;
                case PremiumFeatureUniqueReactions:
                    titleValue = Strings.PremiumPreviewReactions2;
                    subtitleValue = Strings.PremiumPreviewReactions2Description;
                    break;
                case PremiumFeatureUniqueStickers:
                    titleValue = Strings.PremiumPreviewStickers;
                    subtitleValue = Strings.PremiumPreviewStickersDescription;
                    break;
                case PremiumFeatureUpgradedStories:
                    titleValue = Strings.PremiumPreviewStories;
                    subtitleValue = Strings.PremiumPreviewStoriesDescription;
                    break;
                case PremiumFeatureVoiceRecognition:
                    titleValue = Strings.PremiumPreviewVoiceToText;
                    subtitleValue = Strings.PremiumPreviewVoiceToTextDescription;
                    break;
                case PremiumFeatureLastSeenTimes:
                    titleValue = Strings.PremiumPreviewLastSeen;
                    subtitleValue = Strings.PremiumPreviewLastSeenDescription;
                    break;
                case PremiumFeatureMessagePrivacy:
                    titleValue = Strings.PremiumPreviewMessagePrivacy;
                    subtitleValue = Strings.PremiumPreviewMessagePrivacyDescription;
                    break;
            }

            Title.Text = titleValue;
            Subtitle.Text = subtitleValue;

            if (value != null)
            {
                Player.Source = new RemoteFileSource(clientService, value.AnimationValue)
                {
                    Width = 196,
                    Height = 292,
                    Outline = Array.Empty<ClosedVectorPath>()
                };
            }
            else
            {
                Player.Source = null;
            }
        }

        public void UpdateFeature(IClientService clientService, BusinessFeature feature, Animation value)
        {
            var bottom = feature switch
            {
                _ => false
            };

            if (bottom)
            {
                FrameOutside.Margin = new Thickness(0, -157, 0, 0);
                FrameInside.Margin = new Thickness(0, -157, 0, 0);

                Canvas.SetTop(FrameScreen, 0);
                Canvas.SetTop(Player, 0);

                FrameHole.Visibility = Visibility.Collapsed;
            }
            else
            {
                FrameOutside.Margin = new Thickness(0, 0, 0, 0);
                FrameInside.Margin = new Thickness(0, 0, 0, 0);

                Canvas.SetTop(FrameScreen, 28);
                Canvas.SetTop(Player, 28);

                FrameHole.Visibility = Visibility.Visible;
            }

            var titleValue = string.Empty;
            var subtitleValue = string.Empty;

            switch (feature)
            {
                case BusinessFeatureGreetingMessage:
                    titleValue = Strings.PremiumBusinessGreetingMessages;
                    subtitleValue = Strings.PremiumBusinessGreetingMessagesDescription;
                    break;
                case BusinessFeatureAwayMessage:
                    titleValue = Strings.PremiumBusinessAwayMessages;
                    subtitleValue = Strings.PremiumBusinessAwayMessagesDescription;
                    break;
                case BusinessFeatureQuickReplies:
                    titleValue = Strings.PremiumBusinessQuickReplies;
                    subtitleValue = Strings.PremiumBusinessQuickRepliesDescription;
                    break;
                case BusinessFeatureOpeningHours:
                    titleValue = Strings.PremiumBusinessOpeningHours;
                    subtitleValue = Strings.PremiumBusinessOpeningHoursDescription;
                    break;
                case BusinessFeatureLocation:
                    titleValue = Strings.PremiumBusinessLocation;
                    subtitleValue = Strings.PremiumBusinessLocationDescription;
                    break;
                case BusinessFeatureBots:
                    titleValue = Strings.PremiumBusinessChatbots2;
                    subtitleValue = Strings.PremiumBusinessChatbotsDescription;
                    break;
                case BusinessFeatureStartPage:
                    titleValue = Strings.PremiumBusinessIntro;
                    subtitleValue = Strings.PremiumBusinessIntroDescription;
                    break;
                case BusinessFeatureAccountLinks:
                    titleValue = Strings.PremiumBusinessChatLinks;
                    subtitleValue = Strings.PremiumBusinessChatLinksDescription;
                    break;
                case BusinessFeatureChatFolderTags:
                    titleValue = Strings.PremiumPreviewFolderTags;
                    subtitleValue = Strings.PremiumPreviewFolderTagsDescription;
                    break;
                case BusinessFeatureEmojiStatus:
                    titleValue = Strings.PremiumPreviewBusinessEmojiStatus;
                    subtitleValue = Strings.PremiumPreviewBusinessEmojiStatusDescription;
                    break;
            }

            Title.Text = titleValue;
            Subtitle.Text = subtitleValue;

            if (value != null)
            {
                Player.Source = new RemoteFileSource(clientService, value.AnimationValue)
                {
                    Width = 196,
                    Height = 292,
                    Outline = Array.Empty<ClosedVectorPath>()
                };
            }
            else
            {
                Player.Source = null;
            }
        }

        public void StopAnimation()
        {
            Player.Pause();
        }

        public void PlayAnimation()
        {
            Player.Play();
        }
    }
}
