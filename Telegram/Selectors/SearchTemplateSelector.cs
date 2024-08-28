//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Telegram.Collections;
using Telegram.Td.Api;
using Telegram.ViewModels;

namespace Telegram.Selectors
{
    public partial class SearchTemplateSelector : DataTemplateSelector
    {
        public DataTemplate HeaderTemplate { get; set; }
        public DataTemplate ChatTemplate { get; set; }
        public DataTemplate MessageTemplate { get; set; }

        protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
        {
            if (item is IKeyedCollection)
            {
                return HeaderTemplate;
            }
            else if (item is SearchResult)
            {
                return ChatTemplate;
            }
            else if (item is Message)
            {
                return MessageTemplate;
            }

            return base.SelectTemplateCore(item, container);
        }
    }
}
