//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
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
