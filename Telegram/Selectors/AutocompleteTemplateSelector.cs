//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Telegram.Common;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Telegram.ViewModels.Drawers;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Telegram.Selectors
{
    public class AutocompleteTemplateSelector : DataTemplateSelector
    {
        public DataTemplate MentionTemplate { get; set; }
        public DataTemplate CommandTemplate { get; set; }
        public DataTemplate HashtagTemplate { get; set; }
        public DataTemplate StickerTemplate { get; set; }
        public DataTemplate CustomEmojiTemplate { get; set; }
        public DataTemplate EmojiTemplate { get; set; }
        public DataTemplate ItemTemplate { get; set; }

        protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
        {
            if (item is User)
            {
                return MentionTemplate;
            }
            else if (item is UserCommand)
            {
                return CommandTemplate;
            }
            else if (item is Sticker sticker)
            {
                if (sticker.FullType is StickerFullTypeCustomEmoji)
                {
                    return CustomEmojiTemplate;
                }

                return StickerTemplate;
            }
            else if (item is StickerViewModel stickerViewModel)
            {
                if (stickerViewModel.FullType is StickerFullTypeCustomEmoji)
                {
                    return CustomEmojiTemplate;
                }

                return StickerTemplate;
            }
            else if (item is EmojiData)
            {
                return EmojiTemplate;
            }

            return ItemTemplate ?? base.SelectTemplateCore(item, container);
        }
    }
}
