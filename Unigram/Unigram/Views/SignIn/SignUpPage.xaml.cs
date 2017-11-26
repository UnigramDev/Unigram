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
using Telegram.Api.TL.Auth;
using Unigram.Common;

namespace Unigram.Views.SignIn
{
    public sealed partial class SignUpPage : Page
    {
        public SignUpViewModel ViewModel => DataContext as SignUpViewModel;

        public SignUpPage()
        {
            InitializeComponent();
            DataContext = UnigramContainer.Current.ResolveType<SignUpViewModel>();

            ViewModel.PropertyChanged += OnPropertyChanged;
        }

        private void OnPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case "FIRSTNAME_INVALID":
                    VisualUtilities.ShakeView(PrimaryInput);
                    break;
            }
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            PrimaryInput.Focus(FocusState.Keyboard);
        }

        public class NavigationParameters
        {
            public string PhoneNumber { get; set; }
            public string PhoneCode { get; set; }
            public TLAuthSentCode Result { get; set; }
        }
    }
}
