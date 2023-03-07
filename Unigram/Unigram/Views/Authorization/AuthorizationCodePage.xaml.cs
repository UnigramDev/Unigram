//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Navigation;
using Unigram.ViewModels.Authorization;

namespace Unigram.Views.Authorization
{
    public sealed partial class AuthorizationCodePage : Page
    {
        public AuthorizationCodeViewModel ViewModel => DataContext as AuthorizationCodeViewModel;

        public AuthorizationCodePage()
        {
            InitializeComponent();
        }

        protected override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            var window = WindowContext.ForXamlRoot(XamlRoot);
            if (window != null)
            {
                window.Window.SetTitleBar(TitleBar);
            }
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            ViewModel.PropertyChanged += OnPropertyChanged;
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            ViewModel.PropertyChanged -= OnPropertyChanged;
        }

        private void OnPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case "SENT_CODE_INVALID":
                    VisualUtilities.ShakeView(PrimaryInput);
                    break;
            }
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            PrimaryInput.Focus(FocusState.Keyboard);
        }

        #region Binding

        private Uri ConvertAnimation(AuthenticationCodeInfo codeInfo)
        {
            return codeInfo?.Type switch
            {
                AuthenticationCodeTypeFragment => new Uri("ms-appx:///Assets/Animations/AuthorizationStateWaitFragment.tgs"),
                _ => new Uri("ms-appx:///Assets/Animations/AuthorizationStateWaitCode.tgs")
            };
        }

        private string ConvertType(AuthenticationCodeInfo codeInfo)
        {
            return codeInfo?.Type switch
            {
                AuthenticationCodeTypeTelegramMessage => Strings.Resources.SentAppCode,
                AuthenticationCodeTypeFragment => string.Format(Strings.Resources.SentFragmentCode, PhoneNumber.Format(codeInfo.PhoneNumber)),
                AuthenticationCodeTypeSms => string.Format(Strings.Resources.SentSmsCode, PhoneNumber.Format(codeInfo.PhoneNumber)),
                _ => string.Empty
            };
        }

        private string ConvertNext(AuthenticationCodeInfo codeInfo, string code)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                return codeInfo?.Type switch
                {
                    AuthenticationCodeTypeFragment => Strings.Resources.OpenFragment,
                    _ => Strings.Resources.OK
                };
            }

            return Strings.Resources.OK;
        }

        #endregion

    }
}
