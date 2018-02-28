using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using TdWindows;
using Telegram.Api.Services;
using Template10.Common;
using Unigram.Common;
using Unigram.Services;
using Unigram.ViewModels.Settings.Privacy;
using Unigram.Views.Settings;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels.Settings
{
    public class SettingsPrivacyAndSecurityViewModel : UnigramViewModelBase
    {
        private readonly IContactsService _contactsService;

        private readonly SettingsPrivacyShowStatusViewModel _showStatusRules;
        private readonly SettingsPrivacyAllowCallsViewModel _allowCallsRules;
        private readonly SettingsPrivacyAllowChatInvitesViewModel _allowChatInvitesRules;

        public SettingsPrivacyAndSecurityViewModel(IProtoService protoService, IMTProtoService legacyService, ICacheService cacheService, IEventAggregator aggregator, IContactsService contactsService, SettingsPrivacyShowStatusViewModel statusTimestamp, SettingsPrivacyAllowCallsViewModel phoneCall, SettingsPrivacyAllowChatInvitesViewModel chatInvite)
            : base(protoService, legacyService, cacheService, aggregator)
        {
            _contactsService = contactsService;

            _showStatusRules = statusTimestamp;
            _allowCallsRules = phoneCall;
            _allowChatInvitesRules = chatInvite;

            PasswordCommand = new RelayCommand(PasswordExecute);
            ClearPaymentsCommand = new RelayCommand(ClearPaymentsExecute);
            AccountTTLCommand = new RelayCommand(AccountTTLExecute);
            PeerToPeerCommand = new RelayCommand(PeerToPeerExecute);
        }

        public override Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            ProtoService.Send(new GetAccountTtl(), result =>
            {
                if (result is AccountTtl ttl)
                {
                    BeginOnUIThread(() => AccountTTL = ttl.Days);
                }
            });

            ProtoService.Send(new GetBlockedUsers(0, 1), result =>
            {
                if (result is TdWindows.Users users)
                {
                    BeginOnUIThread(() => BlockedUsers = users.TotalCount);
                }
            });

            return Task.CompletedTask;
        }

        #region Properties

        public SettingsPrivacyShowStatusViewModel ShowStatusRules => _showStatusRules;
        public SettingsPrivacyAllowCallsViewModel AllowCallsRules => _allowCallsRules;
        public SettingsPrivacyAllowChatInvitesViewModel AllowChatInvitesRules => _allowChatInvitesRules;

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

        private int _blockedUsers;
        public int BlockedUsers
        {
            get
            {
                return _blockedUsers;
            }
            set
            {
                Set(ref _blockedUsers, value);
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
            //var response = await LegacyService.GetPasswordAsync();
            //if (response.IsSucceeded)
            //{
            //    if (response.Result is TLAccountPassword)
            //    {
            //        NavigationService.Navigate(typeof(SettingsSecurityEnterPasswordPage), response.Result);
            //    }
            //    else
            //    {

            //    }
            //}
            //else
            //{
            //    // TODO
            //}
        }

        public RelayCommand ClearPaymentsCommand { get; }
        private async void ClearPaymentsExecute()
        {
            var dialog = new ContentDialog { Style = BootStrapper.Current.Resources["ModernContentDialogStyle"] as Style };
            var stack = new StackPanel();
            var checkShipping = new CheckBox { Content = Strings.Resources.PrivacyClearShipping, IsChecked = true };
            var checkPayment = new CheckBox { Content = Strings.Resources.PrivacyClearPayment, IsChecked = true };

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

            dialog.Title = Strings.Resources.PrivacyPayments;
            dialog.Content = stack;
            dialog.PrimaryButtonText = Strings.Resources.ClearButton;
            dialog.SecondaryButtonText = Strings.Resources.Cancel;

            var confirm = await dialog.ShowQueuedAsync();
            if (confirm == ContentDialogResult.Primary)
            {
                var info = checkShipping.IsChecked == true;
                var credential = checkPayment.IsChecked == true;

                if (info)
                {
                    ProtoService.Send(new DeleteSavedOrderInfo());
                }

                if (credential)
                {
                    ProtoService.Send(new DeleteSavedCredentials());
                }
            }
        }

        public RelayCommand AccountTTLCommand { get; }
        private async void AccountTTLExecute()
        {
            var dialog = new ContentDialog { Style = BootStrapper.Current.Resources["ModernContentDialogStyle"] as Style };
            var stack = new StackPanel();
            stack.Margin = new Thickness(12, 16, 12, 0);
            stack.Children.Add(new RadioButton { Tag = 30, Content = Locale.Declension("Months", 1) });
            stack.Children.Add(new RadioButton { Tag = 90, Content = Locale.Declension("Months", 3) });
            stack.Children.Add(new RadioButton { Tag = 180, Content = Locale.Declension("Months", 6) });
            stack.Children.Add(new RadioButton { Tag = 365, Content = Locale.Declension("Years", 1) });

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

            dialog.Title = Strings.Resources.DeleteAccountTitle;
            dialog.Content = stack;
            dialog.PrimaryButtonText = Strings.Resources.OK;
            dialog.SecondaryButtonText = Strings.Resources.Cancel;

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

                var response = await ProtoService.SendAsync(new SetAccountTtl(new AccountTtl(days)));
                if (response is Ok)
                {
                    AccountTTL = days;
                }
            }
        }

        public RelayCommand PeerToPeerCommand { get; }
        private async void PeerToPeerExecute()
        {
            var dialog = new ContentDialog { Style = BootStrapper.Current.Resources["ModernContentDialogStyle"] as Style };
            var stack = new StackPanel();
            stack.Margin = new Thickness(12, 16, 12, 0);
            stack.Children.Add(new RadioButton { Tag = 0, Content = Strings.Resources.LastSeenEverybody, IsChecked = PeerToPeerMode == 0 });
            stack.Children.Add(new RadioButton { Tag = 1, Content = Strings.Resources.LastSeenContacts, IsChecked = PeerToPeerMode == 1 });
            stack.Children.Add(new RadioButton { Tag = 2, Content = Strings.Resources.LastSeenNobody, IsChecked = PeerToPeerMode == 2 });

            dialog.Title = Strings.Resources.PrivacyCallsP2PTitle;
            dialog.Content = stack;
            dialog.PrimaryButtonText = Strings.Resources.OK;
            dialog.SecondaryButtonText = Strings.Resources.Cancel;

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
                    //var contacts = CacheService.GetContacts();
                    //var response = new TLContactsContacts { Users = new TLVector<TLUserBase>(contacts) };

                    //await _contactsService.ExportAsync(response);
                }
                else
                {
                    await _contactsService.RemoveAsync();
                }
            }
        }
    }
}
