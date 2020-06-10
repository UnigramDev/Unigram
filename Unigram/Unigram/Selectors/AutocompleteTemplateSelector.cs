using Telegram.Td.Api;
using Unigram.Common;
using Unigram.ViewModels;
using Unigram.ViewModels.Drawers;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Unigram.Selectors
{
    public class AutocompleteTemplateSelector : DataTemplateSelector
    {
        public DataTemplate MentionTemplate { get; set; }
        public DataTemplate CommandTemplate { get; set; }
        public DataTemplate HashtagTemplate { get; set; }
        public DataTemplate StickerTemplate { get; set; }
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
            else if (item is Sticker || item is StickerViewModel)
            {
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
