using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Telegram.Td.Api;

namespace Unigram.Selectors
{
    public class StatsDataTemplateSelector : DataTemplateSelector
    {
        public DataTemplate FileTemplate { get; set; }
        public DataTemplate CallTemplate { get; set; }

        protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
        {
            switch (item)
            {
                case NetworkStatisticsEntryCall:
                    return CallTemplate;
                case NetworkStatisticsEntryFile:
                    return FileTemplate;
            }

            return base.SelectTemplateCore(item, container);
        }
    }
}
