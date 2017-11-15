using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.TL;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Unigram.Selectors
{
    public class DialogTemplateSelector : DataTemplateSelector
    {
        public DataTemplate SelfTemplate { get; set; }
        public DataTemplate ItemTemplate { get; set; }

        protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
        {
            if (item is TLDialog dialog && dialog.With is TLUser inner)
            {
                return inner.IsSelf ? SelfTemplate ?? ItemTemplate : ItemTemplate;
            }

            if (item is TLUser user)
            {
                return user.IsSelf ? SelfTemplate ?? ItemTemplate : ItemTemplate;
            }

            return ItemTemplate;
        }
    }
}
