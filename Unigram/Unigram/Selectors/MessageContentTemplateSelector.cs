using Telegram.Td.Api;
using Unigram.ViewModels;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Unigram.Selectors
{
    public class MessageContentTemplateSelector : DataTemplateSelector
    {
        public DataTemplate PhotoTemplate { get; set; }
        public DataTemplate VideoTemplate { get; set; }
        public DataTemplate TextTemplate { get; set; }
        public DataTemplate MessageTemplate { get; set; }
        public DataTemplate HeaderDateTemplate { get; set; }

        protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
        {
            if (item is Message message)
            {
                switch (message.Content)
                {
                    case MessagePhoto photo:
                        return MessageTemplate ?? PhotoTemplate;
                    case MessageVideo video:
                        return MessageTemplate ?? VideoTemplate;
                    case MessageText text:
                        return MessageTemplate ?? TextTemplate;
                    case MessageHeaderDate headerDate:
                        return HeaderDateTemplate;
                    default:
                        return MessageTemplate;
                }
            }
            else if (item is MessageViewModel viewModel)
            {
                switch (viewModel.Content)
                {
                    case MessagePhoto photo:
                        return MessageTemplate ?? PhotoTemplate;
                    case MessageVideo video:
                        return MessageTemplate ?? VideoTemplate;
                    case MessageText text:
                        return MessageTemplate ?? TextTemplate;
                    case MessageHeaderDate headerDate:
                        return HeaderDateTemplate;
                    default:
                        return MessageTemplate;
                }
            }

            return base.SelectTemplateCore(item, container);
        }
    }
}
