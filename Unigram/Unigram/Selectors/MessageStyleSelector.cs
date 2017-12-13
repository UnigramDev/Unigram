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
    public class MessageStyleSelector : StyleSelector
    {
        public Style MessageStyle { get; set; }
        public Style ServiceStyle { get; set; }

        protected override Style SelectStyleCore(object item, DependencyObject container)
        {
            if (item is TLMessageService serviceMessage && !(serviceMessage.Action is TLMessageActionPhoneCall))
            {
                return ServiceStyle;
            }

            var message = item as TLMessage;
            if (message != null)
            {
                if (message.IsService())
                {
                    return ServiceStyle;
                }

                //if (message.IsOut)
                //{
                //    return MessageStyle;
                //}

                //if (message.ToId is TLPeerChat || (message.ToId is TLPeerChannel && !message.IsPost))
                //{
                //    return ChatFriendMessageStyle;
                //}
            }

            return MessageStyle;
        }
    }
}
