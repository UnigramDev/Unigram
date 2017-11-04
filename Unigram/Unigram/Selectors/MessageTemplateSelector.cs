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
    public class MessageTemplateSelector : DataTemplateSelector
    {
        public Dictionary<long, GroupedMessages> GroupedItems { get; set; }

        private readonly Dictionary<Type, Func<TLMessageBase, DataTemplate>> _templatesCache;

        protected DataTemplate EmptyMessageTemplate = new DataTemplate();

        public DataTemplate UserMessageTemplate { get; set; }
        public DataTemplate FriendMessageTemplate { get; set; }
        public DataTemplate ChatFriendMessageTemplate { get; set; }

        public DataTemplate ServiceUserCallTemplate { get; set; }
        public DataTemplate ServiceFriendCallTemplate { get; set; }

        public DataTemplate ServiceMessageTemplate { get; set; }
        public DataTemplate ServiceMessagePhotoTemplate { get; set; }
        public DataTemplate ServiceMessageLocalTemplate { get; set; }
        public DataTemplate ServiceMessageDateTemplate { get; set; }

        public DataTemplate GroupedPhotoTemplate { get; set; }
        public DataTemplate GroupedVideoTemplate { get; set; }

        public MessageTemplateSelector()
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

            if (serviceMessage.Action is TLMessageActionChatEditPhoto)
            {
                return ServiceMessagePhotoTemplate;
            }
            else if (serviceMessage.Action is TLMessageActionHistoryClear)
            {
                return EmptyMessageTemplate;
            }
            else if (serviceMessage.Action is TLMessageActionDate)
            {
                return ServiceMessageDateTemplate;
            }
            else if (serviceMessage.Action is TLMessageActionUnreadMessages)
            {
                //return ServiceMessageUnreadTemplate;
                return ServiceMessageLocalTemplate;
            }
            else if (serviceMessage.Action is TLMessageActionPhoneCall)
            {
                return serviceMessage.IsOut ? ServiceUserCallTemplate : ServiceFriendCallTemplate;
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

            if (message.HasGroupedId && message.GroupedId is long groupedId && GroupedItems != null && GroupedItems.TryGetValue(groupedId, out GroupedMessages group) && group.Messages.Count > 1)
            {
                return message.Media is TLMessageMediaPhoto ? GroupedPhotoTemplate : GroupedVideoTemplate;
            }

            if (message.Media is TLMessageMediaPhoto photoMedia && photoMedia.HasTTLSeconds && (photoMedia.Photo is TLPhotoEmpty || !photoMedia.HasPhoto))
            {
                return ServiceMessageTemplate;
            }
            else if (message.Media is TLMessageMediaDocument documentMedia && documentMedia.HasTTLSeconds && (documentMedia.Document is TLDocumentEmpty || !documentMedia.HasDocument))
            {
                return ServiceMessageTemplate;
            }

            if (message.IsSaved())
            {
                return ChatFriendMessageTemplate;
            }

            if (message.IsOut && !message.IsPost)
            {
                return UserMessageTemplate;
            }
            else if (message.ToId is TLPeerChat || (message.ToId is TLPeerChannel && !message.IsPost))
            {
                return ChatFriendMessageTemplate;
            }

            return FriendMessageTemplate;
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
