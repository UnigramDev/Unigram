using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Td.Api;
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
            var chat = item as Chat;
            if (chat != null)
            {
                return chat.IsPinned ? PinnedStyle : DialogStyle;
            }

            return base.SelectStyleCore(item, container);
        }
    }
}
