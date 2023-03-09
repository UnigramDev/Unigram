//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Converters;
using Unigram.ViewModels.Premium;
using Windows.UI;

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
                case PremiumFeatureEmojiStatus:
                    iconValue = Icons.ReactionFilled24;
                    titleValue = Strings.Resources.PremiumPreviewEmojiStatus;
                    subtitleValue = Strings.Resources.PremiumPreviewEmojiStatusDescription;
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

            var index = Math.Min(args.ItemIndex, _gradient.Length - 1);

            title.Text = titleValue;
            subtitle.Text = subtitleValue;
            icon.Text = iconValue;
            iconPanel.Background = new SolidColorBrush(_gradient[index]);
        }

        public string ConvertTitle(bool premium, bool title)
        {
            if (title)
            {
                return premium ? Strings.Resources.TelegramPremiumSubscribedTitle : Strings.Resources.TelegramPremium;
            }

            return premium ? Strings.Resources.TelegramPremiumSubscribedSubtitle : Strings.Resources.TelegramPremiumSubtitle;
        }

        public string ConvertPurchase(bool premium, PremiumStatePaymentOption option)
        {
            if (premium || option == null)
            {
                return Strings.Resources.OK;
            }

            return string.Format(Strings.Resources.SubscribeToPremium,
                Locale.FormatCurrency(option.PaymentOption.Amount / option.PaymentOption.MonthCount, option.PaymentOption.Currency));
        }

        private void PurchaseShadow_Loaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            DropShadowEx.Attach(PurchaseShadow);
        }

        private void Purchase_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            Hide();
            ViewModel.Purchase();
        }
    }
}
