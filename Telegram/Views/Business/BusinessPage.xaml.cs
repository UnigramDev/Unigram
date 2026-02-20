//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Telegram.Common;
using Telegram.Controls;
using Telegram.Controls.Media;
using Telegram.Converters;
using Telegram.Td.Api;
using Telegram.ViewModels.Business;
using Telegram.Views.Premium.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace Telegram.Views.Business
{
    public sealed partial class BusinessPage : HostedPage
    {
        public BusinessViewModel ViewModel => DataContext as BusinessViewModel;

        public BusinessPage()
        {
            InitializeComponent();
            Title = Strings.TelegramBusiness;

            Headline.Text = Strings.TelegramBusinessSubtitle;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            Headline.Text = ViewModel.IsPremium
                ? Strings.TelegramBusinessSubscribedSubtitleTemp
                : Strings.TelegramBusinessSubtitleTemp;
        }

        private void OnChoosingItemContainer(ListViewBase sender, ChoosingItemContainerEventArgs args)
        {
            if (args.ItemContainer == null)
            {
                args.ItemContainer = new TableListViewItem();
                args.ItemContainer.Style = sender.ItemContainerStyle;
                args.ItemContainer.ContentTemplate = sender.ItemTemplate;
            }

            args.IsContainerPrepared = true;
        }

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }

            var feature = args.Item as BusinessFeature;
            var content = args.ItemContainer.ContentTemplateRoot as Grid;

            var iconValue = string.Empty;
            var titleValue = string.Empty;
            var subtitleValue = string.Empty;

            switch (feature)
            {
                case BusinessFeatureGreetingMessage:
                    iconValue = Icons.HandWaveFilled;
                    titleValue = Strings.PremiumBusinessGreetingMessages;
                    subtitleValue = Strings.PremiumBusinessGreetingMessagesDescription;
                    break;
                case BusinessFeatureAwayMessage:
                    iconValue = Icons.ChatSnoozeFilled;
                    titleValue = Strings.PremiumBusinessAwayMessages;
                    subtitleValue = Strings.PremiumBusinessAwayMessagesDescription;
                    break;
                case BusinessFeatureQuickReplies:
                    iconValue = Icons.ArrowReplyFilled;
                    titleValue = Strings.PremiumBusinessQuickReplies;
                    subtitleValue = Strings.PremiumBusinessQuickRepliesDescription;
                    break;
                case BusinessFeatureOpeningHours:
                    iconValue = Icons.ClockFilled;
                    titleValue = Strings.PremiumBusinessOpeningHours;
                    subtitleValue = Strings.PremiumBusinessOpeningHoursDescription;
                    break;
                case BusinessFeatureLocation:
                    iconValue = Icons.LocationFilled;
                    titleValue = Strings.PremiumBusinessLocation;
                    subtitleValue = Strings.PremiumBusinessLocationDescription;
                    break;
                case BusinessFeatureBots:
                    iconValue = Icons.BotFilled;
                    titleValue = Strings.PremiumBusinessChatbots2;
                    subtitleValue = Strings.PremiumBusinessChatbotsDescription;
                    break;
                case BusinessFeatureStartPage:
                    iconValue = Icons.ChatInfoFilled;
                    titleValue = Strings.PremiumBusinessIntro;
                    subtitleValue = Strings.PremiumBusinessIntroDescription;
                    break;
                case BusinessFeatureAccountLinks:
                    iconValue = Icons.ChatLinkFilled;
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
                case BusinessFeatureUpgradedStories:
                    titleValue = Strings.PremiumPreviewBusinessStories;
                    subtitleValue = Strings.PremiumPreviewBusinessStoriesDescription;
                    break;
            }

            var title = content.FindName("Title") as TextBlock;
            var subtitle = content.FindName("Subtitle") as TextBlock;
            var icon = content.FindName("Icon") as TextBlock;
            var iconPanel = content.FindName("IconPanel") as Border;
            var badgeControl = content.FindName("Badge") as BadgeControl;

            title.Text = titleValue;
            subtitle.Text = subtitleValue;
            icon.Text = iconValue;
            iconPanel.Background = new SolidColorBrush(ColorsHelper.CalculateColor(PromoPopup.Gradient, (float)args.ItemIndex / (sender.Items.Count - 1)));

            //if (badge)
            //{
            //    badgeControl.Background = new SolidColorBrush(_gradient[index]);
            //    badgeControl.Visibility = Windows.UI.Xaml.Visibility.Visible;
            //}
            //else
            //{
            //    badgeControl.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
            //}

            args.Handled = true;
        }

        private void OnItemClick(object sender, ItemClickEventArgs e)
        {
            ViewModel.OpenFeature(e.ClickedItem as BusinessFeature);
        }

        private void OnDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
        {
            if (args.NewValue is not PremiumStatePaymentOption paymentOption)
            {
                return;
            }

            var option = paymentOption.PaymentOption;

            var title = sender.FindName("Title") as TextBlock;
            var badge = sender.FindName("Badge") as BadgeControl;

            var total = sender.FindName("Total") as TextBlock;
            var price = sender.FindName("Price") as TextBlock;

            if (option.DiscountPercentage > 0)
            {
                var reference = ViewModel.MonthlyOption;
                if (reference == null)
                {
                    return;
                }

                badge.Visibility = Visibility.Visible;
                total.Visibility = Visibility.Visible;

                badge.Text = string.Format(Strings.GiftPremiumOptionDiscount, option.DiscountPercentage);

                total.Inlines.Clear();
                total.Inlines.Add(string.Format(Strings.PricePerYear, Formatter.FormatAmount(reference.PaymentOption.Amount * 12, reference.PaymentOption.Currency)), Windows.UI.Text.TextDecorations.Strikethrough);
                total.Inlines.Add(Icons.Spacing);
                total.Inlines.Add(string.Format(Strings.PricePerYear, Formatter.FormatAmount(option.Amount, option.Currency)));
            }
            else
            {
                badge.Visibility = Visibility.Collapsed;
                total.Visibility = Visibility.Collapsed;
            }

            title.Text = option.MonthCount switch
            {
                1 => Strings.PremiumTierMonthly,
                6 => Strings.PremiumTierSemiannual,
                12 => Strings.PremiumTierAnnual,
                _ => option.MonthCount.ToString()
            };

            price.Text = string.Format(Strings.PricePerMonthMe, Formatter.FormatAmount(option.Amount / option.MonthCount, option.Currency));
        }

        public string ConvertPurchase(bool premium, PremiumStatePaymentOption option)
        {
            return GetPaymentString(premium, option?.PaymentOption);
        }

        public static string GetPaymentString(bool premium, PremiumPaymentOption option)
        {
            // TODO
            if (option == null)
            {
                return Strings.OK;
            }

            return string.Format(premium
                ? Strings.UpgradePremiumPerMonth
                : Strings.SubscribeToPremium, Locale.FormatCurrency(option.Amount / option.MonthCount, option.Currency));
        }

        private void PurchaseShadow_Loaded(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            VisualUtilities.DropShadow(PurchaseShadow);
        }

        private void Purchase_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            //Hide();
            //ViewModel.Purchase();
        }
    }
}
