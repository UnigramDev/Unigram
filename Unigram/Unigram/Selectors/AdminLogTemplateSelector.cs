using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.TL;
using Unigram.Common;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Unigram.Selectors
{
    public class AdminLogTemplateSelector : DataTemplateSelector
    {
        protected DataTemplate EmptyMessageTemplate = new DataTemplate();

        public DataTemplate MessageTemplate { get; set; }
        public DataTemplate EditedTemplate { get; set; }

        public DataTemplate EventMessageTemplate { get; set; }
        public DataTemplate EventMessagePhotoTemplate { get; set; }

        public DataTemplate ServiceMessageTemplate { get; set; }
        public DataTemplate ServiceMessagePhotoTemplate { get; set; }

        protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
        {
            if (item is TLMessageService serviceMessage)
            {
                if (serviceMessage.Action is TLMessageActionAdminLogEvent adminLog)
                {
                    if (adminLog.Event.Action is TLChannelAdminLogEventActionChangePhoto changePhotoAction)
                    {
                        if (changePhotoAction.NewPhoto is TLChatPhotoEmpty)
                        {
                            return EventMessageTemplate;
                        }

                        return EventMessagePhotoTemplate;
                    }
                    //else if (adminLog.Event.Action is TLChannelAdminLogEventActionEditMessage)
                    //{
                    //    return EditedTemplate;
                    //}

                    return EventMessageTemplate;
                }
                else if (serviceMessage.Action is TLMessageActionChatEditPhoto)
                {
                    return ServiceMessagePhotoTemplate;
                }

                return ServiceMessageTemplate;
            }
            else if (item is TLMessage message)
            {
                if (message.Reply != null)
                {
                    return EditedTemplate;
                }

                return MessageTemplate;
            }

            return EmptyMessageTemplate;
        }
    }
}
