using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Unigram.Collections;

namespace Unigram.Selectors
{
    public class InviteLinkTemplateSelector : DataTemplateSelector
    {
        public DataTemplate ItemTemplate { get; set; }
        public DataTemplate GroupTemplate { get; set; }

        protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
        {
            if (item is CollectionSeparator)
            {
                return GroupTemplate;
            }

            return ItemTemplate;

            return base.SelectTemplateCore(item, container);
        }
    }
}
