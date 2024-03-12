using System;
using Telegram.Controls;
using Telegram.Controls.Media;
using Telegram.Td.Api;
using Telegram.ViewModels.Business;
using Windows.UI;
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

        private void Location_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(BusinessLocationPage));
        }

        private void Hours_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(BusinessHoursPage));
        }

        private void Replies_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(BusinessRepliesPage));
        }

        private void Greet_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(BusinessGreetPage));
        }

        private void Away_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(BusinessAwayPage));
        }

        private void Bots_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(BusinessBotsPage));
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

        private readonly Color[] _gradient = new Color[]
        {
            Color.FromArgb(0xFF, 0xe5, 0x49, 0x37),
            Color.FromArgb(0xFF, 0xcb, 0x3e, 0x6d),
            Color.FromArgb(0xFF, 0xa3, 0x4c, 0xd7),
            Color.FromArgb(0xFF, 0x67, 0x6b, 0xff),
            Color.FromArgb(0xFF, 0x42, 0x9b, 0xd5),
            Color.FromArgb(0xFF, 0x3d, 0xbd, 0x4a),
        };

        private readonly Color[] _gradientTop = new Color[]
        {
            Color.FromArgb(0xFF, 0xef, 0x69, 0x22),
            Color.FromArgb(0xFF, 0xe5, 0x49, 0x37),
            Color.FromArgb(0xFF, 0xcb, 0x3e, 0x6d),
            Color.FromArgb(0xFF, 0xa3, 0x4c, 0xd7),
            Color.FromArgb(0xFF, 0x67, 0x6b, 0xff),
            Color.FromArgb(0xFF, 0x42, 0x9b, 0xd5),
        };

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }

            var feature = args.Item as BusinessFeature;
            var content = args.ItemContainer.ContentTemplateRoot as Grid;

            var badge = false;

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
                case BusinessFeatureConnectedBots:
                    iconValue = Icons.BotFilled;
                    titleValue = Strings.PremiumBusinessChatbots;
                    subtitleValue = Strings.PremiumBusinessChatbotsDescription;
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
            iconPanel.Background = new LinearGradientBrush(new GradientStopCollection
            {
                new GradientStop { Color = _gradientTop[index] },
                new GradientStop { Color = _gradient[index], Offset = 1 }
            }, 90);

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
            switch (e.ClickedItem as BusinessFeature)
            {
                case BusinessFeatureGreetingMessage:
                    Frame.Navigate(typeof(BusinessGreetPage));
                    break;
                case BusinessFeatureAwayMessage:
                    Frame.Navigate(typeof(BusinessAwayPage));
                    break;
                case BusinessFeatureQuickReplies:
                    Frame.Navigate(typeof(BusinessRepliesPage));
                    break;
                case BusinessFeatureOpeningHours:
                    Frame.Navigate(typeof(BusinessHoursPage));
                    break;
                case BusinessFeatureLocation:
                    Frame.Navigate(typeof(BusinessLocationPage));
                    break;
                case BusinessFeatureConnectedBots:
                    Frame.Navigate(typeof(BusinessBotsPage));
                    break;
            }
        }
    }
}
