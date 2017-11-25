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
using Unigram.Common;

namespace Unigram.Views.SignIn
{
    public sealed partial class SignInPage : Page
    {
        public SignInViewModel ViewModel => DataContext as SignInViewModel;

        public SignInPage()
        {
            InitializeComponent();
            DataContext = UnigramContainer.Current.ResolveType<SignInViewModel>();

            ViewModel.PropertyChanged += OnPropertyChanged;
        }

        private void OnPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case "PHONE_CODE_INVALID":
                    VisualUtilities.ShakeView(PhoneCode);
                    break;
                case "PHONE_NUMBER_INVALID":
                    VisualUtilities.ShakeView(PrimaryInput);
                    break;
            }
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            PrimaryInput.Focus(FocusState.Keyboard);
        }

        private void PhoneNumber_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                ViewModel.SendCommand.Execute(sender);
                e.Handled = true;
            }
            else if (e.Key == Windows.System.VirtualKey.Back && string.IsNullOrEmpty(PrimaryInput.Text))
            {
                PhoneCode.Focus(FocusState.Keyboard);
                PhoneCode.SelectionStart = PhoneCode.Text.Length;
                e.Handled = true;
            }
        }
    }
}