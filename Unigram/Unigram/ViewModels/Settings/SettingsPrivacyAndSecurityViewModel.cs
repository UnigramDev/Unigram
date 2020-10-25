using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Services;
using Unigram.ViewModels.Settings.Privacy;
using Unigram.Views.Popups;
using Unigram.Views.Settings;
using Unigram.Views.Settings.Password;
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
            Children.Add(_showPhoneRules);
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

            ProtoService.Send(new GetBlockedMessageSenders(0, 1), result =>
            {
                if (result is MessageSenders senders)
                {
                    BeginOnUIThread(() => BlockedUsers = senders.TotalCount);
                }
            });

            ProtoService.Send(new GetActiveSessions(), result =>
            {
                if (result is Sessions sessions)
                {
                    BeginOnUIThread(() => ActiveSessions = sessions.SessionsValue.Count);
                }
            });

            ProtoService.Send(new GetPasswordState(), result =>
            {
                if (result is PasswordState passwordState)
                {
                    BeginOnUIThread(() => IsPasswordEnabled = passwordState.HasPassword);
                }
            });

            IsPasscodeEnabled = _passcodeService.IsEnabled;

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
            get => _accountTTL;
            set => Set(ref _accountTTL, value);
        }

        private int _blockedUsers;
        public int BlockedUsers
        {
            get => _blockedUsers;
            set => Set(ref _blockedUsers, value);
        }

        private int _activeSessions;
        public int ActiveSessions
        {
            get => _activeSessions;
            set => Set(ref _activeSessions, value);
        }

        private bool _isPasswordEnabled;
        public bool IsPasswordEnabled
        {
            get => _isPasswordEnabled;
            set => Set(ref _isPasswordEnabled, value);
        }

        private bool _isPasscodeEnabled;
        public bool IsPasscodeEnabled
        {
            get => _isPasscodeEnabled;
            set => Set(ref _isPasscodeEnabled, value);
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

        public bool IsArchiveAndMuteEnabled
        {
            get
            {
                return ProtoService.Options.ArchiveAndMuteNewChatsFromUnknownUsers;
            }
            set
            {
                ProtoService.Options.ArchiveAndMuteNewChatsFromUnknownUsers = value;
                RaisePropertyChanged();
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
                var confirm = await MessagePopup.ShowAsync(Strings.Resources.SuggestContactsAlert, Strings.Resources.AppName, Strings.Resources.MuteDisable, Strings.Resources.Cancel);
                if (confirm != ContentDialogResult.Primary)
                {
                    RaisePropertyChanged(() => IsContactsSuggestEnabled);
                    return;
                }
            }

            ProtoService.Options.DisableTopChats = !value;
        }

        public RelayCommand PasscodeCommand { get; }
        private void PasscodeExecute()
        {
            NavigationService.NavigateToPasscode();
        }

        public RelayCommand PasswordCommand { get; }
        private async void PasswordExecute()
        {
            var response = await ProtoService.SendAsync(new GetPasswordState());
            if (response is PasswordState passwordState)
            {
                if (passwordState.HasPassword)
                {
                    NavigationService.Navigate(typeof(SettingsPasswordPage));
                }
                else if (passwordState.RecoveryEmailAddressCodeInfo != null)
                {
                    var state = new Dictionary<string, object>
                    {
                        { "pattern", passwordState.RecoveryEmailAddressCodeInfo.EmailAddressPattern },
                        { "length", passwordState.RecoveryEmailAddressCodeInfo.Length }
                    };

                    NavigationService.Navigate(typeof(SettingsPasswordConfirmPage), state: state);
                }
                else
                {
                    NavigationService.Navigate(typeof(SettingsPasswordIntroPage));
                }
            }
        }

        public RelayCommand ClearDraftsCommand { get; }
        private async void ClearDraftsExecute()
        {
            var confirm = await MessagePopup.ShowAsync(Strings.Resources.AreYouSureClearDrafts, Strings.Resources.AppName, Strings.Resources.OK, Strings.Resources.Cancel);
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
            var confirm = await MessagePopup.ShowAsync(Strings.Resources.SyncContactsDeleteInfo, Strings.Resources.Contacts, Strings.Resources.OK, Strings.Resources.Cancel);
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
            var dialog = new ContentPopup();
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
            SelectRadioItem GetSelectedPeriod(SelectRadioItem[] periods, SelectRadioItem defaultPeriod)
            {
                if (_accountTTL == 0)
                {
                    return defaultPeriod;
                }

                SelectRadioItem period = null;

                var max = 2147483647;
                foreach (var current in periods)
                {
                    var days = (int)current.Value;
                    int abs = Math.Abs(_accountTTL - days);
                    if (abs < max)
                    {
                        max = abs;
                        period = current;
                    }
                }

                return period ?? defaultPeriod;
            };

            var items = new[]
            {
                new SelectRadioItem(30, Locale.Declension("Months", 1), false),
                new SelectRadioItem(90, Locale.Declension("Months", 3), false),
                new SelectRadioItem(180, Locale.Declension("Months", 6), false),
                new SelectRadioItem(365, Locale.Declension("Years", 1), false)
            };

            var selected = GetSelectedPeriod(items, items[2]);
            if (selected != null)
            {
                selected.IsChecked = true;
            }

            var dialog = new SelectRadioPopup(items);
            dialog.Title = Strings.Resources.DeleteAccountTitle;
            dialog.PrimaryButtonText = Strings.Resources.OK;
            dialog.SecondaryButtonText = Strings.Resources.Cancel;

            var confirm = await dialog.ShowQueuedAsync();
            if (confirm == ContentDialogResult.Primary && dialog.SelectedIndex is int days)
            {
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
