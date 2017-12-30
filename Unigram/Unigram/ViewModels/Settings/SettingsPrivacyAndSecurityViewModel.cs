using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Aggregator;
using Telegram.Api.Helpers;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Telegram.Api.TL;
using Telegram.Api.TL.Account;
using Telegram.Api.TL.Contacts;
using Template10.Common;
using Unigram.Common;
using Unigram.Core.Services;
using Unigram.Strings;
using Unigram.Views.Settings;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels.Settings
{
    public class SettingsPrivacyAndSecurityViewModel : UnigramViewModelBase
    {
        private readonly IContactsService _contactsService;

        private readonly SettingsPrivacyStatusTimestampViewModel _statusTimestampRules;
        private readonly SettingsPrivacyPhoneCallViewModel _phoneCallRules;
        private readonly SettingsPrivacyChatInviteViewModel _chatInviteRules;

        public SettingsPrivacyAndSecurityViewModel(IMTProtoService protoService, ICacheService cacheService, ITelegramEventAggregator aggregator, IContactsService contactsService, SettingsPrivacyStatusTimestampViewModel statusTimestamp, SettingsPrivacyPhoneCallViewModel phoneCall, SettingsPrivacyChatInviteViewModel chatInvite)
            : base(protoService, cacheService, aggregator)
        {
            _contactsService = contactsService;

            _statusTimestampRules = statusTimestamp;
            _phoneCallRules = phoneCall;
            _chatInviteRules = chatInvite;

            PasswordCommand = new RelayCommand(PasswordExecute);
            ClearPaymentsCommand = new RelayCommand(ClearPaymentsExecute);
            AccountTTLCommand = new RelayCommand(AccountTTLExecute);
            PeerToPeerCommand = new RelayCommand(PeerToPeerExecute);
        }

        public override Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            ProtoService.GetAccountTTLAsync(result =>
            {
                BeginOnUIThread(() => AccountTTL = result.Days);
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

        public int PeerToPeerMode
        {
            get
            {
                return ApplicationSettings.Current.PeerToPeerMode;
            }
            set
            {
                ApplicationSettings.Current.PeerToPeerMode = value;
                RaisePropertyChanged();
            }
        }

        public bool IsContactsSyncEnabled
        {
            get
            {
                return ApplicationSettings.Current.IsContactsSyncEnabled;
            }
            set
            {
                ApplicationSettings.Current.IsContactsSyncEnabled = value;
                RaisePropertyChanged();
            }
        }

        #endregion

        public RelayCommand PasswordCommand { get; }
        private async void PasswordExecute()
        {
            var response = await ProtoService.GetPasswordAsync();
            if (response.IsSucceeded)
            {
                if (response.Result is TLAccountPassword)
                {
                    NavigationService.Navigate(typeof(SettingsSecurityEnterPasswordPage), response.Result);
                }
                else
                {

                }
            }
            else
            {
                // TODO
            }
        }

        public RelayCommand ClearPaymentsCommand { get; }
        private async void ClearPaymentsExecute()
        {
            var dialog = new ContentDialog { Style = BootStrapper.Current.Resources["ModernContentDialogStyle"] as Style };
            var stack = new StackPanel();
            var checkShipping = new CheckBox { Content = Strings.Android.PrivacyClearShipping, IsChecked = true };
            var checkPayment = new CheckBox { Content = Strings.Android.PrivacyClearPayment, IsChecked = true };

            var toggle = new RoutedEventHandler((s, args) =>
            {
                dialog.IsPrimaryButtonEnabled = checkShipping.IsChecked == true || checkPayment.IsChecked == true;
            });

            checkShipping.Checked += toggle;
            checkShipping.Unchecked += toggle;
            checkPayment.Checked += toggle;
            checkPayment.Unchecked += toggle;

            stack.Margin = new Thickness(12, 16, 12, 0);
            stack.Children.Add(checkShipping);
            stack.Children.Add(checkPayment);

            dialog.Title = Strings.Android.PrivacyPayments;
            dialog.Content = stack;
            dialog.PrimaryButtonText = Strings.Android.ClearButton;
            dialog.SecondaryButtonText = Strings.Android.Cancel;

            var confirm = await dialog.ShowQueuedAsync();
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

        public RelayCommand AccountTTLCommand { get; }
        private async void AccountTTLExecute()
        {
            var dialog = new ContentDialog { Style = BootStrapper.Current.Resources["ModernContentDialogStyle"] as Style };
            var stack = new StackPanel();
            stack.Margin = new Thickness(12, 16, 12, 0);
            stack.Children.Add(new RadioButton { Tag = 30, Content = LocaleHelper.Declension("Months", 1) });
            stack.Children.Add(new RadioButton { Tag = 90, Content = LocaleHelper.Declension("Months", 3) });
            stack.Children.Add(new RadioButton { Tag = 180, Content = LocaleHelper.Declension("Months", 6) });
            stack.Children.Add(new RadioButton { Tag = 365, Content = LocaleHelper.Declension("Years", 1) });

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

            dialog.Title = Strings.Android.DeleteAccountTitle;
            dialog.Content = stack;
            dialog.PrimaryButtonText = Strings.Android.OK;
            dialog.SecondaryButtonText = Strings.Android.Cancel;

            var confirm = await dialog.ShowQueuedAsync();
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

        public RelayCommand PeerToPeerCommand { get; }
        private async void PeerToPeerExecute()
        {
            var dialog = new ContentDialog { Style = BootStrapper.Current.Resources["ModernContentDialogStyle"] as Style };
            var stack = new StackPanel();
            stack.Margin = new Thickness(12, 16, 12, 0);
            stack.Children.Add(new RadioButton { Tag = 0, Content = Strings.Android.LastSeenEverybody, IsChecked = PeerToPeerMode == 0 });
            stack.Children.Add(new RadioButton { Tag = 1, Content = Strings.Android.LastSeenContacts, IsChecked = PeerToPeerMode == 1 });
            stack.Children.Add(new RadioButton { Tag = 2, Content = Strings.Android.LastSeenNobody, IsChecked = PeerToPeerMode == 2 });

            dialog.Title = Strings.Android.PrivacyCallsP2PTitle;
            dialog.Content = stack;
            dialog.PrimaryButtonText = Strings.Android.OK;
            dialog.SecondaryButtonText = Strings.Android.Cancel;

            var confirm = await dialog.ShowQueuedAsync();
            if (confirm == ContentDialogResult.Primary)
            {
                var mode = 1;
                foreach (RadioButton current in stack.Children)
                {
                    if (current.IsChecked == true)
                    {
                        mode = (int)current.Tag;
                        break;
                    }
                }

                PeerToPeerMode = mode;
            }
        }

        public override async void RaisePropertyChanged([CallerMemberName] string propertyName = null)
        {
            base.RaisePropertyChanged(propertyName);

            if (propertyName.Equals(nameof(IsContactsSyncEnabled)))
            {
                if (IsContactsSyncEnabled)
                {
                    var contacts = CacheService.GetContacts();
                    var response = new TLContactsContacts { Users = new TLVector<TLUserBase>(contacts) };

                    await _contactsService.ExportAsync(response);
                }
                else
                {
                    await _contactsService.RemoveAsync();
                }
            }
        }
    }
}
