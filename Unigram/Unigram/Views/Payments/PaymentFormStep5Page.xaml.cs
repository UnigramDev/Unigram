using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Telegram.Api.Helpers;
using Telegram.Api.TL;
using Unigram.Common;
using Unigram.Converters;
using Unigram.ViewModels.Payments;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace Unigram.Views.Payments
{
    public sealed partial class PaymentFormStep5Page : Page
    {
        public PaymentFormStep5ViewModel ViewModel => DataContext as PaymentFormStep5ViewModel;

        public BindConvert Convert => BindConvert.Current;

        public PaymentFormStep5Page()
        {
            InitializeComponent();
            DataContext = UnigramContainer.Current.ResolveType<PaymentFormStep5ViewModel>();
        }

        private string ConvertTitle(bool test)
        {
            return (test ? "Test " : string.Empty) +  Strings.Android.PaymentCheckout;
        }

        private string ConvertAddress(TLPostAddress address)
        {
            if (address == null)
            {
                return string.Empty;
            }

            var result = string.Empty;
            if (!string.IsNullOrEmpty(address.StreetLine1))
            {
                result += address.StreetLine1 + ", ";
            }
            if (!string.IsNullOrEmpty(address.StreetLine2))
            {
                result += address.StreetLine2 + ", ";
            }
            if (!string.IsNullOrEmpty(address.City))
            {
                result += address.City + ", ";
            }
            if (!string.IsNullOrEmpty(address.State))
            {
                result += address.State + ", ";
            }
            if (!string.IsNullOrEmpty(address.CountryIso2))
            {
                result += address.CountryIso2 + ", ";
            }
            if (!string.IsNullOrEmpty(address.PostCode))
            {
                result += address.PostCode + ", ";
            }

            return result.Trim(',', ' ');
        }

        private string ConvertPay(long amount, string currency)
        {
            return string.Format(Strings.Android.PaymentCheckoutPay, LocaleHelper.FormatCurrency(amount, currency));
        }
    }
}
