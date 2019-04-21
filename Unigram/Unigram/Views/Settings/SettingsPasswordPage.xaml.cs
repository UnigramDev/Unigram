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

namespace Unigram.Views.Settings
{
    public sealed partial class SettingsPasswordPage : Page
    {
        public SettingsPasswordViewModel ViewModel => DataContext as SettingsPasswordViewModel;

        public SettingsPasswordPage()
        {
            InitializeComponent();
            DataContext = TLContainer.Current.Resolve<SettingsPasswordViewModel>();
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            //PrimaryInput.Focus(FocusState.Keyboard);
        }

        //private void PasswordBox_KeyDown(object sender, KeyRoutedEventArgs e)
        //{
        //    if (e.Key == Windows.System.VirtualKey.Enter)
        //    {
        //        ViewModel.SendCommand.Execute(sender);
        //        e.Handled = true;
        //    }
        //}
    }
}
