﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Telegram.Api.TL;
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
    public sealed partial class SignInSentCodePage : Page
    {
        public SignInSentCodeViewModel ViewModel => DataContext as SignInSentCodeViewModel;

        public SignInSentCodePage()
        {
            InitializeComponent();

            DataContext = UnigramContainer.Current.ResolveType<SignInSentCodeViewModel>();

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
