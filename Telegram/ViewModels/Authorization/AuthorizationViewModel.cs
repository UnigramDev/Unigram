//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Entities;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.ViewModels.Delegates;
using Telegram.Views.Settings;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Telegram.ViewModels.Authorization
{
    public class AuthorizationViewModel : TLViewModelBase, IDelegable<ISignInDelegate>, IHandle
    {
        private readonly ISessionService _sessionService;
        private readonly ILifetimeService _lifetimeService;
        private readonly INotificationsService _notificationsService;

        public ISignInDelegate Delegate { get; set; }

        public AuthorizationViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator, ISessionService sessionService, ILifetimeService lifecycleService, INotificationsService notificationsService)
            : base(clientService, settingsService, aggregator)
        {
            _sessionService = sessionService;
            _lifetimeService = lifecycleService;
            _notificationsService = notificationsService;

            SwitchCommand = new RelayCommand(SwitchExecute);
            SendCommand = new RelayCommand(SendExecute, () => !IsLoading);
            ProxyCommand = new RelayCommand(ProxyExecute);
        }

        protected override Task OnNavigatedToAsync(object parameter, NavigationMode mode, NavigationState state)
        {
            LocaleService.Current.Changed += OnLocaleChanged;

            ClientService.Send(new GetCountryCode(), result =>
            {
                if (result is Text text)
                {
                    BeginOnUIThread(() => GotUserCountry(text.TextValue));
                }
            });

            var authState = ClientService.GetAuthorizationState();
            var waitState = authState is AuthorizationStateWaitPhoneNumber
                or AuthorizationStateWaitCode
                or AuthorizationStateWaitPassword
                or AuthorizationStateWaitEmailAddress
                or AuthorizationStateWaitEmailCode;

            if (waitState && mode != NavigationMode.Refresh)
            {
                IsLoading = false;

                Delegate.UpdateQrCodeMode(QrCodeMode.Loading);

                ClientService.Send(new GetApplicationConfig(), result =>
                {
                    if (result is JsonValueObject json)
                    {
                        var camera = json.GetNamedBoolean("qr_login_camera", false);
                        var code = json.GetNamedString("qr_login_code", "disabled");

                        if (camera && Enum.TryParse(code, true, out QrCodeMode mode))
                        {
                            BeginOnUIThread(() => Delegate?.UpdateQrCodeMode(mode));

                            if (mode == QrCodeMode.Primary)
                            {
                                var userIds = new List<long>();

                                foreach (var session in _lifetimeService.Items)
                                {
                                    if (Settings.UseTestDC == session.Settings.UseTestDC && session.UserId != 0)
                                    {
                                        userIds.Add(session.UserId);
                                    }
                                }

                                ClientService.Send(new RequestQrCodeAuthentication(userIds));
                            }

                            return;
                        }
                    }

                    BeginOnUIThread(() => Delegate?.UpdateQrCodeMode(QrCodeMode.Disabled));
                });
            }
            else if (authState is AuthorizationStateWaitOtherDeviceConfirmation waitOtherDeviceConfirmation)
            {
                var firstTime = _token == null;

                Token = waitOtherDeviceConfirmation.Link;
                Delegate?.UpdateQrCode(waitOtherDeviceConfirmation.Link, firstTime);

                if (mode != NavigationMode.Refresh)
                {
                    Delegate?.UpdateQrCodeMode(QrCodeMode.Primary);
                }
            }

            return Task.CompletedTask;
        }

        protected override Task OnNavigatedFromAsync(NavigationState suspensionState, bool suspending)
        {
            LocaleService.Current.Changed -= OnLocaleChanged;
            return Task.CompletedTask;
        }

        private void OnLocaleChanged(object sender, LocaleChangedEventArgs e)
        {
            BeginOnUIThread(() => S.Handle(e));
        }

        private void GotUserCountry(string code)
        {
            Country country = null;
            foreach (var local in Country.All)
            {
                if (string.Equals(local.Code, code, StringComparison.OrdinalIgnoreCase))
                {
                    country = local;
                    break;
                }
            }

            if (country != null && SelectedCountry == null && string.IsNullOrEmpty(PhoneNumber))
            {
                BeginOnUIThread(() =>
                {
                    SelectedCountry = country;
                });
            }
        }

        private string _token;
        public string Token
        {
            get => _token;
            set => Set(ref _token, value);
        }

        private Country _selectedCountry;
        public Country SelectedCountry
        {
            get => _selectedCountry;
            set => Set(ref _selectedCountry, value);
        }

        private string _phoneNumber;
        public string PhoneNumber
        {
            get => _phoneNumber;
            set => Set(ref _phoneNumber, value);
        }

        public RelayCommand SwitchCommand { get; }
        private void SwitchExecute()
        {
            if (ClientService.AuthorizationState is AuthorizationStateWaitPhoneNumber)
            {
                ClientService.Send(new RequestQrCodeAuthentication());
            }
        }

        public RelayCommand SendCommand { get; }
        private async void SendExecute()
        {
            var phoneNumber = _phoneNumber?.Trim('+').Replace(" ", string.Empty);
            if (string.IsNullOrEmpty(_phoneNumber))
            {
                RaisePropertyChanged("PHONE_NUMBER_INVALID");
                return;
            }

            foreach (var session in _lifetimeService.Items)
            {
                // We don't want to check other accounts if current one is test
                if (Settings.UseTestDC || session.Settings.UseTestDC)
                {
                    continue;
                }

                var user = session.ClientService.GetUser(session.UserId);
                if (user == null)
                {
                    continue;
                }

                if (user.PhoneNumber.Contains(phoneNumber) || phoneNumber.Contains(user.PhoneNumber))
                {
                    var confirm = await ShowPopupAsync(Strings.AccountAlreadyLoggedIn, Strings.AppName, Strings.AccountSwitch, Strings.OK);
                    if (confirm == ContentDialogResult.Primary)
                    {
                        _lifetimeService.PreviousItem = session;
                        ClientService.Send(new Destroy());
                    }

                    return;
                }
            }

            IsLoading = true;

            await _notificationsService.CloseAsync();

            var function = new SetAuthenticationPhoneNumber(phoneNumber, new PhoneNumberAuthenticationSettings(false, false, false, false, null, new string[0]));
            Task<BaseObject> request;
            if (ClientService.AuthorizationState is AuthorizationStateWaitOtherDeviceConfirmation)
            {
                request = _sessionService.SetAuthenticationPhoneNumberAsync(function);
            }
            else
            {
                request = ClientService.SendAsync(function);
            }

            var response = await request;
            if (response is Error error)
            {
                IsLoading = false;

                if (error.TypeEquals(ErrorType.PHONE_NUMBER_INVALID))
                {
                    //needShowInvalidAlert(req.phone_number, false);
                    await ShowPopupAsync(Strings.InvalidPhoneNumber, Strings.AppName, Strings.OK);
                }
                else if (error.TypeEquals(ErrorType.PHONE_PASSWORD_FLOOD))
                {
                    await ShowPopupAsync(Strings.FloodWait, Strings.AppName, Strings.OK);
                }
                else if (error.TypeEquals(ErrorType.PHONE_NUMBER_FLOOD))
                {
                    await ShowPopupAsync(Strings.PhoneNumberFlood, Strings.AppName, Strings.OK);
                }
                else if (error.TypeEquals(ErrorType.PHONE_NUMBER_BANNED))
                {
                    //needShowInvalidAlert(req.phone_number, true);
                    await ShowPopupAsync(Strings.BannedPhoneNumber, Strings.AppName, Strings.OK);
                }
                else if (error.TypeEquals(ErrorType.PHONE_CODE_EMPTY) || error.TypeEquals(ErrorType.PHONE_CODE_INVALID))
                {
                    await ShowPopupAsync(Strings.InvalidCode, Strings.AppName, Strings.OK);
                }
                else if (error.TypeEquals(ErrorType.PHONE_CODE_EXPIRED))
                {
                    await ShowPopupAsync(Strings.CodeExpired, Strings.AppName, Strings.OK);
                }
                else if (error.Message.StartsWith("FLOOD_WAIT"))
                {
                    await ShowPopupAsync(Strings.FloodWait, Strings.AppName, Strings.OK);
                }
                else if (error.Code != -1000)
                {
                    await ShowPopupAsync(error.Message, Strings.AppName, Strings.OK);
                }
            }
        }

        public RelayCommand ProxyCommand { get; }
        private void ProxyExecute()
        {
            NavigationService.Navigate(typeof(SettingsProxiesPage));
        }

        #region Strings

        public Resources S { get; } = new();

        public class Resources : BindableBase
        {
            public string YourPhone => Strings.YourPhone;
            public string StartText => Strings.StartText;
            public string OK => Strings.OK;
            public string LoginWithQrCode => Strings.LoginWithQrCode;
            public string LoginWithQrCodeTitle => Strings.LoginWithQrCodeTitle;
            public string LoginWithQrCodeStep1 => Strings.LoginWithQrCodeStep1;
            public string LoginWithQrCodeStep2 => Strings.LoginWithQrCodeStep2;
            public string LoginWithQrCodeStep3 => Strings.LoginWithQrCodeStep3;
            public string LoginWithQrCodeSkip => Strings.LoginWithQrCodeSkip;
            public string ProxySettings => Strings.ProxySettings;

            private readonly string[] _keys = new[]
            {
                nameof(Strings.YourPhone),
                nameof(Strings.StartText),
                nameof(Strings.OK),
                nameof(Strings.LoginWithQrCode),
                nameof(Strings.LoginWithQrCodeTitle),
                nameof(Strings.LoginWithQrCodeStep1),
                nameof(Strings.LoginWithQrCodeStep2),
                nameof(Strings.LoginWithQrCodeStep3),
                nameof(Strings.LoginWithQrCodeSkip),
                nameof(Strings.ProxySettings)
            };

            public void Handle(LocaleChangedEventArgs update)
            {
                if (update.Strings.Count > 0)
                {
                    foreach (var key in update.Strings)
                    {
                        var index = Array.IndexOf(_keys, key.Key);
                        if (index != -1)
                        {
                            RaisePropertyChanged(key.Key);
                        }
                    }
                }
                else
                {
                    foreach (var key in _keys)
                    {
                        RaisePropertyChanged(key);
                    }
                }
            }
        }

        #endregion
    }
}