using Telegram.Td.Api;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Unigram.Selectors
{
    public class ChatStatisticsOverviewTemplateSelector : DataTemplateSelector
    {
        public DataTemplate ChannelTemplate { get; set; }
        public DataTemplate SupergroupTemplate { get; set; }

        protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
        {
            if (item is ChatStatisticsChannel)
            {
                return ChannelTemplate;
            }

            return SupergroupTemplate;
        }
    }
}
