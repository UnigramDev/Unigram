using System;
using System.Globalization;
using Telegram.Common;
using Telegram.Controls.Media;
using Telegram.Converters;
using Telegram.ViewModels.Chats;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

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

            UpdateAmount(ViewModel.AvailableAmount);
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
                UpdateAmount(ViewModel.AvailableAmount);
            }
            else if (e.PropertyName == nameof(ViewModel.NextWithdrawalDate))
            {
                UpdateCountdown();
            }
        }

        private void UpdateAmount(CryptoAmount value)
        {
            if (value == null)
            {
                return;
            }

            var doubleAmount = Formatter.Amount(value.CryptocurrencyAmount, value.Cryptocurrency);
            var stringAmount = doubleAmount.ToString(CultureInfo.InvariantCulture).Split('.');
            var integerAmount = long.Parse(stringAmount[0]);
            var decimalAmount = stringAmount.Length > 1 ? stringAmount[1] : "0";

            CryptocurrencyAmountLabel.Text = integerAmount.ToString("N0");

            AmountLabel.Text = string.Format("~{0}", Formatter.FormatAmount((long)(value.CryptocurrencyAmount * value.UsdRate), "USD"));
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
                TransferCountdown.Text = Icons.LockClosedFilled12 + Icons.Spacing + diff.GetDuration();
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
