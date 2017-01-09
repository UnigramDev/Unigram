using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.TL;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Unigram.Selectors
{
    public class DialogStyleSelector : StyleSelector
    {
        public Style DialogStyle { get; set; }

        public Style PinnedStyle { get; set; }

        protected override Style SelectStyleCore(object item, DependencyObject container)
        {
            var dialog = item as TLDialog;
            if (dialog != null)
            {
                return dialog.IsPinned ? PinnedStyle : DialogStyle;
            }

            return base.SelectStyleCore(item, container);
        }
    }
}
