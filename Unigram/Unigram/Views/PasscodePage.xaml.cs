using System;
using System.Linq;
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

        private bool _accepted;

        public PasscodePage(bool biometrics)
        {
            InitializeComponent();

            _passcodeService = TLContainer.Current.Resolve<IPasscodeService>();
            _biometrics = biometrics;

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

        #region Bounds

        private ApplicationView _applicationView;

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
                if (Field.Password.All(x => x >= '0' && x <= '9') && _passcodeService.Check(Field.Password))
                {
                    Unlock();
                }
                else
                {
                    VisualUtilities.ShakeView(Field);
                    Field.Password = string.Empty;
                }
            }
        }

        private void Field_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                if (_passcodeService.Check(Field.Password))
                {
                    Unlock();
                }
                else
                {
                    VisualUtilities.ShakeView(Field);
                    Field.Password = string.Empty;
                }
            }
        }

        private void Enter_Click(object sender, RoutedEventArgs e)
        {
            if (_passcodeService.Check(Field.Password))
            {
                Unlock();
            }
            else
            {
                VisualUtilities.ShakeView(Field);
                Field.Password = string.Empty;
            }
        }

        private void OnClosing(ContentDialog sender, ContentDialogClosingEventArgs args)
        {
            args.Cancel = _passcodeService.IsLocked || !_accepted;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            InputPane.GetForCurrentView().Showing += InputPane_Showing;
            InputPane.GetForCurrentView().Hiding += InputPane_Hiding;

            if (!_passcodeService.IsBiometricsEnabled)
            {
                Field.Focus(FocusState.Keyboard);
                return;
            }

            if (await KeyCredentialManager.IsSupportedAsync())
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

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            InputPane.GetForCurrentView().Showing -= InputPane_Showing;
            InputPane.GetForCurrentView().Hiding -= InputPane_Hiding;
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

        private void Unlock()
        {
            _passcodeService.Unlock();
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
                    }
                    else
                    {
                        var creationResult = await KeyCredentialManager.RequestCreateAsync(Strings.Resources.AppName, KeyCredentialCreationOption.ReplaceExisting);
                        if (creationResult.Status == KeyCredentialStatus.Success)
                        {
                            Unlock();
                        }
                    }
                }
            }
            catch { }
        }
    }
}
