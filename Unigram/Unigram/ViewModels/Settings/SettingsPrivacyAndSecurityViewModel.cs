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
using Unigram.Strings;
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
                Execute.BeginOnUIThread(() => AccountTTL = result.Days);
            });

            return base.OnNavigatedToAsync(parameter, mode, state);
        }

        #region Properties

        public SettingsPrivacyStatusTimestampViewModel StatusTimestampRules => _statusTimestampRules;
        public SettingsPrivacyPhoneCallViewModel PhoneCallRules => _phoneCallRules;
        public SettingsPrivacyChatInviteViewModel ChatInviteRules => _chatInviteRules;

        private int _accountTTL;
        public int AccountTTL
        {
            get
            {
                return _accountTTL;
            }
            set
            {
                Set(ref _accountTTL, value);
            }
        }

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

        public RelayCommand AccountTTLCommand => new RelayCommand(AccountTTLExecute);
        private async void AccountTTLExecute()
        {
            var dialog = new ContentDialog();
            var stack = new StackPanel();
            stack.Margin = new Thickness(0, 16, 0, 0);
            stack.Children.Add(new RadioButton { Tag = 30,  Content = Language.Declension(1, AppResources.MonthNominativeSingular, AppResources.MonthNominativePlural, AppResources.MonthGenitiveSingular, AppResources.MonthGenitivePlural, null, null) });
            stack.Children.Add(new RadioButton { Tag = 90,  Content = Language.Declension(3, AppResources.MonthNominativeSingular, AppResources.MonthNominativePlural, AppResources.MonthGenitiveSingular, AppResources.MonthGenitivePlural, null, null) });
            stack.Children.Add(new RadioButton { Tag = 180, Content = Language.Declension(6, AppResources.MonthNominativeSingular, AppResources.MonthNominativePlural, AppResources.MonthGenitiveSingular, AppResources.MonthGenitivePlural, null, null) });
            stack.Children.Add(new RadioButton { Tag = 365, Content = Language.Declension(1, AppResources.YearNominativeSingular,  AppResources.YearNominativePlural,  AppResources.YearGenitiveSingular,  AppResources.YearGenitivePlural,  null, null) });

            RadioButton GetSelectedPeriod(UIElementCollection periods, RadioButton defaultPeriod)
            {
                if (_accountTTL == 0)
                {
                    return stack.Children[2] as RadioButton;
                }

                RadioButton period = null;

                var max = 2147483647;
                foreach (RadioButton current in stack.Children)
                {
                    var days = (int)current.Tag;
                    int abs = Math.Abs(_accountTTL - days);
                    if (abs < max)
                    {
                        max = abs;
                        period = current;
                    }
                }

                return period ?? stack.Children[2] as RadioButton;
            };

            var selected = GetSelectedPeriod(stack.Children, stack.Children[2] as RadioButton);
            if (selected != null)
            {
                selected.IsChecked = true;
            }

            dialog.Title = "Account self-destructs";
            dialog.Content = stack;
            dialog.PrimaryButtonText = "OK";
            dialog.SecondaryButtonText = "Cancel";

            var confirm = await dialog.ShowAsync();
            if (confirm == ContentDialogResult.Primary)
            {
                var days = 180;
                foreach (RadioButton current in stack.Children)
                {
                    if (current.IsChecked == true)
                    {
                        days = (int)current.Tag;
                        break;
                    }
                }

                var response = await ProtoService.SetAccountTTLAsync(new TLAccountDaysTTL { Days = days });
                if (response.IsSucceeded)
                {
                    AccountTTL = days;
                }
                else
                {

                }
            }
        }
    }
}
