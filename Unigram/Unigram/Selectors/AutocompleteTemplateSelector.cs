using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.TL;
using Unigram.Native;
using Unigram.ViewModels;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Unigram.Selectors
{
    public class AutocompleteTemplateSelector : DataTemplateSelector
    {
        public DataTemplate MentionTemplate { get; set; }
        public DataTemplate CommandTemplate { get; set; }
        public DataTemplate HashtagTemplate { get; set; }
        public DataTemplate EmojiTemplate { get; set; }

        protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
        {
            if (item is TLUser)
            {
                return MentionTemplate;
            }
            else if (item is TLUserCommand)
            {
                return CommandTemplate;
            }
            else if (item is EmojiSuggestion)
            {
                return EmojiTemplate;
            }

            return base.SelectTemplateCore(item, container);
        }
    }
}
