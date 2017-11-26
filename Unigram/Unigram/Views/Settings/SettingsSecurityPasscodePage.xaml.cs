using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Controls.Views;
using Unigram.ViewModels.Settings;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Security.Credentials;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace Unigram.Views.Settings
{
    public sealed partial class SettingsSecurityPasscodePage : Page
    {
        public SettingsSecurityPasscodeViewModel ViewModel => DataContext as SettingsSecurityPasscodeViewModel;

        public SettingsSecurityPasscodePage()
        {
            InitializeComponent();
            DataContext = UnigramContainer.Current.ResolveType<SettingsSecurityPasscodeViewModel>();
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            Biometrics.Visibility = await KeyCredentialManager.IsSupportedAsync() ? Visibility.Visible : Visibility.Collapsed;
        }

        private async void Enabled_Toggled(object sender, RoutedEventArgs e)
        {
            if (Enabled.FocusState == FocusState.Unfocused)
            {
                return;
            }

            if (ViewModel.Passcode.IsEnabled)
            {
                ViewModel.Passcode.Reset();
            }
            else if (Enabled.IsOn)
            {
                var timeout = ViewModel.Passcode.AutolockTimeout + 0;
                var dialog = new SettingsSecurityPasscodeEditView();

                var confirm = await dialog.ShowQueuedAsync();
                if (confirm == ContentDialogResult.Primary)
                {
                    var passcode = dialog.Passcode;
                    ViewModel.Passcode.Set(passcode, true, timeout);
                    InactivityHelper.Initialize(timeout);
                }
                else
                {
                    Enabled.IsOn = false;
                }
            }
        }

        private async void Edit_Click(object sender, RoutedEventArgs e)
        {
            var timeout = ViewModel.Passcode.AutolockTimeout + 0;
            var dialog = new SettingsSecurityPasscodeEditView();

            var confirm = await dialog.ShowQueuedAsync();
            if (confirm == ContentDialogResult.Primary)
            {
                var passcode = dialog.Passcode;
                ViewModel.Passcode.Set(passcode, true, timeout);
                InactivityHelper.Initialize(timeout);
            }
        }
    }
}
