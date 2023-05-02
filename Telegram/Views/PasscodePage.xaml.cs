//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Services;
using Windows.ApplicationModel;
using Windows.Security.Credentials;
using Windows.Security.Cryptography;
using Windows.UI.Core;
using Windows.UI.ViewManagement.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;

namespace Telegram.Views
{
    public sealed partial class PasscodePage : ContentPopup
    {
        private readonly IPasscodeService _passcodeService;
        private readonly bool _biometrics;

        private readonly DispatcherTimer _retryTimer;

        private bool _accepted;

        public PasscodePage(bool biometrics)
        {
            InitializeComponent();

            _passcodeService = TLContainer.Current.Passcode;
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

        private void Field_TextChanged(object sender, RoutedEventArgs e)
        {
            if (_passcodeService.IsSimple && Field.Password.Length == 4)
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
        }

        private void Field_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
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
        }

        private void Field_LosingFocus(UIElement sender, LosingFocusEventArgs args)
        {
            args.TryCancel();
        }

        private void Enter_Click(object sender, RoutedEventArgs e)
        {
            if (_passcodeService.TryUnlock(Field.Password))
            {
                Unlock();
            }
            else
            {
                Lock();

                CoreInputView.GetForCurrentView().TryShow();
            }
        }

        private void OnClosing(ContentDialog sender, ContentDialogClosingEventArgs args)
        {
            args.Cancel = _passcodeService.IsLocked || !_accepted;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            Window.Current.Activated += Window_Activated;
            Window.Current.SizeChanged += Window_SizeChanged;

            UpdateView();

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
                CoreInputView.GetForCurrentView().TryShow();
            }
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            Window.Current.Activated -= Window_Activated;
            Window.Current.SizeChanged -= Window_SizeChanged;
        }

        #region Bounds

        private void Window_Activated(object sender, WindowActivatedEventArgs e)
        {
            if (e.WindowActivationState != CoreWindowActivationState.Deactivated)
            {
                Field.Focus(FocusState.Keyboard);
            }
        }

        private void Window_SizeChanged(object sender, WindowSizeChangedEventArgs e)
        {
            Telegram.App.Track();

            UpdateView();
        }

        private void UpdateView()
        {
            var bounds = Window.Current.Bounds;

            Margin = new Thickness();
            MinWidth = bounds.Width;
            MinHeight = bounds.Height;
            MaxWidth = bounds.Width;
            MaxHeight = bounds.Height;

            LayoutRoot.Width = bounds.Width;
            LayoutRoot.Height = bounds.Height;
        }

        #endregion

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
            _accepted = true;

            Hide();
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
    }
}
