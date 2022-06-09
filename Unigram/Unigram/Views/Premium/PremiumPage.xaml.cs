using Telegram.Td.Api;
using Unigram.Common;
using Unigram.ViewModels.Premium;
using Windows.UI;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace Unigram.Views.Premium
{
    public sealed partial class PremiumPage : HostedPage
    {
        public PremiumViewModel ViewModel => DataContext as PremiumViewModel;

        public PremiumPage()
        {
            InitializeComponent();

            Title = Strings.Resources.TelegramPremium;
            DropShadowEx.Attach(PurchaseShadow);
        }

        private void OnItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is PremiumFeature feature)
            {
                ViewModel.Open(feature);
            }
        }

        private readonly Color[] _gradient = new Color[]
        {
            Color.FromArgb(0xFF, 0xF2, 0x7C, 0x30),
            Color.FromArgb(0xFF, 0xE3, 0x68, 0x50),
            Color.FromArgb(0xFF, 0xD1, 0x50, 0x78),
            Color.FromArgb(0xFF, 0xC1, 0x49, 0x98),
            Color.FromArgb(0xFF, 0xB2, 0x4C, 0xB5),
            Color.FromArgb(0xFF, 0xA3, 0x4E, 0xD0),
            Color.FromArgb(0xFF, 0x90, 0x54, 0xE9),
            Color.FromArgb(0xFF, 0x75, 0x61, 0xEB),
            Color.FromArgb(0xFF, 0x5A, 0x6E, 0xEE),
            Color.FromArgb(0xFF, 0x54, 0x8D, 0xFF),
            Color.FromArgb(0xFF, 0x54, 0xA3, 0xFF),
        };

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }

            var feature = args.Item as PremiumFeature;
            var content = args.ItemContainer.ContentTemplateRoot as Grid;

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
                case PremiumFeatureDisabledAds:
                    titleValue = Strings.Resources.PremiumPreviewNoAds;
                    subtitleValue = Strings.Resources.PremiumPreviewNoAdsDescription;
                    break;
                case PremiumFeatureImprovedDownloadSpeed:
                    titleValue = Strings.Resources.PremiumPreviewDownloadSpeed;
                    subtitleValue = Strings.Resources.PremiumPreviewDownloadSpeedDescription;
                    break;
                case PremiumFeatureIncreasedLimits:
                    titleValue = Strings.Resources.PremiumPreviewLimits;
                    subtitleValue = ViewModel.PremiumPreviewLimitsDescription;
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
                    titleValue = Strings.Resources.PremiumPreviewReactions;
                    subtitleValue = Strings.Resources.PremiumPreviewReactionsDescription;
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

            var title = content.FindName("Title") as TextBlock;
            var subtitle = content.FindName("Subtitle") as TextBlock;
            var icon = content.FindName("Icon") as Border;

            title.Text = titleValue;
            subtitle.Text = subtitleValue;
            icon.Background = new SolidColorBrush(_gradient[args.ItemIndex]);
        }

        public string ConvertPurchase(long amount, string currency)
        {
            return string.Format(Strings.Resources.SubscribeToPremium, Locale.FormatCurrency(amount, currency));
        }
    }
}
