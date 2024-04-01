//
// Copyright Fela Ameghino 2015-2024
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
            Color.FromArgb(0xFF, 0xef, 0x69, 0x22),
            Color.FromArgb(0xFF, 0xe9, 0x5a, 0x2c),
            Color.FromArgb(0xFF, 0xe7, 0x4e, 0x33),
            Color.FromArgb(0xFF, 0xe5, 0x49, 0x37),
            Color.FromArgb(0xFF, 0xe3, 0x43, 0x3c),
            Color.FromArgb(0xFF, 0xdb, 0x37, 0x4b),
            Color.FromArgb(0xFF, 0xcb, 0x3e, 0x6d),
            Color.FromArgb(0xFF, 0xbc, 0x43, 0x95),
            Color.FromArgb(0xFF, 0xab, 0x4a, 0xc4),
            Color.FromArgb(0xFF, 0xa3, 0x4c, 0xd7),
            Color.FromArgb(0xFF, 0x9b, 0x4f, 0xed),
            Color.FromArgb(0xFF, 0x89, 0x58, 0xff),
            Color.FromArgb(0xFF, 0x67, 0x6b, 0xff),
            Color.FromArgb(0xFF, 0x61, 0x72, 0xff),
            Color.FromArgb(0xFF, 0x5b, 0x79, 0xff),
            Color.FromArgb(0xFF, 0x44, 0x92, 0xff),
            Color.FromArgb(0xFF, 0x42, 0x9b, 0xd5),
            Color.FromArgb(0xFF, 0x41, 0xa6, 0xa5),
            Color.FromArgb(0xFF, 0x3e, 0xb2, 0x6d),
            Color.FromArgb(0xFF, 0x3d, 0xbd, 0x4a),
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
                case PremiumFeatureAccentColor:
                    iconValue = Icons.PaintBrushFilled;
                    titleValue = Strings.PremiumPreviewProfileColor;
                    subtitleValue = Strings.PremiumPreviewProfileColorDescription;
                    break;
                case PremiumFeatureBackgroundForBoth:
                    iconValue = Icons.WallpaperFilled;
                    titleValue = Strings.PremiumPreviewWallpaper;
                    subtitleValue = Strings.PremiumPreviewWallpaperDescription;
                    break;
                case PremiumFeatureAdvancedChatManagement:
                    iconValue = Icons.ChatSettingsFilled;
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
                    iconValue = Icons.HandOpenHeartFilled;
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
                case PremiumFeatureSavedMessagesTags:
                    iconValue = Icons.TagFilled;
                    titleValue = Strings.PremiumPreviewTags2;
                    subtitleValue = Strings.PremiumPreviewTagsDescription2;
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
                    break;
                case PremiumFeatureVoiceRecognition:
                    iconValue = Icons.MicOnFilled;
                    titleValue = Strings.PremiumPreviewVoiceToText;
                    subtitleValue = Strings.PremiumPreviewVoiceToTextDescription;
                    break;
                case PremiumFeatureLastSeenTimes:
                    iconValue = Icons.LastSeenFilled;
                    titleValue = Strings.PremiumPreviewLastSeen;
                    subtitleValue = Strings.PremiumPreviewLastSeenDescription;
                    break;
                case PremiumFeatureMessagePrivacy:
                    iconValue = Icons.ChatLockedFilled;
                    titleValue = Strings.PremiumPreviewMessagePrivacy;
                    subtitleValue = Strings.PremiumPreviewMessagePrivacyDescription;
                    break;
                case PremiumFeatureBusiness:
                    iconValue = Icons.BuildingShopFilled;
                    titleValue = Strings.TelegramBusiness;
                    subtitleValue = Strings.PremiumPreviewBusinessDescription;
                    badge = true;
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

            if (badge)
            {
                badgeControl.Background = new SolidColorBrush(_gradient[index]);
                badgeControl.Visibility = Windows.UI.Xaml.Visibility.Visible;
            }
            else
            {
                badgeControl.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
            }

            args.Handled = true;
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
