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
    public sealed partial class SignInPasswordPage : Page
    {
        public SignInPasswordViewModel ViewModel => DataContext as SignInPasswordViewModel;

        public SignInPasswordPage()
        {
            InitializeComponent();
            DataContext = UnigramContainer.Current.ResolveType<SignInPasswordViewModel>();
        }

        private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            ((PasswordBox)sender).GetBindingExpression(PasswordBox.PasswordProperty)?.UpdateSource();
        }

        public class NavigationParameters
        {
            public string PhoneNumber { get; set; }
            public string PhoneCode { get; set; }
            public TLAuthSentCode Result { get; set; }
            public TLAccountPassword Password { get; set; }
        }
    }
}
