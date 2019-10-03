using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
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
    public sealed partial class WalletSettingsPage : Page
    {
        public WalletSettingsViewModel ViewModel => DataContext as WalletSettingsViewModel;

        public WalletSettingsPage()
        {
            InitializeComponent();
            DataContext = TLContainer.Current.Resolve<WalletSettingsViewModel>();
        }
    }
}
