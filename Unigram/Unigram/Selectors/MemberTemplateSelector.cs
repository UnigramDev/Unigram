using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Telegram.Td.Api;

namespace Unigram.Selectors
{
    public class MemberTemplateSelector : DataTemplateSelector
    {
        public DataTemplate ItemTemplate { get; set; }
        public DataTemplate AdminTemplate { get; set; }
        public DataTemplate CreatorTemplate { get; set; }
        public DataTemplate GroupTemplate { get; set; }

        protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
        {
            return SelectTemplateCore(item);
        }

        protected override DataTemplate SelectTemplateCore(object item)
        {
            var member = item as ChatMember;
            if (member == null)
            {
                return GroupTemplate;
            }

            switch (member.Status)
            {
                case ChatMemberStatusCreator:
                    return CreatorTemplate ?? AdminTemplate ?? ItemTemplate;
                case ChatMemberStatusAdministrator:
                    return AdminTemplate ?? ItemTemplate;
                default:
                    return ItemTemplate;
            }
        }
    }
}
