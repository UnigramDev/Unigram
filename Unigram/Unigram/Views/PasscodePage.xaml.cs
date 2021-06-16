using System;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Services;
using Windows.ApplicationModel;
using Windows.Foundation;
using Windows.Security.Credentials;
using Windows.Security.Cryptography;
using Windows.UI.Core;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;

namespace Unigram.Views
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

            _applicationView = ApplicationView.GetForCurrentView();
            _applicationView.VisibleBoundsChanged += OnVisibleBoundsChanged;
            OnVisibleBoundsChanged(_applicationView, null);

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
                RetryIn.Text = string.Format(Strings.Resources.TooManyTries, Locale.Declension("Seconds", _passcodeService.RetryIn));
            }
            else
            {
                _retryTimer.Stop();
                RetryIn.Visibility = Visibility.Collapsed;
            }
        }

        #region Bounds

        private readonly ApplicationView _applicationView;

        private void OnVisibleBoundsChanged(ApplicationView sender, object args)
        {
            if (sender == null)
            {
                return;
            }

            if (/*BackgroundElement != null &&*/ Window.Current?.Bounds is Rect bounds && sender.VisibleBounds != bounds)
            {
                Margin = new Thickness(sender.VisibleBounds.X - bounds.Left, sender.VisibleBounds.Y - bounds.Top, bounds.Width - (sender.VisibleBounds.Right - bounds.Left), bounds.Height - (sender.VisibleBounds.Bottom - bounds.Top));
                UpdateViewBase();
            }
            else
            {
                Margin = new Thickness();
                UpdateViewBase();
            }
        }

        private void UpdateViewBase()
        {
            var bounds = _applicationView.VisibleBounds;
            MinWidth = bounds.Width;
            MinHeight = bounds.Height;
            MaxWidth = bounds.Width;
            MaxHeight = bounds.Height;

            LayoutRoot.Width = bounds.Width;
            LayoutRoot.Height = bounds.Height;
        }

        #endregion

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

                InputPane.GetForCurrentView().TryShow();
            }
        }

        private void OnClosing(ContentDialog sender, ContentDialogClosingEventArgs args)
        {
            args.Cancel = _passcodeService.IsLocked || !_accepted;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            Window.Current.Activated += Window_Activated;

            InputPane.GetForCurrentView().Showing += InputPane_Showing;
            InputPane.GetForCurrentView().Hiding += InputPane_Hiding;

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
                InputPane.GetForCurrentView().TryShow();
            }
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            Window.Current.Activated -= Window_Activated;

            InputPane.GetForCurrentView().Showing -= InputPane_Showing;
            InputPane.GetForCurrentView().Hiding -= InputPane_Hiding;
        }

        private void Window_Activated(object sender, WindowActivatedEventArgs e)
        {
            if (e.WindowActivationState != CoreWindowActivationState.Deactivated)
            {
                Field.Focus(FocusState.Keyboard);
            }
        }

        private void InputPane_Showing(InputPane sender, InputPaneVisibilityEventArgs args)
        {
            args.EnsuredFocusedElementInView = true;
            FieldPanel.Margin = new Thickness(0, 0, 0, Math.Max(args.OccludedRect.Height + 8, 120));
        }

        private void InputPane_Hiding(InputPane sender, InputPaneVisibilityEventArgs args)
        {
            args.EnsuredFocusedElementInView = true;
            FieldPanel.Margin = new Thickness(0, 0, 0, 120);
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
            _retryTimer.Stop();
            _accepted = true;

            Hide();
        }

        private async void Biometrics_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var windowContext = TLWindowContext.GetForCurrentView();
                if (windowContext.ActivationMode == CoreWindowActivationMode.ActivatedInForeground)
                {
                    var result = await KeyCredentialManager.OpenAsync(Strings.Resources.AppName);
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
                        var creationResult = await KeyCredentialManager.RequestCreateAsync(Strings.Resources.AppName, KeyCredentialCreationOption.ReplaceExisting);
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
            }
            catch
            {
                Field.Focus(FocusState.Keyboard);
            }
        }
    }
}
