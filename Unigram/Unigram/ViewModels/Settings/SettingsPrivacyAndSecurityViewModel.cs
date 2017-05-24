using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Aggregator;
using Telegram.Api.Helpers;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Telegram.Api.TL;
using Unigram.Common;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels.Settings
{
    public class SettingsPrivacyAndSecurityViewModel : UnigramViewModelBase
    {
        private readonly SettingsPrivacyStatusTimestampViewModel _statusTimestampRules;
        private readonly SettingsPrivacyPhoneCallViewModel _phoneCallRules;
        private readonly SettingsPrivacyChatInviteViewModel _chatInviteRules;

        public SettingsPrivacyAndSecurityViewModel(IMTProtoService protoService, ICacheService cacheService, ITelegramEventAggregator aggregator, SettingsPrivacyStatusTimestampViewModel statusTimestamp, SettingsPrivacyPhoneCallViewModel phoneCall, SettingsPrivacyChatInviteViewModel chatInvite)
            : base(protoService, cacheService, aggregator)
        {
            _statusTimestampRules = statusTimestamp;
            _phoneCallRules = phoneCall;
            _chatInviteRules = chatInvite;
        }

        public override Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            ProtoService.GetAccountTTLAsync(result =>
            {

            });

            return base.OnNavigatedToAsync(parameter, mode, state);
        }

        #region Properties

        public SettingsPrivacyStatusTimestampViewModel StatusTimestampRules => _statusTimestampRules;
        public SettingsPrivacyPhoneCallViewModel PhoneCallRules => _phoneCallRules;
        public SettingsPrivacyChatInviteViewModel ChatInviteRules => _chatInviteRules;

        #endregion

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
