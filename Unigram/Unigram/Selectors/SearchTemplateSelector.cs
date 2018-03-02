using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TdWindows;
using Unigram.ViewModels;
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
            if (item is SearchResult)
            {
                return ChatTemplate;
            }

            if (item is Message)
            {
                return MessageTemplate;
            }

            return base.SelectTemplateCore(item, container);
        }
    }
}
