//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Unigram.Selectors
{
    public class StickerTemplateSelector : DataTemplateSelector
    {
        public DataTemplate ItemTemplate { get; set; }
        public DataTemplate AnimatedTemplate { get; set; }

        protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
        {
            if (item is ViewModels.Dialogs.StickerViewModel sticker)
            {
                if (sticker.IsAnimated)
                {
                    return AnimatedTemplate;
                }

                return ItemTemplate;
            }

            return base.SelectTemplateCore(item, container);
        }
    }
}
