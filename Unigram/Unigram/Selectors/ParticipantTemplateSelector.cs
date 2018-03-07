using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Unigram.Selectors
{
    public class ParticipantTemplateSelector : DataTemplateSelector
    {
        public DataTemplate ItemTemplate { get; set; }
        public DataTemplate AdminTemplate { get; set; }
        public DataTemplate CreatorTemplate { get; set; }

        protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
        {
            var member = item as ChatMember;
            if (member == null)
            {
                return base.SelectTemplateCore(item, container);
            }

            switch (member.Status)
            {
                case ChatMemberStatusCreator creator:
                    return CreatorTemplate ?? AdminTemplate ?? ItemTemplate;
                case ChatMemberStatusAdministrator administrator:
                    return AdminTemplate ?? ItemTemplate;
                default:
                    return ItemTemplate;
            }
        }
    }
}
