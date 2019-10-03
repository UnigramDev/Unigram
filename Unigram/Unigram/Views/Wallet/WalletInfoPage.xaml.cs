using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Unigram.Common;
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
                    return new Uri("ms-appx:///Assets/Animations/WalletDone.tgs");
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
                default:
                    return null;
            }
        }

        private string ConvertText(WalletInfoState state)
        {
            switch (state)
            {
                case WalletInfoState.Created:
                    return Strings.Resources.WalletCongratulationsinfo;
                case WalletInfoState.Ready:
                    return Strings.Resources.WalletReadyInfo;
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
                    return Strings.Resources.WalletView;
                default:
                    return null;
            }
        }
    }
}
