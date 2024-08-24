//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Telegram.Entities;

namespace Telegram.Selectors
{
    public class StorageMediaTemplateSelector : DataTemplateSelector
    {
        public DataTemplate PhotoTemplate { get; set; }
        public DataTemplate VideoTemplate { get; set; }

        protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
        {
            if (item is StoragePhoto)
            {
                return PhotoTemplate;
            }
            else if (item is StorageVideo)
            {
                return VideoTemplate;
            }

            return base.SelectTemplateCore(item, container);
        }
    }
}
