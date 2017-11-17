using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Telegram.Api.TL;
using Unigram.Views;
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
using Telegram.Api.TL.Auth;

namespace Unigram.Views.Settings
{
    public sealed partial class SettingsPhoneSentCodePage : Page
    {
        public SettingsPhoneSentCodeViewModel ViewModel => DataContext as SettingsPhoneSentCodeViewModel;

        public SettingsPhoneSentCodePage()
        {
            InitializeComponent();
            DataContext = UnigramContainer.Current.ResolveType<SettingsPhoneSentCodeViewModel>();

            // Used to hide the app gray bar on desktop.
            // Currently this is always hidden on both family devices.
            //
            //if (!Windows.Foundation.Metadata.ApiInformation.IsTypePresent("Windows.UI.ViewManagement.StatusBar"))
            //{
            //    rpMasterTitlebar.Visibility = Visibility.Collapsed;
            //}
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            PrimaryInput.Focus(FocusState.Keyboard);
        }

        public class NavigationParameters
        {
            public string PhoneNumber { get; set; }
            public TLAuthSentCode Result { get; set; }
        }
    }
}
