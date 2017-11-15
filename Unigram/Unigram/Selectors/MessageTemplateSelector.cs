﻿using System;
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

        protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
        {
            var messageBase = item as TLMessageBase;
            if (messageBase == null || messageBase is TLMessageEmpty)
            {
                return EmptyMessageTemplate;
            }
            else if (messageBase is TLMessage message)
            {
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
            else if (messageBase is TLMessageService serviceMessage)
            {
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

            return EmptyMessageTemplate;
        }
    }
}
