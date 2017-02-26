using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Unigram.Core.Dependency;
using Unigram.ViewModels.Login;
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

namespace Unigram.Views.Login
{
    public sealed partial class LoginPhoneNumberPage : Page
    {
        public LoginPhoneNumberViewModel ViewModel => DataContext as LoginPhoneNumberViewModel;

        public LoginPhoneNumberPage()
        {
            InitializeComponent();

            DataContext = UnigramContainer.Current.ResolveType<LoginPhoneNumberViewModel>();

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