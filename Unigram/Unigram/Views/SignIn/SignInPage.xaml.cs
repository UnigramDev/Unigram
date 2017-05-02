using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Unigram.Views;
using Unigram.ViewModels.SignIn;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.Web.Http;

namespace Unigram.Views.SignIn
{
    public sealed partial class SignInPage : Page
    {
        public SignInViewModel ViewModel => DataContext as SignInViewModel;

        public SignInPage()
        {
            InitializeComponent();

            DataContext = UnigramContainer.Current.ResolveType<SignInViewModel>();

            // Used to hide the app gray bar on desktop.
            // Currently this is always hidden on both family devices.
            //
            //if (!Windows.Foundation.Metadata.ApiInformation.IsTypePresent("Windows.UI.ViewManagement.StatusBar"))
            //{
            //    //txtMasterTitle.Visibility = Visibility.Visible;
            //    rpMasterTitlebar.Visibility = Visibility.Collapsed;
            //}





            // IDEA MATEI
            //this.Loaded += LoginPhoneNumberPage_Loaded;
        }

        // IDEA MATEI
        //private void LoginPhoneNumberPage_Loaded(object sender, RoutedEventArgs e)
        //{
        //    ViewModel.LocalizeCommand.Execute(sender);
        //}

        private void PhoneNumber_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                ViewModel.SendCommand.Execute(sender);
                e.Handled = true;
            }
        }
    }
}