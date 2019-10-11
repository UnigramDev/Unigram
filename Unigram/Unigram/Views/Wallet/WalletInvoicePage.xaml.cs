using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
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
    public sealed partial class WalletInvoicePage : Page
    {
        public WalletInvoiceViewModel ViewModel => DataContext as WalletInvoiceViewModel;

        public WalletInvoicePage()
        {
            InitializeComponent();
            DataContext = TLContainer.Current.Resolve<WalletInvoiceViewModel>();
        }

        #region Binding

        private string ConvertAmount(long value)
        {
            if (value > 0)
            {
                return BindConvert.Grams(value, false);
            }

            return string.Empty;
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

        private string ConvertUrl(string address, long amount, string text)
        {
            var query = string.Empty;

            if (amount > 0)
            {
                query = $"?amount={amount}";
            }

            if (!string.IsNullOrWhiteSpace(text))
            {
                if (query.Length > 0)
                {
                    query += "&";
                }
                else
                {
                    query += "?";
                }

                query += $"text={text}";
            }

            return $"ton://transfer/{address}{query}";
        }

        #endregion

    }
}
