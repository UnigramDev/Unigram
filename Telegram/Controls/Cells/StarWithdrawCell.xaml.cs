using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Globalization;
using Telegram.Common;
using Telegram.Controls.Media;
using Telegram.Converters;
using Telegram.Native;
using Telegram.Td.Api;
using Telegram.ViewModels.Chats;

namespace Telegram.Controls.Cells
{
    public sealed partial class StarWithdrawCell : UserControl
    {
        public ChatStarsViewModel ViewModel => DataContext as ChatStarsViewModel;

        public StarWithdrawCell()
        {
            InitializeComponent();
        }

        private DispatcherTimer _countdownTimer;

        public void OnNavigatedTo(NavigationEventArgs e)
        {
            ViewModel.PropertyChanged += OnPropertyChanged;

            UpdateAmount(ViewModel.AvailableAmount, ViewModel.UsdRate);
            UpdateCountdown();
        }

        public void OnNavigatedFrom(NavigationEventArgs e)
        {
            ViewModel.PropertyChanged -= OnPropertyChanged;
        }

        private void OnPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ViewModel.AvailableAmount))
            {
                UpdateAmount(ViewModel.AvailableAmount, ViewModel.UsdRate);
            }
            else if (e.PropertyName == nameof(ViewModel.NextWithdrawalDate))
            {
                UpdateCountdown();
            }
        }

        private void UpdateAmount(StarAmount amount, double usdRate)
        {
            if (amount == null)
            {
                return;
            }

            var integerAmount = Math.Abs(amount.StarCount);
            var decimalAmount = Math.Abs(amount.NanostarCount);

            var culture = new CultureInfo(NativeUtils.GetCurrentCulture());
            var separator = culture.NumberFormat.NumberDecimalSeparator;

            CryptocurrencyAmountLabel.Text = integerAmount.ToString("N0");
            CryptocurrencyDecimalLabel.Text = decimalAmount > 0 ? string.Format("{0}{1}", separator, decimalAmount) : string.Empty;

            AmountLabel.Text = string.Format("~{0}", Formatter.FormatAmount((long)(amount.StarCount * usdRate), "USD"));
        }

        private void UpdateCountdown()
        {
            _countdownTimer?.Stop();

            if (ViewModel.NextWithdrawalDate != 0)
            {
                if (_countdownTimer == null)
                {
                    _countdownTimer = new DispatcherTimer();
                    _countdownTimer.Interval = TimeSpan.FromMilliseconds(500);
                    _countdownTimer.Tick += Countdown_Tick;
                }

                _countdownTimer.Start();
            }
        }

        private void Countdown_Tick(object sender, object e)
        {
            var date = Formatter.ToLocalTime(ViewModel.NextWithdrawalDate);
            var diff = date - DateTime.Now;

            if (diff > TimeSpan.Zero)
            {
                TransferCountdown.Text = Icons.LockClosedFilled12 + Icons.Spacing + diff.ToDuration();
                TransferCountdown.Visibility = Visibility.Visible;
                TransferText.Margin = new Thickness(0, -4, 0, 0);
            }
            else
            {
                _countdownTimer.Stop();
                TransferCountdown.Visibility = Visibility.Collapsed;
                TransferText.Margin = new Thickness(0);
            }
        }
    }
}
