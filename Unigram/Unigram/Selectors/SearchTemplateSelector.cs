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
    public class SearchTemplateSelector : DataTemplateSelector
    {
        public DataTemplate UserTemplate { get; set; }

        public DataTemplate ChatTemplate { get; set; }

        public DataTemplate ChannelTemplate { get; set; }

        public DataTemplate ChannelForbiddenTemplate { get; set; }

        public DataTemplate MessageTemplate { get; set; }

        protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
        {
            if (item is TLUser user)
            {
                return UserTemplate;
            }

            if (item is TLChat chat)
            {
                return ChatTemplate;
            }

            if (item is TLChannel channel)
            {
                return ChannelTemplate;
            }

            if (item is TLChannelForbidden channelForbidden)
            {
                return ChannelForbiddenTemplate;
            }

            if (item is TLDialog dialog)
            {
                return MessageTemplate;
            }

            return base.SelectTemplateCore(item, container);
        }
    }
}
