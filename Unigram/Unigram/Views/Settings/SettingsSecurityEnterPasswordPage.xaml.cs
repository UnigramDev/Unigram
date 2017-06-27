using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Unigram.ViewModels.Settings;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace Unigram.Views.Settings
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class SettingsSecurityEnterPasswordPage : Page
    {
        public SettingsSecurityEnterPasswordViewModel ViewModel => DataContext as SettingsSecurityEnterPasswordViewModel;

        public SettingsSecurityEnterPasswordPage()
        {
            InitializeComponent();
            DataContext = UnigramContainer.Current.ResolveType<SettingsSecurityEnterPasswordViewModel>();
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            PrimaryInput.Focus(FocusState.Keyboard);
        }

        private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            ((PasswordBox)sender).GetBindingExpression(PasswordBox.PasswordProperty)?.UpdateSource();
        }

        private void PasswordBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                ViewModel.SendCommand.Execute(sender);
                e.Handled = true;
            }
        }
    }
}
