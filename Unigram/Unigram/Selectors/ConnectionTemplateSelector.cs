using Unigram.ViewModels.Settings;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Unigram.Selectors
{
    public class ConnectionTemplateSelector : DataTemplateSelector
    {
        public DataTemplate ItemTemplate { get; set; }
        public DataTemplate ProxyTemplate { get; set; }

        protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
        {
            if (item is ProxyViewModel)
            {
                return ProxyTemplate;
            }
            else if (item is ConnectionViewModel)
            {
                return ItemTemplate;
            }

            return base.SelectTemplateCore(item, container);
        }
    }
}
