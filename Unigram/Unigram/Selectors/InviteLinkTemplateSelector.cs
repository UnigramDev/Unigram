//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
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
