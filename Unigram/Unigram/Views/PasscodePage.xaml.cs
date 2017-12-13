using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Telegram.Api.Helpers;
using Telegram.Api.Services.Cache;
using Template10.Common;
using Unigram.Common;
using Unigram.Converters;
using Unigram.Services;
using Windows.ApplicationModel;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Security.Credentials;
using Windows.Security.Cryptography;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace Unigram.Views
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class PasscodePage : ContentDialog
    {
        private readonly IPasscodeService _passcodeService;

        private bool _accepted;

        public PasscodePage()
        {
            InitializeComponent();

            _passcodeService = UnigramContainer.Current.ResolveType<IPasscodeService>();

            _applicationView = ApplicationView.GetForCurrentView();
            _applicationView.VisibleBoundsChanged += OnVisibleBoundsChanged;
            OnVisibleBoundsChanged(_applicationView, null);

            var user = InMemoryCacheService.Current.GetUser(SettingsHelper.UserId);
            if (user != null)
            {
                Photo.Source = DefaultPhotoConverter.Convert(user, false) as ImageSource;
                FullName.Text = user.FullName;
            }
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
            if (Field.Password.Length == 4)
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
                var result = await KeyCredentialManager.OpenAsync(Strings.Android.AppName);
                if (result.Credential != null)
                {
                    var signResult = await result.Credential.RequestSignAsync(CryptographicBuffer.ConvertStringToBinary(Package.Current.Id.Name, BinaryStringEncoding.Utf8));
                    if (signResult.Status == KeyCredentialStatus.Success)
                    {
                        Unlock();
                    }
                    else
                    {
                        Biometrics.Visibility = Visibility.Visible;
                    }
                }
                else
                {
                    var creationResult = await KeyCredentialManager.RequestCreateAsync(Strings.Android.AppName, KeyCredentialCreationOption.ReplaceExisting);
                    if (creationResult.Status == KeyCredentialStatus.Success)
                    {
                        Unlock();
                    }
                    else
                    {
                        Biometrics.Visibility = Visibility.Visible;
                    }
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
            Field.Margin = new Thickness(0, 0, 0, Math.Max(args.OccludedRect.Height + 8, 120));
        }

        private void InputPane_Hiding(InputPane sender, InputPaneVisibilityEventArgs args)
        {
            args.EnsuredFocusedElementInView = true;
            Field.Margin = new Thickness(0, 0, 0, 120);
        }

        private void Unlock()
        {
            _passcodeService.Unlock();
            _accepted = true;

            Hide();
        }
    }
}
