using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
using Unigram.ViewModels.Wallet;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace Unigram.Views.Wallet
{
    public sealed partial class WalletCreatePage : Page
    {
        public WalletCreateViewModel ViewModel => DataContext as WalletCreateViewModel;

        public WalletCreatePage()
        {
            InitializeComponent();
            DataContext = TLContainer.Current.Resolve<WalletCreateViewModel>();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            Frame.BackStack.Clear();
        }

        private string ConvertTos()
        {
            // WalletTosUrl
            var text = Strings.Resources.CreateMyWalletTerms;
            var builder = new StringBuilder(text);

            var index = text.IndexOf('*');
            var lastIndex = text.LastIndexOf('*');

            if (index != -1 && lastIndex != -1)
            {
                builder.Remove(lastIndex, 1);
                builder.Insert(lastIndex, $"]({Strings.Resources.WalletTosUrl})");
                builder.Remove(index, 1);
                builder.Insert(index, "[");
            }

            return builder.ToString();
        }
    }
}
