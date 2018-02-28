using System;
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
using TdWindows;
using Telegram.Helpers;

namespace Unigram.Views.SignIn
{
    public sealed partial class SignInSentCodePage : Page
    {
        public SignInSentCodeViewModel ViewModel => DataContext as SignInSentCodeViewModel;

        public SignInSentCodePage()
        {
            InitializeComponent();
            DataContext = UnigramContainer.Current.ResolveType<SignInSentCodeViewModel>();

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

        private string ConvertType(AuthorizationStateWaitCode waitCode, string number)
        {
            if (waitCode == null)
            {
                return null;
            }

            switch (waitCode.CodeInfo.Type)
            {
                case AuthenticationCodeTypeTelegramMessage appType:
                    return Strings.Resources.SentAppCode;
                case AuthenticationCodeTypeSms smsType:
                    return string.Format(Strings.Resources.SentSmsCode, PhoneNumber.Format(number));
            }

            return null;
        }

        #endregion

        public class NavigationParameters
        {
            public string PhoneNumber { get; set; }
            public AuthorizationStateWaitCode Result { get; set; }
        }
    }
}
