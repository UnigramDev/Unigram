using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Ton.Tonlib.Api;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Unigram.Selectors
{
    public class RawTransactionTemplateSelector : DataTemplateSelector
    {
        public DataTemplate TransactionTemplate { get; set; }
        
        public DataTemplate DateHeaderTemplate { get; set; }

        protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
        {
            if (item is RawTransaction)
            {
                return TransactionTemplate;
            }
            else if (item is DateTime)
            {
                return DateHeaderTemplate;
            }

            return base.SelectTemplateCore(item, container);
        }
    }
}
