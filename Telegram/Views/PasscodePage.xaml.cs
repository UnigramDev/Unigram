//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Navigation;
using Telegram.Services;
using Telegram.Td.Api;
using Windows.ApplicationModel;
using Windows.Security.Credentials;
using Windows.Security.Cryptography;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;

namespace Telegram.Views
{
    public sealed partial class PasscodePage : UserControlEx
    {
        private readonly WindowContext _window;

        private readonly IPasscodeService _passcodeService;
        private readonly bool _biometrics;

        private readonly DispatcherTimer _retryTimer;

        public PasscodePage(WindowContext window, bool biometrics)
        {
            InitializeComponent();

            _window = window;
            _window.SetTitleBar(TitleBar);

            _passcodeService = LifetimeService.Current.Passcode;
            _biometrics = biometrics;

            _retryTimer = new DispatcherTimer();
            _retryTimer.Interval = TimeSpan.FromMilliseconds(100);
            _retryTimer.Tick += Retry_Tick;

            if (_passcodeService.RetryIn > 0)
            {
                _retryTimer.Start();
            }

            //var user = InMemoryCacheService.Current.GetUser(SettingsHelper.UserId);
            //if (user != null)
            //{
            //    Photo.Source = DefaultPhotoConverter.Convert(user, false) as ImageSource;
            //    FullName.Text = user.FullName;
            //}

            var confirmScope = new InputScope();
            confirmScope.Names.Add(new InputScopeName(_passcodeService.IsSimple ? InputScopeNameValue.NumericPin : InputScopeNameValue.Password));

            Field.InputScope = confirmScope;
            Field.MaxLength = _passcodeService.IsSimple ? 4 : int.MaxValue;
        }

        private void Retry_Tick(object sender, object e)
        {
            if (_passcodeService.RetryIn > 0)
            {
                RetryIn.Visibility = Visibility.Visible;
                RetryIn.Text = string.Format(Strings.TooManyTries, Locale.Declension(Strings.R.Seconds, _passcodeService.RetryIn));
            }
            else
            {
                _retryTimer.Stop();
                RetryIn.Visibility = Visibility.Collapsed;
            }
        }

        private void Field_LosingFocus(UIElement sender, LosingFocusEventArgs args)
        {
            args.TryCancel();
        }

        private void Field_TextChanged(object sender, RoutedEventArgs e)
        {
            if (_passcodeService.IsSimple && Field.Password.Length == 4)
            {
                TryUnlock();
            }
        }

        private void Field_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Enter)
            {
                TryUnlock();
            }
        }

        private void Enter_Click(object sender, RoutedEventArgs e)
        {
            TryUnlock();
        }

        private void TryUnlock()
        {
            if (_passcodeService.TryUnlock(Field.Password))
            {
                Unlock();
            }
            else
            {
                Lock();
            }
        }

        private async void OnLoaded(object sender, RoutedEventArgs args)
        {
            _window.Activated += Window_Activated;
            Field.LosingFocus += Field_LosingFocus;

            if (_passcodeService.IsBiometricsEnabled && await KeyCredentialManager.IsSupportedAsync())
            {
                Biometrics.Visibility = Visibility.Visible;

                if (_biometrics)
                {
                    Biometrics_Click(null, null);
                }
            }
            else
            {
                Field.Focus(FocusState.Keyboard);
            }
        }

        private void OnUnloaded(object sender, RoutedEventArgs args)
        {
            _window.Activated -= Window_Activated;
            Field.LosingFocus -= Field_LosingFocus;

            _retryTimer.Stop();
        }

        private void Window_Activated(object sender, WindowActivatedEventArgs e)
        {
            _window.SetTitleBar(TitleBar);

            if (e.WindowActivationState != CoreWindowActivationState.Deactivated)
            {
                Field.Focus(FocusState.Keyboard);
            }
        }

        private void Lock()
        {
            if (_passcodeService.RetryIn > 0)
            {
                _retryTimer.Start();
            }

            VisualUtilities.ShakeView(Field);
            Field.Password = string.Empty;
        }

        private void Unlock()
        {
            _passcodeService.Unlock();
            _retryTimer.Stop();
        }

        private async void Biometrics_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var result = await KeyCredentialManager.OpenAsync(Strings.AppName);
                if (result.Credential != null)
                {
                    var signResult = await result.Credential.RequestSignAsync(CryptographicBuffer.ConvertStringToBinary(Package.Current.Id.Name, BinaryStringEncoding.Utf8));
                    if (signResult.Status == KeyCredentialStatus.Success)
                    {
                        Unlock();
                    }
                    else
                    {
                        Field.Focus(FocusState.Keyboard);
                    }
                }
                else
                {
                    var creationResult = await KeyCredentialManager.RequestCreateAsync(Strings.AppName, KeyCredentialCreationOption.ReplaceExisting);
                    if (creationResult.Status == KeyCredentialStatus.Success)
                    {
                        Unlock();
                    }
                    else
                    {
                        Field.Focus(FocusState.Keyboard);
                    }
                }
            }
            catch
            {
                Field.Focus(FocusState.Keyboard);
            }
        }

        private async void LogOut_Click(object sender, RoutedEventArgs e)
        {
            var confirm = await MessagePopup.ShowAsync(XamlRoot, Strings.AreYouSureLogout, Strings.AppName, Strings.LogOut, Strings.Cancel, destructive: true);
            if (confirm == ContentDialogResult.Primary)
            {
                foreach (var client in LifetimeService.Current.ResolveAll<IClientService>())
                {
                    client.Send(new LogOut());
                }
            }
        }
    }
}
