using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Unigram.ViewModels;

namespace Unigram.Selectors
{
    public class MessageStyleSelector : StyleSelector
    {
        public Style MessageStyle { get; set; }
        public Style ServiceStyle { get; set; }

        protected override Style SelectStyleCore(object item, DependencyObject container)
        {
            if (item is MessageViewModel message && message.IsService())
            {
                return ServiceStyle;
            }

            return MessageStyle;
        }
    }
}
