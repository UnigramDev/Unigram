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
using Unigram.Common;
using Telegram.Td.Api;

namespace Unigram.Views.SignIn
{
    public sealed partial class SignInSentCodePage : Page
    {
        public SignInSentCodeViewModel ViewModel => DataContext as SignInSentCodeViewModel;

        public SignInSentCodePage()
        {
            InitializeComponent();
            DataContext = TLContainer.Current.Resolve<SignInSentCodeViewModel>();

            Transitions = ApiInfo.CreateSlideTransition();

            ViewModel.PropertyChanged += OnPropertyChanged;
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

        private string ConvertType(AuthenticationCodeInfo codeInfo)
        {
            if (codeInfo == null)
            {
                return null;
            }

            switch (codeInfo.Type)
            {
                case AuthenticationCodeTypeTelegramMessage appType:
                    return Strings.Resources.SentAppCode;
                case AuthenticationCodeTypeSms smsType:
                    return string.Format(Strings.Resources.SentSmsCode, PhoneNumber.Format(codeInfo.PhoneNumber));
            }

            return null;
        }

        #endregion
    }
}
