using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.TL;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Unigram.Selectors
{
    public class MessageTemplateSelector : DataTemplateSelector
    {
        private readonly Dictionary<Type, Func<TLMessageBase, DataTemplate>> _templatesCache;

        protected DataTemplate EmptyMessageTemplate = new DataTemplate();

        public DataTemplate UserMessageTemplate { get; set; }
        public DataTemplate FriendMessageTemplate { get; set; }
        public DataTemplate ChatFriendMessageTemplate { get; set; }

        public DataTemplate UserForwardTemplate { get; set; }
        public DataTemplate FriendForwardTemplate { get; set; }
        public DataTemplate ChatFriendForwardTemplate { get; set; }

        public DataTemplate UserMediaTemplate { get; set; }
        public DataTemplate FriendMediaTemplate { get; set; }
        public DataTemplate ChatFriendMediaTemplate { get; set; }

        public DataTemplate UserStickerTemplate { get; set; }
        public DataTemplate FriendStickerTemplate { get; set; }
        public DataTemplate ChatFriendStickerTemplate { get; set; }

        public DataTemplate ServiceMessageTemplate { get; set; }
        public DataTemplate ServiceMessagePhotoTemplate { get; set; }

        public DataTemplate UnreadMessagesTemplate { get; set; }

        public MessageTemplateSelector()
        {
            _templatesCache = new Dictionary<Type, Func<TLMessageBase, DataTemplate>>();
            _templatesCache.Add(typeof(TLMessageService), new Func<TLMessageBase, DataTemplate>(GenerateServiceMessageTemplate));
            _templatesCache.Add(typeof(TLMessageEmpty), (TLMessageBase m) => EmptyMessageTemplate);
            _templatesCache.Add(typeof(TLMessage), new Func<TLMessageBase, DataTemplate>(GenerateCommonMessageTemplate));
            //_templatesCache.Add(typeof(TLMessageForwarded), new Func<TLMessageBase, DataTemplate>(GenerateCommonMessageTemplate));
        }

        private DataTemplate GenerateServiceMessageTemplate(TLMessageBase message)
        {
            var serviceMessage = message as TLMessageService;
            if (serviceMessage == null)
            {
                return EmptyMessageTemplate;
            }

            // TODO: actually this is a local TL, and at this time we don't support it.
            //if (!(tLMessageService.Action is TLMessageActionUnreadMessages))
            //{
            //    return ServiceMessageTemplate ?? EmptyMessageTemplate;
            //}

            if (serviceMessage.Action is TLMessageActionChatEditPhoto)
            {
                return ServiceMessagePhotoTemplate;
            }

            return ServiceMessageTemplate ?? EmptyMessageTemplate;
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
                if (message.IsOut)
                {
                    return UserStickerTemplate ?? EmptyMessageTemplate;
                }
                if (message.ToId is TLPeerChat)
                {
                    return ChatFriendStickerTemplate ?? EmptyMessageTemplate;
                }

                return FriendStickerTemplate ?? EmptyMessageTemplate;
            }
            else
            {
                if (message.IsOut)
                {
                    if (!(message?.Media is TLMessageMediaEmpty))
                    {
                        return UserMediaTemplate ?? UserMessageTemplate;
                    }

                    //if (message25?.FwdFromId != null)
                    //{
                    //    return UserForwardTemplate ?? UserMessageTemplate;
                    //}

                    return UserMessageTemplate ?? EmptyMessageTemplate;
                }
                if (message.ToId is TLPeerChat || message.ToId is TLPeerChannel) // TODO: probably some addtional check needed for channels
                {
                    if (!(message?.Media is TLMessageMediaEmpty))
                    {
                        return ChatFriendMediaTemplate ?? ChatFriendMessageTemplate;
                    }

                    //if (message25?.FwdFromId != null)
                    //{
                    //    return ChatFriendForwardTemplate ?? ChatFriendMessageTemplate;
                    //}

                    return ChatFriendMessageTemplate ?? EmptyMessageTemplate;
                }

                if (!(message?.Media is TLMessageMediaEmpty))
                {
                    return FriendMediaTemplate ?? FriendMessageTemplate;
                }

                //if (message25?.FwdFromId != null)
                //{
                //    return FriendForwardTemplate ?? FriendMessageTemplate;
                //}

                return FriendMessageTemplate ?? EmptyMessageTemplate;
            }
        }

        protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
        {
            var message = item as TLMessageBase;
            if (message == null)
            {
                return EmptyMessageTemplate;
            }

            Func<TLMessageBase, DataTemplate> func;
            if (_templatesCache.TryGetValue(message.GetType(), out func))
            {
                return func.Invoke(message);
            }

            return EmptyMessageTemplate;
        }
    }
}
