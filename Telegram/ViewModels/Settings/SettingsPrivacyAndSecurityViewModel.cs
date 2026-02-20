//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.ViewModels.Settings.Privacy;
using Telegram.Views.Popups;
using Telegram.Views.Settings;
using Telegram.Views.Settings.LoginEmail;
using Telegram.Views.Settings.Password;
using Telegram.Views.Settings.Popups;
using Telegram.Views.Settings.Privacy;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Telegram.ViewModels.Settings
{
    public partial class SettingsPrivacyAndSecurityViewModel : MultiViewModelBase, IHandle
    {
        private readonly IPasscodeService _passcodeService;

        private readonly SettingsPrivacyShowForwardedViewModel _showForwardedRules;
        private readonly SettingsPrivacyShowPhoneViewModel _showPhoneRules;
        private readonly SettingsPrivacyShowPhotoViewModel _showPhotoRules;
        private readonly SettingsPrivacyShowStatusViewModel _showStatusRules;
        private readonly SettingsPrivacyShowBioViewModel _showBioRules;
        private readonly SettingsPrivacyShowProfileAudioViewModel _showProfileAudioRules;
        private readonly SettingsPrivacyShowBirthdateViewModel _showBirthdateRules;
        private readonly SettingsPrivacyAutosaveGiftsViewModel _autosaveGiftsRules;
        private readonly SettingsPrivacyAllowCallsViewModel _allowCallsRules;
        private readonly SettingsPrivacyAllowChatInvitesViewModel _allowChatInvitesRules;
        private readonly SettingsPrivacyAllowPrivateVoiceAndVideoNoteMessagesViewModel _allowPrivateVoiceAndVideoNoteMessages;
        private readonly SettingsPrivacyNewChatViewModel _newChatRules;

        public SettingsPrivacyAndSecurityViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator, IPasscodeService passcodeService)
            : base(clientService, settingsService, aggregator)
        {
            _passcodeService = passcodeService;

            _showForwardedRules = Session.Resolve<SettingsPrivacyShowForwardedViewModel>();
            _showPhoneRules = Session.Resolve<SettingsPrivacyShowPhoneViewModel>();
            _showPhotoRules = Session.Resolve<SettingsPrivacyShowPhotoViewModel>();
            _showStatusRules = Session.Resolve<SettingsPrivacyShowStatusViewModel>();
            _showBioRules = Session.Resolve<SettingsPrivacyShowBioViewModel>();
            _showProfileAudioRules = Session.Resolve<SettingsPrivacyShowProfileAudioViewModel>();
            _showBirthdateRules = Session.Resolve<SettingsPrivacyShowBirthdateViewModel>();
            _autosaveGiftsRules = Session.Resolve<SettingsPrivacyAutosaveGiftsViewModel>();
            _allowCallsRules = Session.Resolve<SettingsPrivacyAllowCallsViewModel>();
            _allowChatInvitesRules = Session.Resolve<SettingsPrivacyAllowChatInvitesViewModel>();
            _allowPrivateVoiceAndVideoNoteMessages = Session.Resolve<SettingsPrivacyAllowPrivateVoiceAndVideoNoteMessagesViewModel>();
            _newChatRules = Session.Resolve<SettingsPrivacyNewChatViewModel>();

            Children.Add(_showForwardedRules);
            Children.Add(_showPhotoRules);
            Children.Add(_showPhoneRules);
            Children.Add(_showStatusRules);
            Children.Add(_showBioRules);
            Children.Add(_showProfileAudioRules);
            Children.Add(_showBirthdateRules);
            Children.Add(_autosaveGiftsRules);
            Children.Add(_allowCallsRules);
            Children.Add(_allowChatInvitesRules);
            Children.Add(_allowPrivateVoiceAndVideoNoteMessages);
            Children.Add(_newChatRules);
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
                        HasPassword = passwordState.HasPassword;
                        HasEmailAddress = passwordState.LoginEmailAddressPattern.Length > 0;
                        EmailAddressPattern = UpdateEmailAddressPattern(passwordState.LoginEmailAddressPattern);
                    });
                }
            });

            ClientService.Send(new GetLoginPasskeys(), result =>
            {
                if (result is Passkeys passkeys)
                {
                    BeginOnUIThread(() => HasPasskeys = passkeys.PasskeysValue.Count > 0);
                }
            });

            ClientService.Send(new GetDefaultMessageAutoDeleteTime(), result =>
            {
                if (result is MessageAutoDeleteTime messageTtl)
                {
                    BeginOnUIThread(() => DefaultTtl = messageTtl.Time);
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

        public override void NavigatingFrom(NavigatingEventArgs args)
        {
            // Do nothing
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
        public SettingsPrivacyShowProfileAudioViewModel ShowProfileAudioRules => _showProfileAudioRules;
        public SettingsPrivacyShowBirthdateViewModel ShowBirthdateRules => _showBirthdateRules;
        public SettingsPrivacyAutosaveGiftsViewModel AutosaveGiftsRules => _autosaveGiftsRules;
        public SettingsPrivacyAllowCallsViewModel AllowCallsRules => _allowCallsRules;
        public SettingsPrivacyAllowChatInvitesViewModel AllowChatInvitesRules => _allowChatInvitesRules;
        public SettingsPrivacyAllowPrivateVoiceAndVideoNoteMessagesViewModel AllowPrivateVoiceAndVideoNoteMessages => _allowPrivateVoiceAndVideoNoteMessages;
        public SettingsPrivacyNewChatViewModel NewChatRules => _newChatRules;

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
            365,
            548,
            730
        };

        public List<SettingsOptionItem<int>> AccountTtlOptions { get; } = new()
        {
            new SettingsOptionItem<int>(30, Locale.Declension(Strings.R.Months, 1)),
            new SettingsOptionItem<int>(90, Locale.Declension(Strings.R.Months, 3)),
            new SettingsOptionItem<int>(180, Locale.Declension(Strings.R.Months, 6)),
            new SettingsOptionItem<int>(365, Locale.Declension(Strings.R.Months, 12)),
            new SettingsOptionItem<int>(548, Locale.Declension(Strings.R.Months, 18)),
            new SettingsOptionItem<int>(730, Locale.Declension(Strings.R.Months, 24))
        };

        private int _blockedUsers;
        public int BlockedUsers
        {
            get => _blockedUsers;
            set => Set(ref _blockedUsers, value);
        }

        private bool _hasPasskeys;
        public bool HasPasskeys
        {
            get => _hasPasskeys;
            set => Set(ref _hasPasskeys, value);
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

        private FormattedText _emailAddressPattern;
        public FormattedText EmailAddressPattern
        {
            get => _emailAddressPattern;
            set => Set(ref _emailAddressPattern, value);
        }

        private FormattedText UpdateEmailAddressPattern(string pattern)
        {
            pattern ??= string.Empty;

            var first = pattern.IndexOf('*');
            var last = pattern.LastIndexOf('*');

            if (first != -1 && last != -1)
            {
                var formatted = new FormattedText(pattern, new[]
                {
                    new TextEntity(first, last - first + 1, new TextEntityTypeSpoiler())
                });

                return formatted;
            }

            return pattern.AsFormattedText();
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
            if (update.Name == OptionsService.R.DisableTopChats)
            {
                BeginOnUIThread(() => RaisePropertyChanged(nameof(IsContactsSuggestEnabled)));
            }
            else if (update.Name == OptionsService.R.IgnoreSensitiveContentRestrictions)
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

        public async void Passkeys()
        {
            if (HasPasskeys)
            {
                NavigationService.Navigate(typeof(SettingsPasskeysPage));
            }
            else
            {
                if (!BridgeApplicationContext.IsPasskeySupported())
                {
                    ShowPopup(Strings.PasskeyNotSupportedText, Strings.AppName, Strings.OK);
                    return;
                }

                var confirm = await ShowPopupAsync(new SettingsPasskeysIntroPopup());
                if (confirm == ContentDialogResult.Primary)
                {
                    var response = await BridgeApplicationContext.AddLoginPasskeyAsync(ClientService);
                    if (response is Passkey passkey)
                    {
                        HasPasskeys = true;
                        NavigationService.Navigate(typeof(SettingsPasskeysPage));
                        ShowToast(string.Format("**{0}**\n{1}", Strings.PasskeyAddedTitle, string.Format(Strings.PasskeyAddedText, passkey.Name)));
                    }
                    else if (response is Error { Code: not -2147023673 and not -2146893770 } error)
                    {
                        ShowToast(error);
                    }
                }
            }
        }

        public async void Password()
        {
            // TODO: Maybe use NavigationService.NavigateToPasswordAsync
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
                            ShowPopup(new SettingsPasswordDonePopup());
                        }
                    }
                }
                else if (passwordState.RecoveryEmailAddressCodeInfo != null)
                {
                    var emailCode = new SettingsPasswordEmailCodePopup(ClientService, passwordState.RecoveryEmailAddressCodeInfo, SettingsPasswordEmailCodeType.Continue);

                    if (ContentDialogResult.Primary == await ShowPopupAsync(emailCode))
                    {
                        ShowPopup(new SettingsPasswordDonePopup());
                    }
                }
                else
                {
                    passwordState = await NavigationService.NavigateToPasswordSetupAsync();
                }

                HasPassword = passwordState?.HasPassword ?? false;
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
                var block = new FormattedTextBlock
                {
                    IsTextSelectionEnabled = false,
                    TextWrapping = TextWrapping.NoWrap,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    MaxLines = 1,
                    FontSize = 20,
                    AutoFontSize = false
                };

                block.SetText(ClientService, EmailAddressPattern);

                var popup = new MessagePopup
                {
                    Title = block,
                    Message = Strings.EmailLoginChangeMessage,
                    PrimaryButtonText = Strings.ChangeEmail,
                    SecondaryButtonText = Strings.Cancel
                };

                var confirm = await ShowPopupAsync(popup);
                if (confirm == ContentDialogResult.Primary)
                {
                    var address = new SettingsLoginEmailAddressPopup(ClientService);

                    var coconfirm = await ShowPopupAsync(address);
                    if (coconfirm == ContentDialogResult.Primary)
                    {
                        ShowPopup(new SettingsLoginEmailCodePopup(ClientService, address.CodeInfo));
                    }
                }
            }
            else
            {
                HasEmailAddress = false;
                EmailAddressPattern = UpdateEmailAddressPattern(string.Empty);
            }
        }

        public void ArchiveSettings()
        {
            ShowPopup(new SettingsArchivePopup(ClientService));
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

            stack.Children.Add(checkShipping);
            stack.Children.Add(checkPayment);

            dialog.Title = Strings.PrivacyPayments;
            dialog.Content = stack;
            dialog.Padding = new Thickness(24, 24, 24, 18);
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

        public void OpenProfileAudio()
        {
            NavigationService.Navigate(typeof(SettingsPrivacyShowProfileAudioPage));
        }

        public void OpenGifts()
        {
            NavigationService.Navigate(typeof(SettingsPrivacyAutosaveGiftsPage));
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
    }
}
