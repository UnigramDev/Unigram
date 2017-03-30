using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.TL;
using Windows.UI.Xaml.Data;

namespace Unigram.Converters
{
    public class InvoiceToAmountConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is TLMessageMediaInvoice invoiceMedia)
            {
                return BindConvert.Current.FormatAmount(invoiceMedia.TotalAmount, invoiceMedia.Currency);
            }

            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
