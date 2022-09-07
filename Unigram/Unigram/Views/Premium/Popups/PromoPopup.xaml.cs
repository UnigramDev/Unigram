using System;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Converters;
using Unigram.ViewModels.Premium;
using Windows.UI;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace Unigram.Views.Premium.Popups
{
    public sealed partial class PromoPopup : ContentPopup
    {
        public PromoViewModel ViewModel => DataContext as PromoViewModel;

        public PromoPopup()
        {
            InitializeComponent();

            Title = Strings.Resources.TelegramPremium;
        }

        private async void OnItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is PremiumFeature feature)
            {
                Hide();

                if (await ViewModel.OpenAsync(feature))
                {
                    await ShowAsync();
                }
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

            var iconValue = string.Empty;
            var titleValue = string.Empty;
            var subtitleValue = string.Empty;

            switch (feature)
            {
                case PremiumFeatureAdvancedChatManagement:
                    iconValue = Icons.ChatFilled24;
                    titleValue = Strings.Resources.PremiumPreviewAdvancedChatManagement;
                    subtitleValue = Strings.Resources.PremiumPreviewAdvancedChatManagementDescription;
                    break;
                case PremiumFeatureAnimatedProfilePhoto:
                    iconValue = Icons.PlayCircleFilled24;
                    titleValue = Strings.Resources.PremiumPreviewAnimatedProfiles;
                    subtitleValue = Strings.Resources.PremiumPreviewAnimatedProfilesDescription;
                    break;
                case PremiumFeatureAppIcons:
                    titleValue = Strings.Resources.PremiumPreviewAppIcon;
                    subtitleValue = Strings.Resources.PremiumPreviewAppIconDescription;
                    break;
                case PremiumFeatureCustomEmoji:
                    iconValue = Icons.EmojiFilled24;
                    titleValue = Strings.Resources.PremiumPreviewEmoji;
                    subtitleValue = Strings.Resources.PremiumPreviewEmojiDescription;
                    break;
                case PremiumFeatureDisabledAds:
                    iconValue = Icons.MegaphoneFilled24;
                    titleValue = Strings.Resources.PremiumPreviewNoAds;
                    subtitleValue = Strings.Resources.PremiumPreviewNoAdsDescription;
                    break;
                case PremiumFeatureImprovedDownloadSpeed:
                    iconValue = Icons.TopSpeedFilled24;
                    titleValue = Strings.Resources.PremiumPreviewDownloadSpeed;
                    subtitleValue = Strings.Resources.PremiumPreviewDownloadSpeedDescription;
                    break;
                case PremiumFeatureIncreasedLimits:
                    iconValue = Icons.Multiplier2xFilled24;
                    titleValue = Strings.Resources.PremiumPreviewLimits;
                    subtitleValue = ViewModel.PremiumPreviewLimitsDescription;
                    break;
                case PremiumFeatureIncreasedUploadFileSize:
                    iconValue = Icons.DocumentFilled24;
                    titleValue = Strings.Resources.PremiumPreviewUploads;
                    subtitleValue = Strings.Resources.PremiumPreviewUploadsDescription;
                    break;
                case PremiumFeatureProfileBadge:
                    iconValue = Icons.PremiumFilled24;
                    titleValue = Strings.Resources.PremiumPreviewProfileBadge;
                    subtitleValue = Strings.Resources.PremiumPreviewProfileBadgeDescription;
                    break;
                case PremiumFeatureUniqueReactions:
                    iconValue = Icons.HeartFilled24;
                    titleValue = Strings.Resources.PremiumPreviewReactions2;
                    subtitleValue = Strings.Resources.PremiumPreviewReactions2Description;
                    break;
                case PremiumFeatureUniqueStickers:
                    iconValue = Icons.StickerFilled24;
                    titleValue = Strings.Resources.PremiumPreviewStickers;
                    subtitleValue = Strings.Resources.PremiumPreviewStickersDescription;
                    break;
                case PremiumFeatureVoiceRecognition:
                    iconValue = Icons.MicFilled24;
                    titleValue = Strings.Resources.PremiumPreviewVoiceToText;
                    subtitleValue = Strings.Resources.PremiumPreviewVoiceToTextDescription;
                    break;
            }

            var title = content.FindName("Title") as TextBlock;
            var subtitle = content.FindName("Subtitle") as TextBlock;
            var icon = content.FindName("Icon") as TextBlock;
            var iconPanel = content.FindName("IconPanel") as Border;

            title.Text = titleValue;
            subtitle.Text = subtitleValue;
            icon.Text = iconValue;
            iconPanel.Background = new SolidColorBrush(_gradient[args.ItemIndex]);
        }

        public string ConvertTitle(bool premium, bool title)
        {
            if (title)
            {
                return premium ? Strings.Resources.TelegramPremiumSubscribedTitle : Strings.Resources.TelegramPremium;
            }

            return premium ? Strings.Resources.TelegramPremiumSubscribedSubtitle : Strings.Resources.TelegramPremiumSubtitle;
        }

        public string ConvertPurchase(bool premium, long amount, string currency)
        {
            if (premium)
            {
                return Strings.Resources.OK;
            }

            return string.Format(Strings.Resources.SubscribeToPremium, Locale.FormatCurrency(amount, currency));
        }

        private void PurchaseShadow_Loaded(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            DropShadowEx.Attach(PurchaseShadow);
        }

        private void Purchase_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            Hide();
            ViewModel.Purchase();
        }
    }
}
