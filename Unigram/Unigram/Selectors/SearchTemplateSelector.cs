using Telegram.Td.Api;
using Unigram.ViewModels;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Unigram.Selectors
{
    public class SearchTemplateSelector : DataTemplateSelector
    {
        public DataTemplate ChatTemplate { get; set; }
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
