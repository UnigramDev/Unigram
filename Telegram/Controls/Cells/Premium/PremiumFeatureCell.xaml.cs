//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Telegram.Services;
using Telegram.Streams;
using Telegram.Td.Api;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Telegram.Controls.Cells.Premium
{
    public sealed partial class PremiumFeatureCell : UserControl
    {
        public PremiumFeatureCell()
        {
            InitializeComponent();
        }

        internal void UpdateFeature(IClientService clientService, PremiumFeature feature, Animation value)
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
                case PremiumFeatureUniqueReactions:
                    titleValue = Strings.PremiumPreviewReactions2;
                    subtitleValue = Strings.PremiumPreviewReactions2Description;
                    break;
                case PremiumFeatureUniqueStickers:
                    titleValue = Strings.PremiumPreviewStickers;
                    subtitleValue = Strings.PremiumPreviewStickersDescription;
                    break;
                case PremiumFeatureVoiceRecognition:
                    titleValue = Strings.PremiumPreviewVoiceToText;
                    subtitleValue = Strings.PremiumPreviewVoiceToTextDescription;
                    break;
            }

            Title.Text = titleValue;
            Subtitle.Text = subtitleValue;

            if (value != null)
            {
                Player.Source = new RemoteFileSource(clientService, value.AnimationValue, value.Duration);
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
