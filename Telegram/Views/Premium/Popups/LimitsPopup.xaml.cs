//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Collections.Generic;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Services;
using Telegram.Td.Api;
using Windows.UI;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace Telegram.Views.Premium.Popups
{
    public sealed partial class LimitsPopup : ContentPopup
    {
        private readonly Color[] _gradient = new Color[]
        {
            Color.FromArgb(0xFF, 0x5B, 0xA0, 0xFE),
            Color.FromArgb(0xFF, 0x79, 0x8A, 0xFF),
            Color.FromArgb(0xFF, 0x93, 0x77, 0xFF),
            Color.FromArgb(0xFF, 0xAC, 0x64, 0xF2),
            Color.FromArgb(0xFF, 0xC4, 0x56, 0xAE),
            Color.FromArgb(0xFF, 0xCF, 0x57, 0x9A),
            Color.FromArgb(0xFF, 0xDB, 0x58, 0x87),
            Color.FromArgb(0xFF, 0xDA, 0x49, 0x6F),
            Color.FromArgb(0xFF, 0xE9, 0x5D, 0x44),
            Color.FromArgb(0xFF, 0xF2, 0x82, 0x2A)
        };

        public LimitsPopup(IClientService clientService, PremiumPaymentOption option, IList<PremiumLimit> limits)
        {
            InitializeComponent();

            Title = Strings.DoubledLimits;

            ScrollingHost.ItemsSource = limits;
            PurchaseCommand.Content = PromoPopup.GetPaymentString(clientService.IsPremium, option);

            clientService.Send(new ViewPremiumFeature(new PremiumFeatureIncreasedLimits()));
        }

        public bool ShouldPurchase { get; private set; }

        private void Purchase_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            ShouldPurchase = true;
            Hide();
        }

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }

            var limit = args.Item as PremiumLimit;
            var content = args.ItemContainer.ContentTemplateRoot as Grid;

            var titleValue = string.Empty;
            var subtitleValue = string.Empty;

            switch (limit.Type)
            {
                case PremiumLimitTypeBioLength:
                    titleValue = Strings.BioLimitTitle;
                    subtitleValue = Strings.BioLimitSubtitle;
                    break;
                case PremiumLimitTypeCaptionLength:
                    titleValue = Strings.CaptionsLimitTitle;
                    subtitleValue = Strings.CaptionsLimitSubtitle;
                    break;
                case PremiumLimitTypeChatFolderChosenChatCount:
                    titleValue = Strings.ChatPerFolderLimitTitle;
                    subtitleValue = Strings.ChatPerFolderLimitSubtitle;
                    break;
                case PremiumLimitTypeChatFolderCount:
                    titleValue = Strings.FoldersLimitTitle;
                    subtitleValue = Strings.FoldersLimitSubtitle;
                    break;
                case PremiumLimitTypeCreatedPublicChatCount:
                    titleValue = Strings.PublicLinksLimitTitle;
                    subtitleValue = Strings.PublicLinksLimitSubtitle;
                    break;
                case PremiumLimitTypeFavoriteStickerCount:
                    titleValue = Strings.FavoriteStickersLimitTitle;
                    subtitleValue = Strings.FavoriteStickersLimitSubtitle;
                    break;
                case PremiumLimitTypePinnedArchivedChatCount:
                    titleValue = "";
                    subtitleValue = "";
                    break;
                case PremiumLimitTypePinnedChatCount:
                    titleValue = Strings.PinChatsLimitTitle;
                    subtitleValue = Strings.PinChatsLimitSubtitle;
                    break;
                case PremiumLimitTypeSavedAnimationCount:
                    titleValue = Strings.SavedGifsLimitTitle;
                    subtitleValue = Strings.SavedGifsLimitSubtitle;
                    break;
                case PremiumLimitTypeSupergroupCount:
                    titleValue = Strings.GroupsAndChannelsLimitTitle;
                    subtitleValue = Strings.GroupsAndChannelsLimitSubtitle;
                    break;
                case PremiumLimitTypeConnectedAccounts:
                    titleValue = Strings.ConnectedAccountsLimitTitle;
                    subtitleValue = Strings.ConnectedAccountsLimitSubtitle;
                    break;
            }

            var title = content.FindName("Title") as TextBlock;
            var subtitle = content.FindName("Subtitle") as TextBlock;
            var prevLimit = content.FindName("PrevLimit") as TextBlock;
            var nextLimit = content.FindName("NextLimit") as TextBlock;
            var nextPanel = content.FindName("NextPanel") as Grid;

            title.Text = titleValue;
            subtitle.Text = string.Format(subtitleValue, limit.PremiumValue);
            prevLimit.Text = limit.DefaultValue.ToString();
            nextLimit.Text = limit.PremiumValue.ToString();
            nextPanel.Background = new SolidColorBrush(_gradient[Math.Min(args.ItemIndex, _gradient.Length - 1)]);
        }

        private void PurchaseShadow_Loaded(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            DropShadowEx.Attach(PurchaseShadow);
        }
    }
}
