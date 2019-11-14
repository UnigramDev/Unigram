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
using Unigram.Entities;
using System.Text;
using Windows.ApplicationModel;

namespace Unigram.Views.SignIn
{
    public sealed partial class SignInPage : Page
    {
        public SignInViewModel ViewModel => DataContext as SignInViewModel;

        public SignInPage()
        {
            InitializeComponent();
            DataContext = TLContainer.Current.Resolve<SignInViewModel>();

            Transitions = ApiInfo.CreateSlideTransition();

            Diagnostics.Text = $"Unigram " + GetVersion();

            ViewModel.PropertyChanged += OnPropertyChanged;
        }

        private string GetVersion()
        {
            Package package = Package.Current;
            PackageId packageId = package.Id;
            PackageVersion version = packageId.Version;
            return string.Format("{0}.{1}.{2}", version.Major, version.Minor, version.Build, version.Revision);
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
                ViewModel.SendCommand.Execute(null);
                e.Handled = true;
            }
            else if (e.Key == Windows.System.VirtualKey.Back && string.IsNullOrEmpty(PrimaryInput.Text))
            {
                PhoneCode.Focus(FocusState.Keyboard);
                PhoneCode.SelectionStart = PhoneCode.Text.Length;
                e.Handled = true;
            }
        }

        #region Binding

        private string ConvertFormat(Country country)
        {
            if (country == null)
            {
                return null;
            }

            var groups = PhoneNumber.Parse(country.PhoneCode);
            var builder = new StringBuilder();

            for (int i = 1; i < groups.Length; i++)
            {
                for (int j = 0; j < groups[i]; j++)
                {
                    builder.Append('-');
                }

                if (i + 1 < groups.Length)
                {
                    builder.Append(' ');
                }
            }

            return builder.ToString();
        }

        #endregion

        private int _advanced;

        private void Diagnostics_Click(object sender, RoutedEventArgs e)
        {
            _advanced++;

            if (_advanced >= 10)
            {
                _advanced = 0;

                Frame.Navigate(typeof(DiagnosticsPage));
            }
        }
    }
}