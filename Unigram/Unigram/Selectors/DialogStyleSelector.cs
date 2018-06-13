using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Services;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Unigram.Selectors
{
    public class DialogStyleSelector : StyleSelector
    {
        public Style DialogStyle { get; set; }

        public Style PinnedStyle { get; set; }

        public ICacheService CacheService { get; set; }

        protected override Style SelectStyleCore(object item, DependencyObject container)
        {
            var chat = item as Chat;
            if (chat != null)
            {
                return chat.IsPinned || (CacheService != null && CacheService.IsChatPromoted(chat)) ? PinnedStyle : DialogStyle;
            }

            return base.SelectStyleCore(item, container);
        }
    }
}
