using System;
using Telegram.Charts;
using Telegram.Controls;
using Telegram.Controls.Cells;
using Telegram.Controls.Cells.Monetization;
using Telegram.Converters;
using Telegram.Streams;
using Telegram.ViewModels.Chats;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Telegram.Views.Chats
{
    public sealed partial class ChatMonetizationPage : HostedPage
    {
        public ChatMonetizationViewModel ViewModel => DataContext as ChatMonetizationViewModel;

        public ChatMonetizationPage()
        {
            InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            ViewModel.PropertyChanged += OnPropertyChanged;

            UpdateAmount(ViewModel.AvailableAmount);
            AvailableAmount.UpdateAmount(ViewModel.ClientService, ViewModel.AvailableAmount);
            PreviousAmount.UpdateAmount(ViewModel.ClientService, ViewModel.PreviousAmount);
            TotalAmount.UpdateAmount(ViewModel.ClientService, ViewModel.TotalAmount);

            FooterInfo.Text = string.Format(Strings.MonetizationInfo, 50);
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            ViewModel.PropertyChanged -= OnPropertyChanged;
        }

        private void OnPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(AvailableAmount))
            {
                UpdateAmount(ViewModel.AvailableAmount);
                AvailableAmount.UpdateAmount(ViewModel.ClientService, ViewModel.AvailableAmount);
            }
            else if (e.PropertyName == nameof(PreviousAmount))
            {
                PreviousAmount.UpdateAmount(ViewModel.ClientService, ViewModel.PreviousAmount);
            }
            else if (e.PropertyName == nameof(TotalAmount))
            {
                TotalAmount.UpdateAmount(ViewModel.ClientService, ViewModel.TotalAmount);
            }
        }

        public void UpdateAmount(CryptoAmount value)
        {
            if (value == null)
            {
                return;
            }

            var doubleAmount = value.CryptocurrencyAmount / 100.0;
            var integerAmount = Math.Truncate(doubleAmount);
            var decimalAmount = (int)((decimal)doubleAmount % 1 * 100);

            CryptocurrencyAmountLabel.Text = integerAmount.ToString("N0");
            CryptocurrencyDecimalLabel.Text = string.Format(".{0:N0}", decimalAmount);

            Icon.Source = new AnimatedEmojiFileSource(ViewModel.ClientService, "\U0001F48E");

            AmountLabel.Text = string.Format("~{0}", Formatter.FormatAmount(value.Amount, value.Currency));
        }

        private void OnElementPrepared(FrameworkElement sender, DataContextChangedEventArgs args)
        {
            var root = sender as ChartCell;
            var data = args.NewValue as ChartViewData;

            if (root == null)
            {
                return;
            }

            var header = root.Items[0] as ChartHeaderView;
            var border = root.Items[1] as AspectView;
            var checks = root.Items[2] as WrapPanel;

            root.Header = data?.title ?? string.Empty;
            border.Children.Clear();
            border.Constraint = data;

            root.UpdateData(data);
        }

        private void OnItemClick(object sender, ItemClickEventArgs e)
        {
            ViewModel.ShowTransaction(e.ClickedItem as TransactionInfo);
        }

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }
            else if (args.ItemContainer.ContentTemplateRoot is TransactionCell cell && args.Item is TransactionInfo info)
            {
                cell.UpdateInfo(info);
            }
        }
    }
}
