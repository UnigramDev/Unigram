//
// Copyright Fela Ameghino & Contributors 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Telegram.Common;
using Telegram.Controls;
using Telegram.Streams;
using Telegram.Td.Api;
using Telegram.ViewModels.Authorization;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Navigation;

namespace Telegram.Views.Authorization
{
    public sealed partial class AuthorizationCodePage : CorePage
    {
        public AuthorizationCodeViewModel ViewModel => DataContext as AuthorizationCodeViewModel;

        public AuthorizationCodePage()
        {
            InitializeComponent();
            Window.Current.SetTitleBar(TitleBar);
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            ViewModel.PropertyChanged += OnPropertyChanged;
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            ViewModel.PropertyChanged -= OnPropertyChanged;
        }

        protected override void OnLayoutMetricsChanged(SystemOverlayMetrics metrics)
        {
            Back.HorizontalAlignment = metrics.LeftInset > 0
                ? HorizontalAlignment.Right
                : HorizontalAlignment.Left;
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

        private LocalFileSource ConvertAnimation(AuthenticationCodeInfo codeInfo)
        {
            return codeInfo?.Type switch
            {
                AuthenticationCodeTypeFragment => new LocalFileSource("ms-appx:///Assets/Animations/AuthorizationStateWaitFragment.tgs"),
                _ => new LocalFileSource("ms-appx:///Assets/Animations/AuthorizationStateWaitCode.tgs")
            };
        }

        private string ConvertType(AuthenticationCodeInfo codeInfo)
        {
            return codeInfo?.Type switch
            {
                AuthenticationCodeTypeTelegramMessage => Strings.SentAppCode,
                AuthenticationCodeTypeFragment => string.Format(Strings.SentFragmentCode, PhoneNumber.Format(codeInfo.PhoneNumber)),
                AuthenticationCodeTypeSms => string.Format(Strings.SentSmsCode, PhoneNumber.Format(codeInfo.PhoneNumber)),
                _ => string.Empty
            };
        }

        private string ConvertNext(AuthenticationCodeInfo codeInfo, string code)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                return codeInfo?.Type switch
                {
                    AuthenticationCodeTypeFragment => Strings.OpenFragment,
                    _ => Strings.OK
                };
            }

            return Strings.OK;
        }

        #endregion

    }
}
