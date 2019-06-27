using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Template10.Common;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Controls.Views;
using Unigram.Services;
using Unigram.ViewModels.Settings.Privacy;
using Unigram.Views.Settings;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels.Settings
{
    public class SettingsPrivacyAndSecurityViewModel : TLMultipleViewModelBase, IHandle<UpdateOption>
    {
        private readonly IContactsService _contactsService;
        private readonly IPasscodeService _passcodeService;

        private readonly SettingsPrivacyShowForwardedViewModel _showForwardedRules;
        private readonly SettingsPrivacyShowPhoneViewModel _showPhoneRules;
        private readonly SettingsPrivacyShowPhotoViewModel _showPhotoRules;
        private readonly SettingsPrivacyShowStatusViewModel _showStatusRules;
        private readonly SettingsPrivacyAllowCallsViewModel _allowCallsRules;
        private readonly SettingsPrivacyAllowChatInvitesViewModel _allowChatInvitesRules;

        public SettingsPrivacyAndSecurityViewModel(IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator, IContactsService contactsService, IPasscodeService passcodeService, SettingsPrivacyShowForwardedViewModel showForwarded, SettingsPrivacyShowPhoneViewModel showPhone, SettingsPrivacyShowPhotoViewModel showPhoto, SettingsPrivacyShowStatusViewModel statusTimestamp, SettingsPrivacyAllowCallsViewModel phoneCall, SettingsPrivacyAllowChatInvitesViewModel chatInvite)
            : base(protoService, cacheService, settingsService, aggregator)
        {
            _contactsService = contactsService;
            _passcodeService = passcodeService;

            _showForwardedRules = showForwarded;
            _showPhoneRules = showPhone;
            _showPhotoRules = showPhoto;
            _showStatusRules = statusTimestamp;
            _allowCallsRules = phoneCall;
            _allowChatInvitesRules = chatInvite;

            PasscodeCommand = new RelayCommand(PasscodeExecute);
            PasswordCommand = new RelayCommand(PasswordExecute);
            ClearDraftsCommand = new RelayCommand(ClearDraftsExecute);
            ClearContactsCommand = new RelayCommand(ClearContactsExecute);
            ClearPaymentsCommand = new RelayCommand(ClearPaymentsExecute);
            AccountTTLCommand = new RelayCommand(AccountTTLExecute);

            Children.Add(_showForwardedRules);
            Children.Add(_showPhotoRules);
            //Children.Add(_showPhoneRules);
            Children.Add(_showStatusRules);
            Children.Add(_allowCallsRules);
            Children.Add(_allowChatInvitesRules);

            aggregator.Subscribe(this);
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
                if (result is Telegram.Td.Api.Users users)
                {
                    BeginOnUIThread(() => BlockedUsers = users.TotalCount);
                }
            });

            return base.OnNavigatedToAsync(parameter, mode, state);
        }

        #region Properties

        public SettingsPrivacyShowForwardedViewModel ShowForwardedRules => _showForwardedRules;
        public SettingsPrivacyShowPhoneViewModel ShowPhoneRules => _showPhoneRules;
        public SettingsPrivacyShowPhotoViewModel ShowPhotoRules => _showPhotoRules;
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

        public bool IsContactsSyncEnabled
        {
            get
            {
                return Settings.IsContactsSyncEnabled;
            }
            set
            {
                Settings.IsContactsSyncEnabled = value;
                RaisePropertyChanged();
            }
        }

        public bool IsContactsSuggestEnabled
        {
            get
            {
                return !CacheService.Options.DisableTopChats;
            }
            set
            {
                SetSuggestContacts(value);
            }
        }

        public bool IsSecretPreviewsEnabled
        {
            get
            {
                return Settings.IsSecretPreviewsEnabled;
            }
            set
            {
                Settings.IsSecretPreviewsEnabled = value;
                RaisePropertyChanged();
            }
        }

        #endregion

        public void Handle(UpdateOption update)
        {
            if (update.Name.Equals("disable_top_chats"))
            {
                BeginOnUIThread(() => RaisePropertyChanged(() => IsContactsSuggestEnabled));
            }
        }

        private async void SetSuggestContacts(bool value)
        {
            if (!value)
            {
                var confirm = await TLMessageDialog.ShowAsync(Strings.Resources.SuggestContactsAlert, Strings.Resources.AppName, Strings.Resources.MuteDisable, Strings.Resources.Cancel);
                if (confirm != ContentDialogResult.Primary)
                {
                    RaisePropertyChanged(() => IsContactsSuggestEnabled);
                    return;
                }
            }

            ProtoService.Options.DisableTopChats = !value;
        }

        public RelayCommand PasscodeCommand { get; }
        private async void PasscodeExecute()
        {
            if (_passcodeService.IsEnabled)
            {
                var dialog = new SettingsPasscodeConfirmView(_passcodeService);
                dialog.IsSimple = _passcodeService.IsSimple;

                var confirm = await dialog.ShowAsync();
                if (confirm == ContentDialogResult.Primary)
                {
                    NavigationService.Navigate(typeof(SettingsPasscodePage));
                }
            }
            else
            {
                NavigationService.Navigate(typeof(SettingsPasscodePage));
            }
        }

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

        public RelayCommand ClearDraftsCommand { get; }
        private async void ClearDraftsExecute()
        {
            var confirm = await TLMessageDialog.ShowAsync(Strings.Resources.AreYouSureClearDrafts, Strings.Resources.AppName, Strings.Resources.OK, Strings.Resources.Cancel);
            if (confirm != ContentDialogResult.Primary)
            {
                return;
            }

            var clear = await ProtoService.SendAsync(new ClearAllDraftMessages(true));
            if (clear is Error)
            {
                // TODO
            }
        }

        public RelayCommand ClearContactsCommand { get; }
        private async void ClearContactsExecute()
        {
            var confirm = await TLMessageDialog.ShowAsync(Strings.Resources.SyncContactsDeleteInfo, Strings.Resources.Contacts, Strings.Resources.OK, Strings.Resources.Cancel);
            if (confirm != ContentDialogResult.Primary)
            {
                return;
            }

            IsContactsSyncEnabled = false;

            var clear = await ProtoService.SendAsync(new ClearImportedContacts());
            if (clear is Error)
            {
                // TODO
            }

            var contacts = await ProtoService.SendAsync(new GetContacts());
            if (contacts is Telegram.Td.Api.Users users)
            {
                var delete = await ProtoService.SendAsync(new RemoveContacts(users.UserIds));
                if (delete is Error)
                {
                    // TODO
                }
            }
        }

        public RelayCommand ClearPaymentsCommand { get; }
        private async void ClearPaymentsExecute()
        {
            var dialog = new TLContentDialog();
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
            var dialog = new TLContentDialog();
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

        public override async void RaisePropertyChanged([CallerMemberName] string propertyName = null)
        {
            base.RaisePropertyChanged(propertyName);

            if (propertyName.Equals(nameof(IsContactsSyncEnabled)))
            {
                if (IsContactsSyncEnabled)
                {
                    ProtoService.Send(new GetContacts(), async result =>
                    {
                        if (result is Telegram.Td.Api.Users users)
                        {
                            await _contactsService.SyncAsync(users);
                        }
                    });
                }
                else
                {
                    await _contactsService.RemoveAsync();
                }
            }
        }
    }
}
