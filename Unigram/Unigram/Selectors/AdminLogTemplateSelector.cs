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
        private readonly Dictionary<Type, Func<TLMessageBase, DataTemplate>> _templatesCache;

        protected DataTemplate EmptyMessageTemplate = new DataTemplate();

        public DataTemplate MessageTemplate { get; set; }
        public DataTemplate EditedTemplate { get; set; }
        public DataTemplate StickerTemplate { get; set; }
        public DataTemplate RoundVideoTemplate { get; set; }

        public DataTemplate EventMessageTemplate { get; set; }
        public DataTemplate EventMessagePhotoTemplate { get; set; }

        public DataTemplate ServiceMessageTemplate { get; set; }
        public DataTemplate ServiceMessagePhotoTemplate { get; set; }

        public AdminLogTemplateSelector()
        {
            _templatesCache = new Dictionary<Type, Func<TLMessageBase, DataTemplate>>();
            _templatesCache.Add(typeof(TLMessageService), new Func<TLMessageBase, DataTemplate>(GenerateServiceMessageTemplate));
            _templatesCache.Add(typeof(TLMessageEmpty), (TLMessageBase m) => EmptyMessageTemplate);
            _templatesCache.Add(typeof(TLMessage), new Func<TLMessageBase, DataTemplate>(GenerateCommonMessageTemplate));
        }

        private DataTemplate GenerateServiceMessageTemplate(TLMessageBase message)
        {
            var serviceMessage = message as TLMessageService;
            if (serviceMessage == null)
            {
                return EmptyMessageTemplate;
            }

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

        private DataTemplate GenerateCommonMessageTemplate(TLMessageBase m)
        {
            var message = m as TLMessage;
            if (message == null)
            {
                return EmptyMessageTemplate;
            }

            if (message.IsSticker())
            {
                return StickerTemplate;
            }
            else if (message.IsRoundVideo())
            {
                return RoundVideoTemplate;
            }
            else if (message.Reply != null)
            {
                return EditedTemplate;
            }

            return MessageTemplate;
        }

        protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
        {
            var message = item as TLMessageBase;
            if (message == null)
            {
                return EmptyMessageTemplate;
            }

            if (_templatesCache.TryGetValue(message.GetType(), out Func<TLMessageBase, DataTemplate> func))
            {
                return func.Invoke(message);
            }

            return EmptyMessageTemplate;
        }
    }
}
