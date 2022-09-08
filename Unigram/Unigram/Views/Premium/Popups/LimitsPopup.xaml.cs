using System.Collections.Generic;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Services;
using Windows.UI;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace Unigram.Views.Premium.Popups
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

        public LimitsPopup(IProtoService protoService, PremiumPaymentOption option, IList<PremiumLimit> limits)
        {
            InitializeComponent();

            Title = Strings.Resources.DoubledLimits;

            ScrollingHost.ItemsSource = limits;

            PurchaseCommand.Content = protoService.IsPremium
                ? Strings.Resources.OK
                : string.Format(Strings.Resources.SubscribeToPremium, Locale.FormatCurrency(option.Amount / option.MonthCount, option.Currency));
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
                    titleValue = Strings.Resources.BioLimitTitle;
                    subtitleValue = Strings.Resources.BioLimitSubtitle;
                    break;
                case PremiumLimitTypeCaptionLength:
                    titleValue = Strings.Resources.CaptionsLimitTitle;
                    subtitleValue = Strings.Resources.CaptionsLimitSubtitle;
                    break;
                case PremiumLimitTypeChatFilterChosenChatCount:
                    titleValue = Strings.Resources.ChatPerFolderLimitTitle;
                    subtitleValue = Strings.Resources.ChatPerFolderLimitSubtitle;
                    break;
                case PremiumLimitTypeChatFilterCount:
                    titleValue = Strings.Resources.FoldersLimitTitle;
                    subtitleValue = Strings.Resources.FoldersLimitSubtitle;
                    break;
                case PremiumLimitTypeCreatedPublicChatCount:
                    titleValue = Strings.Resources.PublicLinksLimitTitle;
                    subtitleValue = Strings.Resources.PublicLinksLimitSubtitle;
                    break;
                case PremiumLimitTypeFavoriteStickerCount:
                    titleValue = Strings.Resources.FavoriteStickersLimitTitle;
                    subtitleValue = Strings.Resources.FavoriteStickersLimitSubtitle;
                    break;
                case PremiumLimitTypePinnedArchivedChatCount:
                    titleValue = "";
                    subtitleValue = "";
                    break;
                case PremiumLimitTypePinnedChatCount:
                    titleValue = Strings.Resources.PinChatsLimitTitle;
                    subtitleValue = Strings.Resources.PinChatsLimitSubtitle;
                    break;
                case PremiumLimitTypeSavedAnimationCount:
                    titleValue = Strings.Resources.SavedGifsLimitTitle;
                    subtitleValue = Strings.Resources.SavedGifsLimitSubtitle;
                    break;
                case PremiumLimitTypeSupergroupCount:
                    titleValue = Strings.Resources.GroupsAndChannelsLimitTitle;
                    subtitleValue = Strings.Resources.GroupsAndChannelsLimitSubtitle;
                    break;
                case PremiumLimitTypeConnectedAccounts:
                    titleValue = Strings.Resources.ConnectedAccountsLimitTitle;
                    subtitleValue = Strings.Resources.ConnectedAccountsLimitSubtitle;
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
            nextPanel.Background = new SolidColorBrush(_gradient[args.ItemIndex]);
        }

        private void PurchaseShadow_Loaded(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            DropShadowEx.Attach(PurchaseShadow);
        }
    }
}
