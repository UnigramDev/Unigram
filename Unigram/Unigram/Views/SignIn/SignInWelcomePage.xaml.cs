using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Unigram.Views;
using Unigram.ViewModels.SignIn;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace Unigram.Views.SignIn
{
    public sealed partial class SignInWelcomePage : Page
    {
        public SignInWelcomeViewModel ViewModel => DataContext as SignInWelcomeViewModel;

        public SignInWelcomePage()
        {
            InitializeComponent();

            DataContext = UnigramContainer.Current.ResolveType<SignInWelcomeViewModel>();

            // Used to hide the app gray bar on desktop.
            // Currently this is always hidden on both family devices.
            //
            //if (!Windows.Foundation.Metadata.ApiInformation.IsTypePresent("Windows.UI.ViewManagement.StatusBar"))
            //{
            //    txtMasterTitle.Visibility = Visibility.Visible;
            //    rpMasterTitlebar.Visibility = Visibility.Collapsed;
            //}
        }
    }
}
