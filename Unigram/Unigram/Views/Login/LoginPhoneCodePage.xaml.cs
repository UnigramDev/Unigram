using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Telegram.Api.TL;
using Unigram.Core.Dependency;
using Unigram.ViewModels.Login;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace Unigram.Views.Login
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class LoginPhoneCodePage : Page
    {
        public LoginPhoneCodeViewModel ViewModel => DataContext as LoginPhoneCodeViewModel;

        public LoginPhoneCodePage()
        {
            InitializeComponent();

            DataContext = UnigramContainer.Current.ResolveType<LoginPhoneCodeViewModel>();

            // Used to hide the app gray bar on desktop.
            // Currently this is always hidden on both family devices.
            //
            //if (!Windows.Foundation.Metadata.ApiInformation.IsTypePresent("Windows.UI.ViewManagement.StatusBar"))
            //{
            //    rpMasterTitlebar.Visibility = Visibility.Collapsed;
            //}
        }

        public class NavigationParameters
        {
            public string PhoneNumber { get; set; }
            public TLAuthSentCode Result { get; set; }
        }
    }
}
