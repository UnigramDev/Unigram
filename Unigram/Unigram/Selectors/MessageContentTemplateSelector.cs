using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
        public DataTemplate DocumentTemplate { get; set; }
        public DataTemplate HeaderDateTemplate { get; set; }

        protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
        {
            if (item is Message message)
            {
                switch (message.Content)
                {
                    case MessagePhoto photo:
                        return PhotoTemplate;
                    case MessageVideo video:
                        return VideoTemplate;
                    case MessageText text:
                        return TextTemplate;
                    case MessageHeaderDate headerDate:
                        return HeaderDateTemplate;
                    default:
                        return DocumentTemplate;
                }
            }
            else if (item is MessageViewModel viewModel)
            {
                switch (viewModel.Content)
                {
                    case MessagePhoto photo:
                        return PhotoTemplate;
                    case MessageVideo video:
                        return VideoTemplate;
                    case MessageText text:
                        return TextTemplate;
                    case MessageHeaderDate headerDate:
                        return HeaderDateTemplate;
                    default:
                        return DocumentTemplate;
                }
            }

            return base.SelectTemplateCore(item, container);
        }
    }
}
