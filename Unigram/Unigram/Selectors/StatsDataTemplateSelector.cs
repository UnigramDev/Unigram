using Telegram.Td.Api;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

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
                case NetworkStatisticsEntryCall call:
                    return CallTemplate;
                case NetworkStatisticsEntryFile data:
                    return FileTemplate;
            }

            return base.SelectTemplateCore(item, container);
        }
    }
}
