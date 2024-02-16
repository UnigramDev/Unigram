//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Telegram.Td.Api;
using Telegram.ViewModels;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Telegram.Selectors
{
    public class MessageContentTemplateSelector : DataTemplateSelector
    {
        public DataTemplate PhotoTemplate { get; set; }
        public DataTemplate VideoTemplate { get; set; }
        public DataTemplate TextTemplate { get; set; }
        public DataTemplate AnimationTemplate { get; set; }
        public DataTemplate MessageTemplate { get; set; }
        public DataTemplate HeaderDateTemplate { get; set; }

        protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
        {
            if (item is Message message)
            {
                switch (message.Content)
                {
                    case MessagePhoto:
                        return MessageTemplate ?? PhotoTemplate;
                    case MessageVideo:
                        return MessageTemplate ?? VideoTemplate;
                    case MessageText:
                        return MessageTemplate ?? TextTemplate;
                    case MessageAnimation:
                        return MessageTemplate ?? AnimationTemplate;
                    case MessageHeaderDate:
                        return HeaderDateTemplate;
                    default:
                        return MessageTemplate;
                }
            }
            else if (item is MessageWithOwner viewModel)
            {
                switch (viewModel.Content)
                {
                    case MessagePhoto:
                        return MessageTemplate ?? PhotoTemplate;
                    case MessageVideo:
                        return MessageTemplate ?? VideoTemplate;
                    case MessageText:
                        return MessageTemplate ?? TextTemplate;
                    case MessageAnimation:
                        return MessageTemplate ?? AnimationTemplate;
                    case MessageHeaderDate:
                        return HeaderDateTemplate;
                    default:
                        return MessageTemplate;
                }
            }

            return base.SelectTemplateCore(item, container);
        }
    }
}
