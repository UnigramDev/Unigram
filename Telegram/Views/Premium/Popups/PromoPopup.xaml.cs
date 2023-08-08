//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Controls.Media;
using Telegram.Td.Api;
using Telegram.ViewModels.Premium;
using Windows.UI;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace Telegram.Views.Premium.Popups
{
    public sealed partial class PromoPopup : ContentPopup
    {
        public PromoViewModel ViewModel => DataContext as PromoViewModel;

        public PromoPopup()
        {
            InitializeComponent();
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
            Color.FromArgb(0xFF, 0xF2, 0x87, 0x2C),
            Color.FromArgb(0xFF, 0xEC, 0x7C, 0x47),
            Color.FromArgb(0xFF, 0xE7, 0x72, 0x62),
            Color.FromArgb(0xFF, 0xE1, 0x68, 0x7E),
            Color.FromArgb(0xFF, 0xDC, 0x5D, 0x99),
            Color.FromArgb(0xFF, 0xC9, 0x60, 0xBF),
            Color.FromArgb(0xFF, 0xB4, 0x64, 0xE7),
            Color.FromArgb(0xFF, 0x9B, 0x70, 0xFF),
            Color.FromArgb(0xFF, 0x73, 0x8E, 0xFF),
            Color.FromArgb(0xFF, 0x55, 0xA5, 0xFF),
            Color.FromArgb(0xFF, 0x51, 0xB2, 0xC3),
            Color.FromArgb(0xFF, 0x4F, 0xBC, 0x95),
            Color.FromArgb(0xFF, 0x4C, 0xC5, 0x67),
        };

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }

            var feature = args.Item as PremiumFeature;
            var content = args.ItemContainer.ContentTemplateRoot as Grid;

            var badge = false;

            var iconValue = string.Empty;
            var titleValue = string.Empty;
            var subtitleValue = string.Empty;

            switch (feature)
            {
                case PremiumFeatureAdvancedChatManagement:
                    iconValue = Icons.ChatFilled;
                    titleValue = Strings.PremiumPreviewAdvancedChatManagement;
                    subtitleValue = Strings.PremiumPreviewAdvancedChatManagementDescription;
                    break;
                case PremiumFeatureAnimatedProfilePhoto:
                    iconValue = Icons.PlayCircleFilled;
                    titleValue = Strings.PremiumPreviewAnimatedProfiles;
                    subtitleValue = Strings.PremiumPreviewAnimatedProfilesDescription;
                    break;
                case PremiumFeatureAppIcons:
                    titleValue = Strings.PremiumPreviewAppIcon;
                    subtitleValue = Strings.PremiumPreviewAppIconDescription;
                    break;
                case PremiumFeatureCustomEmoji:
                    iconValue = Icons.EmojiFilled;
                    titleValue = Strings.PremiumPreviewEmoji;
                    subtitleValue = Strings.PremiumPreviewEmojiDescription;
                    break;
                case PremiumFeatureDisabledAds:
                    iconValue = Icons.MegaphoneOffFilled;
                    titleValue = Strings.PremiumPreviewNoAds;
                    subtitleValue = Strings.PremiumPreviewNoAdsDescription;
                    break;
                case PremiumFeatureEmojiStatus:
                    iconValue = Icons.ReactionFilled24;
                    titleValue = Strings.PremiumPreviewEmojiStatus;
                    subtitleValue = Strings.PremiumPreviewEmojiStatusDescription;
                    break;
                case PremiumFeatureImprovedDownloadSpeed:
                    iconValue = Icons.TopSpeedFilled;
                    titleValue = Strings.PremiumPreviewDownloadSpeed;
                    subtitleValue = Strings.PremiumPreviewDownloadSpeedDescription;
                    break;
                case PremiumFeatureIncreasedLimits:
                    iconValue = Icons.Multiplier2xFilled;
                    titleValue = Strings.PremiumPreviewLimits;
                    subtitleValue = ViewModel.PremiumPreviewLimitsDescription;
                    break;
                case PremiumFeatureIncreasedUploadFileSize:
                    iconValue = Icons.DocumentFilled;
                    titleValue = Strings.PremiumPreviewUploads;
                    subtitleValue = Strings.PremiumPreviewUploadsDescription;
                    break;
                case PremiumFeatureProfileBadge:
                    iconValue = Icons.Premium16;
                    titleValue = Strings.PremiumPreviewProfileBadge;
                    subtitleValue = Strings.PremiumPreviewProfileBadgeDescription;
                    break;
                case PremiumFeatureRealTimeChatTranslation:
                    iconValue = Icons.TranslateFilled;
                    titleValue = Strings.PremiumPreviewTranslations;
                    subtitleValue = Strings.PremiumPreviewTranslationsDescription;
                    break;
                case PremiumFeatureUniqueReactions:
                    iconValue = Icons.HeartFilled;
                    titleValue = Strings.PremiumPreviewReactions2;
                    subtitleValue = Strings.PremiumPreviewReactions2Description;
                    break;
                case PremiumFeatureUniqueStickers:
                    iconValue = Icons.StickerFilled;
                    titleValue = Strings.PremiumPreviewStickers;
                    subtitleValue = Strings.PremiumPreviewStickersDescription;
                    break;
                case PremiumFeatureUpgradedStories:
                    iconValue = Icons.Stories;
                    titleValue = Strings.PremiumPreviewStories;
                    subtitleValue = Strings.PremiumPreviewStoriesDescription;
                    badge = true;
                    break;
                case PremiumFeatureVoiceRecognition:
                    iconValue = Icons.MicOnFilled;
                    titleValue = Strings.PremiumPreviewVoiceToText;
                    subtitleValue = Strings.PremiumPreviewVoiceToTextDescription;
                    break;
            }

            var title = content.FindName("Title") as TextBlock;
            var subtitle = content.FindName("Subtitle") as TextBlock;
            var icon = content.FindName("Icon") as TextBlock;
            var iconPanel = content.FindName("IconPanel") as Border;
            var badgeControl = content.FindName("Badge") as BadgeControl;

            var index = Math.Min(args.ItemIndex, _gradient.Length - 1);

            title.Text = titleValue;
            subtitle.Text = subtitleValue;
            icon.Text = iconValue;
            iconPanel.Background = new SolidColorBrush(_gradient[index]);
            badgeControl.Visibility = badge ? Windows.UI.Xaml.Visibility.Visible : Windows.UI.Xaml.Visibility.Collapsed;
        }

        public string ConvertTitle(bool premium, bool title)
        {
            if (title)
            {
                return premium ? Strings.TelegramPremiumSubscribedTitle : Strings.TelegramPremium;
            }

            return premium ? Strings.TelegramPremiumSubscribedSubtitle : Strings.TelegramPremiumSubtitle;
        }

        public string ConvertPurchase(bool premium, PremiumStatePaymentOption option)
        {
            return GetPaymentString(premium, option?.PaymentOption);
        }

        public static string GetPaymentString(bool premium, PremiumPaymentOption option)
        {
            // TODO
            if (premium || option == null)
            {
                return Strings.OK;
            }

            return string.Format(premium
                ? option.MonthCount == 12
                ? Strings.UpgradePremiumPerYear
                : Strings.UpgradePremiumPerMonth
                : option.MonthCount == 12
                ? Strings.SubscribeToPremiumPerYear
                : Strings.SubscribeToPremium, Locale.FormatCurrency(option.MonthCount == 12 ? option.Amount : option.Amount / option.MonthCount, option.Currency));
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
