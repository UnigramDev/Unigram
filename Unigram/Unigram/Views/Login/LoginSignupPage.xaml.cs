﻿using System;
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

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace Unigram.Views.Login
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class LoginSignUpPage : Page
    {
        public LoginSignUpViewModel ViewModel => DataContext as LoginSignUpViewModel;

        public LoginSignUpPage()
        {
            InitializeComponent();
            DataContext = UnigramContainer.Current.ResolveType<LoginSignUpViewModel>();
        }

        public class NavigationParameters
        {
            public string PhoneNumber { get; set; }
            public string PhoneCode { get; set; }
            public TLAuthSentCode Result { get; set; }
        }
    }
}
