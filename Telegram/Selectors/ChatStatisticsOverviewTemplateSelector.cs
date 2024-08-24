//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Telegram.Td.Api;

namespace Telegram.Selectors
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
