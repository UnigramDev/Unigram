using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Telegram.Td.Api;

namespace Unigram.Selectors
{
    public class StorageStatisticsTemplateSelector : DataTemplateSelector
    {
        public DataTemplate ItemTemplate { get; set; }
        public DataTemplate OtherTemplate { get; set; }

        protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
        {
            if (item is StorageStatisticsByChat statistics)
            {
                return statistics.ChatId == 0 ? OtherTemplate : ItemTemplate;
            }

            return base.SelectTemplateCore(item, container);
        }
    }
}
