using Telegram.Td.Api;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

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
