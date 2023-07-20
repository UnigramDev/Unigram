//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using Telegram.ViewModels.Drawers;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Telegram.Selectors
{
    public class StickerSetTemplateSelector : DataTemplateSelector
    {
        public DataTemplate GroupTemplate { get; set; }
        public DataTemplate RecentsTemplate { get; set; }
        public DataTemplate TrendingTemplate { get; set; }
        public DataTemplate FavedTemplate { get; set; }
        public DataTemplate ItemTemplate { get; set; }

        protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
        {
            if (item is StickerSetViewModel stickerSet)
            {
                if (string.Equals(stickerSet.Name, "tg/recentlyUsed", StringComparison.OrdinalIgnoreCase))
                {
                    return RecentsTemplate ?? ItemTemplate;
                }
                else if (string.Equals(stickerSet.Name, "tg/favedStickers", StringComparison.OrdinalIgnoreCase))
                {
                    return FavedTemplate ?? ItemTemplate;
                }
                else if (string.Equals(stickerSet.Name, "tg/groupStickers", StringComparison.OrdinalIgnoreCase))
                {
                    return GroupTemplate ?? ItemTemplate;
                }

                return ItemTemplate;
            }
            else if (item is AnimationsCollection animations)
            {
                if (string.Equals(animations.Name, "tg/recentlyUsed", StringComparison.OrdinalIgnoreCase))
                {
                    return RecentsTemplate ?? ItemTemplate;
                }
                else if (string.Equals(animations.Name, "tg/trending", StringComparison.OrdinalIgnoreCase))
                {
                    return TrendingTemplate ?? ItemTemplate;
                }
            }

            return ItemTemplate;
        }
    }
}
