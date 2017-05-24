using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Aggregator;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Unigram.Common;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Unigram.ViewModels.Settings
{
    public class SettingsPrivacyViewModel : UnigramViewModelBase
    {
        public SettingsPrivacyViewModel(IMTProtoService protoService, ICacheService cacheService, ITelegramEventAggregator aggregator)
            : base(protoService, cacheService, aggregator)
        {
        }

        public RelayCommand ClearPaymentsCommand => new RelayCommand(ClearPaymentsExecute);
        private async void ClearPaymentsExecute()
        {
            var dialog = new ContentDialog();
            var stack = new StackPanel();
            var checkShipping = new CheckBox { Content = "Shipping info", IsChecked = true };
            var checkPayment = new CheckBox { Content = "Payment info", IsChecked = true };

            var toggle = new RoutedEventHandler((s, args) =>
            {
                dialog.IsPrimaryButtonEnabled = checkShipping.IsChecked == true || checkPayment.IsChecked == true;
            });

            checkShipping.Checked += toggle;
            checkShipping.Unchecked += toggle;
            checkPayment.Checked += toggle;
            checkPayment.Unchecked += toggle;

            stack.Margin = new Thickness(0, 16, 0, 0);
            stack.Children.Add(checkShipping);
            stack.Children.Add(checkPayment);

            dialog.Title = "Payments";
            dialog.Content = stack;
            dialog.PrimaryButtonText = "Clear";
            dialog.SecondaryButtonText = "Cancel";

            var confirm = await dialog.ShowAsync();
            if (confirm == ContentDialogResult.Primary)
            {
                var info = checkShipping.IsChecked == true;
                var credential = checkPayment.IsChecked == true;
                var response = await ProtoService.ClearSavedInfoAsync(info, credential);
                if (response.IsSucceeded)
                {

                }
                else
                {

                }
            }
        }
    }
}
