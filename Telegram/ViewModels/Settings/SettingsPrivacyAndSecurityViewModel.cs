//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.ViewModels.Settings.Privacy;
using Telegram.Views;
using Telegram.Views.Popups;
using Telegram.Views.Settings;
using Telegram.Views.Settings.LoginEmail;
using Telegram.Views.Settings.Password;
using Telegram.Views.Settings.Popups;
using Telegram.Views.Settings.Privacy;

namespace Telegram.ViewModels.Settings
{
    public partial class SettingsPrivacyAndSecurityViewModel : MultiViewModelBase, IHandle
    {
        private readonly IContactsService _contactsService;
        private readonly IPasscodeService _passcodeService;

        private readonly SettingsPrivacyShowForwardedViewModel _showForwardedRules;
        private readonly SettingsPrivacyShowPhoneViewModel _showPhoneRules;
        private readonly SettingsPrivacyShowPhotoViewModel _showPhotoRules;
        private readonly SettingsPrivacyShowStatusViewModel _showStatusRules;
        private readonly SettingsPrivacyShowBioViewModel _showBioRules;
        private readonly SettingsPrivacyShowBirthdateViewModel _showBirthdateRules;
        private readonly SettingsPrivacyAllowCallsViewModel _allowCallsRules;
        private readonly SettingsPrivacyAllowChatInvitesViewModel _allowChatInvitesRules;
        private readonly SettingsPrivacyAllowPrivateVoiceAndVideoNoteMessagesViewModel _allowPrivateVoiceAndVideoNoteMessages;

        public SettingsPrivacyAndSecurityViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator, IContactsService contactsService, IPasscodeService passcodeService)
            : base(clientService, settingsService, aggregator)
        {
            _contactsService = contactsService;
            _passcodeService = passcodeService;

            _showForwardedRules = TypeResolver.Current.Resolve<SettingsPrivacyShowForwardedViewModel>(SessionId);
            _showPhoneRules = TypeResolver.Current.Resolve<SettingsPrivacyShowPhoneViewModel>(SessionId);
            _showPhotoRules = TypeResolver.Current.Resolve<SettingsPrivacyShowPhotoViewModel>(SessionId);
            _showStatusRules = TypeResolver.Current.Resolve<SettingsPrivacyShowStatusViewModel>(SessionId);
            _showBioRules = TypeResolver.Current.Resolve<SettingsPrivacyShowBioViewModel>(SessionId);
            _showBirthdateRules = TypeResolver.Current.Resolve<SettingsPrivacyShowBirthdateViewModel>(SessionId);
            _allowCallsRules = TypeResolver.Current.Resolve<SettingsPrivacyAllowCallsViewModel>(SessionId);
            _allowChatInvitesRules = TypeResolver.Current.Resolve<SettingsPrivacyAllowChatInvitesViewModel>(SessionId);
            _allowPrivateVoiceAndVideoNoteMessages = TypeResolver.Current.Resolve<SettingsPrivacyAllowPrivateVoiceAndVideoNoteMessagesViewModel>(SessionId);

            Children.Add(_showForwardedRules);
            Children.Add(_showPhotoRules);
            Children.Add(_showPhoneRules);
            Children.Add(_showStatusRules);
            Children.Add(_showBioRules);
            Children.Add(_showBirthdateRules);
            Children.Add(_allowCallsRules);
            Children.Add(_allowChatInvitesRules);
            Children.Add(_allowPrivateVoiceAndVideoNoteMessages);
        }

        protected override Task OnNavigatedToAsync(object parameter, NavigationMode mode, NavigationState state)
        {
            ClientService.Send(new GetAccountTtl(), result =>
            {
                if (result is AccountTtl ttl)
                {
                    BeginOnUIThread(() =>
                    {
                        if (ttl.Days == 0)
                        {
                            _accountTtl = _accountTtlIndexer[2];
                            RaisePropertyChanged(nameof(AccountTtl));
                            return;
                        }

                        int? period = null;

                        var max = 2147483647;
                        foreach (var days in _accountTtlIndexer)
                        {
                            int abs = Math.Abs(ttl.Days - days);
                            if (abs < max)
                            {
                                max = abs;
                                period = days;
                            }
                        }

                        _accountTtl = period ?? _accountTtlIndexer[2];
                        RaisePropertyChanged(nameof(AccountTtl));
                    });
                }
            });

            ClientService.Send(new GetBlockedMessageSenders(new BlockListMain(), 0, 1), result =>
            {
                if (result is MessageSenders senders)
                {
                    BeginOnUIThread(() => BlockedUsers = senders.TotalCount);
                }
            });

            ClientService.Send(new GetPasswordState(), result =>
            {
                if (result is PasswordState passwordState)
                {
                    BeginOnUIThread(() =>
                    {
                        HasEmailAddress = passwordState.LoginEmailAddressPattern.Length > 0;
                        HasPassword = passwordState.HasPassword;
                    });
                }
            });

            ClientService.Send(new GetDefaultMessageAutoDeleteTime(), result =>
            {
                if (result is MessageAutoDeleteTime messageTtl)
                {
                    BeginOnUIThread(() => DefaultTtl = messageTtl.Time);
                }
            });

            ClientService.Send(new GetNewChatPrivacySettings(), result =>
            {
                if (result is NewChatPrivacySettings settings)
                {
                    BeginOnUIThread(() => AllowNewChatsFromUnknownUsers = settings.AllowNewChatsFromUnknownUsers);
                }
            });

            if (ApiInfo.IsPackagedRelease && ClientService.Options.CanIgnoreSensitiveContentRestrictions)
            {
                ClientService.Send(new GetOption("ignore_sensitive_content_restrictions"), result =>
                {
                    BeginOnUIThread(() => RaisePropertyChanged(nameof(IgnoreSensitiveContentRestrictions)));
                });
            }

            HasPasscode = _passcodeService.IsEnabled;
            return Task.CompletedTask;
        }

        public override void Subscribe()
        {
            Aggregator.Subscribe<UpdateOption>(this, Handle);
        }

        #region Properties

        public SettingsPrivacyShowForwardedViewModel ShowForwardedRules => _showForwardedRules;
        public SettingsPrivacyShowPhoneViewModel ShowPhoneRules => _showPhoneRules;
        public SettingsPrivacyShowPhotoViewModel ShowPhotoRules => _showPhotoRules;
        public SettingsPrivacyShowStatusViewModel ShowStatusRules => _showStatusRules;
        public SettingsPrivacyShowBioViewModel ShowBioRules => _showBioRules;
        public SettingsPrivacyShowBirthdateViewModel ShowBirthdateRules => _showBirthdateRules;
        public SettingsPrivacyAllowCallsViewModel AllowCallsRules => _allowCallsRules;
        public SettingsPrivacyAllowChatInvitesViewModel AllowChatInvitesRules => _allowChatInvitesRules;
        public SettingsPrivacyAllowPrivateVoiceAndVideoNoteMessagesViewModel AllowPrivateVoiceAndVideoNoteMessages => _allowPrivateVoiceAndVideoNoteMessages;

        private bool? _allowNewChatsFromUnknownUsers;
        public bool? AllowNewChatsFromUnknownUsers
        {
            get => _allowNewChatsFromUnknownUsers;
            set => Set(ref _allowNewChatsFromUnknownUsers, value);
        }

        private int _accountTtl;
        public int AccountTtl
        {
            get => Array.IndexOf(_accountTtlIndexer, _accountTtl);
            set
            {
                if (value >= 0 && value < _accountTtlIndexer.Length && _accountTtl != _accountTtlIndexer[value])
                {
                    ClientService.SendAsync(new SetAccountTtl(new AccountTtl(_accountTtl = _accountTtlIndexer[value])));
                    RaisePropertyChanged();
                }
            }
        }

        private readonly int[] _accountTtlIndexer = new[]
        {
            30,
            90,
            180,
            365
        };

        public List<SettingsOptionItem<int>> AccountTtlOptions { get; } = new()
        {
            new SettingsOptionItem<int>(30, Locale.Declension(Strings.R.Months, 1)),
            new SettingsOptionItem<int>(90, Locale.Declension(Strings.R.Months, 3)),
            new SettingsOptionItem<int>(180, Locale.Declension(Strings.R.Months, 6)),
            new SettingsOptionItem<int>(365, Locale.Declension(Strings.R.Years, 1))
        };

        private int _blockedUsers;
        public int BlockedUsers
        {
            get => _blockedUsers;
            set => Set(ref _blockedUsers, value);
        }

        private bool _hasPassword;
        public bool HasPassword
        {
            get => _hasPassword;
            set => Set(ref _hasPassword, value);
        }

        private bool _hasEmailAddress;
        public bool HasEmailAddress
        {
            get => _hasEmailAddress;
            set => Set(ref _hasEmailAddress, value);
        }

        private bool _hasPasscode;
        public bool HasPasscode
        {
            get => _hasPasscode;
            set => Set(ref _hasPasscode, value);
        }

        private int _defaultTtl;
        public int DefaultTtl
        {
            get => _defaultTtl;
            set => Set(ref _defaultTtl, value);
        }

        public bool IsContactsSyncEnabled
        {
            get => Settings.IsContactsSyncEnabled;
            set
            {
                Settings.IsContactsSyncEnabled = value;
                RaisePropertyChanged();
            }
        }

        public bool IsContactsSuggestEnabled
        {
            get => !ClientService.Options.DisableTopChats;
            set => SetSuggestContacts(value);
        }

        public bool IsArchiveAndMuteEnabled
        {
            get => true; //ClientService.Options.ArchiveAndMuteNewChatsFromUnknownUsers;
            set
            {
                //ClientService.Options.ArchiveAndMuteNewChatsFromUnknownUsers = value;
                RaisePropertyChanged();
            }
        }

        public bool IsSecretPreviewsEnabled
        {
            get => Settings.IsSecretPreviewsEnabled;
            set
            {
                Settings.IsSecretPreviewsEnabled = value;
                RaisePropertyChanged();
            }
        }

        public bool IgnoreSensitiveContentRestrictions
        {
            get => ClientService.Options.IgnoreSensitiveContentRestrictions;
            set
            {
                if (ClientService.Options.CanIgnoreSensitiveContentRestrictions)
                {
                    ClientService.Options.IgnoreSensitiveContentRestrictions = value;
                    RaisePropertyChanged();
                }
            }
        }

        #endregion

        public void Handle(UpdateOption update)
        {
            if (update.Name.Equals("disable_top_chats"))
            {
                BeginOnUIThread(() => RaisePropertyChanged(nameof(IsContactsSuggestEnabled)));
            }
            else if (update.Name.Equals("ignore_sensitive_content_restrictions"))
            {
                BeginOnUIThread(() => RaisePropertyChanged(nameof(IgnoreSensitiveContentRestrictions)));
            }
        }

        private async void SetSuggestContacts(bool value)
        {
            if (!value)
            {
                var confirm = await ShowPopupAsync(Strings.SuggestContactsAlert, Strings.AppName, Strings.MuteDisable, Strings.Cancel);
                if (confirm != ContentDialogResult.Primary)
                {
                    RaisePropertyChanged(nameof(IsContactsSuggestEnabled));
                    return;
                }
            }

            ClientService.Options.DisableTopChats = !value;
        }

        public void Passcode()
        {
            NavigationService.NavigateToPasscode();
        }

        public async void Password()
        {
            var response = await ClientService.SendAsync(new GetPasswordState());
            if (response is PasswordState passwordState)
            {
                if (passwordState.HasPassword)
                {
                    var popup = new SettingsPasswordConfirmPopup(ClientService, passwordState);

                    var confirm = await ShowPopupAsync(popup);
                    if (confirm == ContentDialogResult.Primary && !string.IsNullOrEmpty(popup.Password))
                    {
                        NavigationService.Navigate(typeof(SettingsPasswordPage), popup.Password);
                    }
                    else if (popup.RecoveryEmailAddressCodeInfo != null)
                    {
                        var emailCode = new SettingsPasswordEmailCodePopup(ClientService, popup.RecoveryEmailAddressCodeInfo, SettingsPasswordEmailCodeType.Recovery);

                        if (ContentDialogResult.Primary == await ShowPopupAsync(emailCode))
                        {
                            await ShowPopupAsync(new SettingsPasswordDonePopup());
                        }
                    }
                }
                else if (passwordState.RecoveryEmailAddressCodeInfo != null)
                {
                    var emailCode = new SettingsPasswordEmailCodePopup(ClientService, passwordState.RecoveryEmailAddressCodeInfo, SettingsPasswordEmailCodeType.Continue);

                    if (ContentDialogResult.Primary == await ShowPopupAsync(emailCode))
                    {
                        await ShowPopupAsync(new SettingsPasswordDonePopup());
                    }
                }
                else
                {
                    passwordState = await NavigationService.NavigateToPasswordAsync();
                }

                HasPassword = passwordState?.HasPassword ?? false;
                HasEmailAddress = passwordState?.LoginEmailAddressPattern.Length > 0;
            }
        }

        public void OpenAutoDelete()
        {
            NavigationService.Navigate(typeof(SettingsAutoDeletePage));
        }

        public async void ChangeEmail()
        {
            var response = await ClientService.SendAsync(new GetPasswordState());
            if (response is PasswordState passwordState && passwordState.LoginEmailAddressPattern.Length > 0)
            {
                var confirm = await ShowPopupAsync(Strings.EmailLoginChangeMessage, passwordState.LoginEmailAddressPattern, Strings.ChangeEmail, Strings.Cancel);
                if (confirm == ContentDialogResult.Primary)
                {
                    var address = new SettingsLoginEmailAddressPopup(ClientService);

                    var coconfirm = await ShowPopupAsync(address);
                    if (coconfirm == ContentDialogResult.Primary)
                    {
                        await ShowPopupAsync(new SettingsLoginEmailCodePopup(ClientService, address.CodeInfo));
                    }
                }
            }
            else
            {
                HasEmailAddress = false;
            }
        }

        public void ArchiveSettings()
        {
            ShowPopupAsync(new SettingsArchivePopup(ClientService));
        }

        public async void ClearContacts()
        {
            var confirm = await ShowPopupAsync(Strings.SyncContactsDeleteInfo, Strings.Contacts, Strings.OK, Strings.Cancel);
            if (confirm != ContentDialogResult.Primary)
            {
                return;
            }

            IsContactsSyncEnabled = false;

            var clear = await ClientService.SendAsync(new ClearImportedContacts());
            if (clear is Error)
            {
                // TODO
            }

            var contacts = await ClientService.SendAsync(new GetContacts());
            if (contacts is Telegram.Td.Api.Users users)
            {
                var delete = await ClientService.SendAsync(new RemoveContacts(users.UserIds));
                if (delete is Error)
                {
                    // TODO
                }
            }
        }

        public async void ClearPayments()
        {
            var dialog = new ContentPopup();
            var stack = new StackPanel();
            var checkShipping = new CheckBox { Content = Strings.PrivacyClearShipping, IsChecked = true };
            var checkPayment = new CheckBox { Content = Strings.PrivacyClearPayment, IsChecked = true };

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

            dialog.Title = Strings.PrivacyPayments;
            dialog.Content = stack;
            dialog.PrimaryButtonText = Strings.ClearButton;
            dialog.SecondaryButtonText = Strings.Cancel;

            var confirm = await ShowPopupAsync(dialog);
            if (confirm == ContentDialogResult.Primary)
            {
                var info = checkShipping.IsChecked == true;
                var credential = checkPayment.IsChecked == true;

                if (info)
                {
                    ClientService.Send(new DeleteSavedOrderInfo());
                }

                if (credential)
                {
                    ClientService.Send(new DeleteSavedCredentials());
                }
            }
        }

        public void OpenWebSessions()
        {
            NavigationService.Navigate(typeof(SettingsWebSessionsPage));
        }

        public void OpenBlockedUsers()
        {
            NavigationService.Navigate(typeof(SettingsBlockedChatsPage));
        }

        public void OpenShowPhone()
        {
            NavigationService.Navigate(typeof(SettingsPrivacyPhonePage));
        }

        public void OpenStatusTimestamp()
        {
            NavigationService.Navigate(typeof(SettingsPrivacyShowStatusPage));
        }

        public void OpenProfilePhoto()
        {
            NavigationService.Navigate(typeof(SettingsPrivacyShowPhotoPage));
        }

        public void OpenBio()
        {
            NavigationService.Navigate(typeof(SettingsPrivacyShowBioPage));
        }

        public void OpenBirthdate()
        {
            NavigationService.Navigate(typeof(SettingsPrivacyShowBirthdatePage));
        }

        public void OpenForwards()
        {
            NavigationService.Navigate(typeof(SettingsPrivacyShowForwardedPage));
        }

        public void OpenPhoneCall()
        {
            NavigationService.Navigate(typeof(SettingsPrivacyAllowCallsPage));
        }

        public void OpenChatInvite()
        {
            NavigationService.Navigate(typeof(SettingsPrivacyAllowChatInvitesPage));
        }

        public void OpenVoiceMessages()
        {
            NavigationService.Navigate(typeof(SettingsPrivacyAllowPrivateVoiceAndVideoNoteMessagesPage));
        }

        public void OpenMessages()
        {
            NavigationService.Navigate(typeof(SettingsPrivacyNewChatPage));
        }

        public override async void RaisePropertyChanged([CallerMemberName] string propertyName = null)
        {
            base.RaisePropertyChanged(propertyName);

            if (propertyName.Equals(nameof(IsContactsSyncEnabled)))
            {
                if (IsContactsSyncEnabled)
                {
                    ClientService.Send(new GetContacts(), async result =>
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
