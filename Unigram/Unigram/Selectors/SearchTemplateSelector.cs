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

        public DataTemplate ChannelTemplate { get; set; }

        public DataTemplate ChannelForbiddenTemplate { get; set; }

        public DataTemplate MessageTemplate { get; set; }

        protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
        {
            var user = item as TLUser;
            if (user != null)
            {
                return UserTemplate;
            }

            var channel = item as TLChannel;
            if (channel != null)
            {
                return ChannelTemplate;
            }

            var channelForbidden = item as TLChannelForbidden;
            if( channelForbidden != null)
            {
                return ChannelForbiddenTemplate;
            }

            var dialog = item as TLDialog;
            if (dialog != null)
            {
                return MessageTemplate;
            }

            return base.SelectTemplateCore(item, container);
        }
    }
}
