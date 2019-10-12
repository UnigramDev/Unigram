using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Unigram.Common;
using Unigram.Converters;
using Unigram.ViewModels.Wallet;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace Unigram.Views.Wallet
{
    public sealed partial class WalletInfoPage : Page
    {
        public WalletInfoViewModel ViewModel => DataContext as WalletInfoViewModel;

        public WalletInfoPage()
        {
            InitializeComponent();
            DataContext = TLContainer.Current.Resolve<WalletInfoViewModel>();

            Transitions = ApiInfo.CreateSlideTransition();
        }

        private Uri ConvertHeaderSource(WalletInfoState state)
        {
            switch (state)
            {
                case WalletInfoState.Created:
                    return new Uri("ms-appx:///Assets/Animations/WalletCreated.tgs");
                case WalletInfoState.Ready:
                case WalletInfoState.Sent:
                    return new Uri("ms-appx:///Assets/Animations/WalletDone.tgs");
                case WalletInfoState.TooBad:
                    return new Uri("ms-appx:///Assets/Animations/WalletNotAvailable.tgs");
                default:
                    return null;
            }
        }

        private string ConvertTitle(WalletInfoState state)
        {
            switch (state)
            {
                case WalletInfoState.Created:
                    return Strings.Resources.WalletCongratulations;
                case WalletInfoState.Ready:
                    return Strings.Resources.WalletReady;
                case WalletInfoState.Sent:
                    return Strings.Resources.WalletSendDone;
                case WalletInfoState.TooBad:
                    return Strings.Resources.WalletTooBad;
                default:
                    return null;
            }
        }

        private string ConvertText(WalletInfoState state, long amount)
        {
            switch (state)
            {
                case WalletInfoState.Created:
                    return Strings.Resources.WalletCongratulationsinfo;
                case WalletInfoState.Ready:
                    return Strings.Resources.WalletReadyInfo;
                case WalletInfoState.Sent:
                    return string.Format(Strings.Resources.WalletSendDoneText, BindConvert.Grams(amount, false));
                case WalletInfoState.TooBad:
                    return Strings.Resources.WalletTooBadInfo;
                default:
                    return null;
            }
        }

        private string ConvertButtonText(WalletInfoState state)
        {
            switch (state)
            {
                case WalletInfoState.Created:
                    return Strings.Resources.WalletContinue;
                case WalletInfoState.Ready:
                case WalletInfoState.Sent:
                    return Strings.Resources.WalletView;
                case WalletInfoState.TooBad:
                    return Strings.Resources.WalletTooBadEnter;
                default:
                    return null;
            }
        }

        private Visibility ConvertLinkVisibility(WalletInfoState state)
        {
            switch (state)
            {
                case WalletInfoState.TooBad:
                    return Visibility.Visible;
                default:
                    return Visibility.Collapsed;
            }
        }

        private void Hyperlink_Click(Windows.UI.Xaml.Documents.Hyperlink sender, Windows.UI.Xaml.Documents.HyperlinkClickEventArgs args)
        {
            ViewModel.SendCommand.Execute();
        }
    }
}
