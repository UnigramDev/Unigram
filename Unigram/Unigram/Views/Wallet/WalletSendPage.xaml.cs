using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Unigram.Common;
using Unigram.ViewModels.Wallet;
using Windows.ApplicationModel.DataTransfer;
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
    public sealed partial class WalletSendPage : Page
    {
        public WalletSendViewModel ViewModel => DataContext as WalletSendViewModel;

        public WalletSendPage()
        {
            InitializeComponent();
            DataContext = TLContainer.Current.Resolve<WalletSendViewModel>();
        }

        #region Binding

        private string ConvertAmount(long value)
        {
            return (value / 1000000000d).ToString("0.000000000");
        }

        private void ConvertAmountBack(string value)
        {
            if (double.TryParse(value, out double result))
            {
                ViewModel.Amount = (long)(result * 1000000000d);
            }
            else
            {
                ViewModel.Amount = 0;
            }
        }

        #endregion

        private async void Address_Paste(object sender, TextControlPasteEventArgs e)
        {
            var textBox = sender as TextBox;
            if (!string.IsNullOrWhiteSpace(textBox.Text))
            {
                return;
            }

            e.Handled = true;

            var clipboard = Clipboard.GetContent();
            if (clipboard.Contains(StandardDataFormats.Text))
            {
                var text = await clipboard.GetTextAsync();
                if (ViewModel.TryParseUrl(text))
                {
                    return;
                }

                textBox.Text = text;
            }
        }
    }
}
