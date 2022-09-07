using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Services;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Unigram.Controls.Cells.Premium
{
    public sealed partial class PremiumFeatureCell : UserControl
    {
        public PremiumFeatureCell()
        {
            InitializeComponent();
        }

        internal void UpdateFeature(IProtoService protoService, PremiumFeature feature, Animation value)
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
                    titleValue = Strings.Resources.PremiumPreviewAdvancedChatManagement;
                    subtitleValue = Strings.Resources.PremiumPreviewAdvancedChatManagementDescription;
                    break;
                case PremiumFeatureAnimatedProfilePhoto:
                    titleValue = Strings.Resources.PremiumPreviewAnimatedProfiles;
                    subtitleValue = Strings.Resources.PremiumPreviewAnimatedProfilesDescription;
                    break;
                case PremiumFeatureAppIcons:
                    titleValue = Strings.Resources.PremiumPreviewAppIcon;
                    subtitleValue = Strings.Resources.PremiumPreviewAppIconDescription;
                    break;
                case PremiumFeatureCustomEmoji:
                    titleValue = Strings.Resources.PremiumPreviewEmoji;
                    subtitleValue = Strings.Resources.PremiumPreviewEmojiDescription;
                    break;
                case PremiumFeatureDisabledAds:
                    titleValue = Strings.Resources.PremiumPreviewNoAds;
                    subtitleValue = Strings.Resources.PremiumPreviewNoAdsDescription;
                    break;
                case PremiumFeatureImprovedDownloadSpeed:
                    titleValue = Strings.Resources.PremiumPreviewDownloadSpeed;
                    subtitleValue = Strings.Resources.PremiumPreviewDownloadSpeedDescription;
                    break;
                case PremiumFeatureIncreasedUploadFileSize:
                    titleValue = Strings.Resources.PremiumPreviewUploads;
                    subtitleValue = Strings.Resources.PremiumPreviewUploadsDescription;
                    break;
                case PremiumFeatureProfileBadge:
                    titleValue = Strings.Resources.PremiumPreviewProfileBadge;
                    subtitleValue = Strings.Resources.PremiumPreviewProfileBadgeDescription;
                    break;
                case PremiumFeatureUniqueReactions:
                    titleValue = Strings.Resources.PremiumPreviewReactions2;
                    subtitleValue = Strings.Resources.PremiumPreviewReactions2Description;
                    break;
                case PremiumFeatureUniqueStickers:
                    titleValue = Strings.Resources.PremiumPreviewStickers;
                    subtitleValue = Strings.Resources.PremiumPreviewStickersDescription;
                    break;
                case PremiumFeatureVoiceRecognition:
                    titleValue = Strings.Resources.PremiumPreviewVoiceToText;
                    subtitleValue = Strings.Resources.PremiumPreviewVoiceToTextDescription;
                    break;
            }

            Title.Text = titleValue;
            Subtitle.Text = subtitleValue;

            if (value != null)
            {
                Player.Source = new RemoteVideoSource(protoService, value.AnimationValue, value.Duration);
            }
            else
            {
                Player.Source = null;
            }
        }

        public void StopAnimation()
        {
            Player.Stop();
        }

        public void PlayAnimation()
        {
            Player.Play();
        }
    }
}
