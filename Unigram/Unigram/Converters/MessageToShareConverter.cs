using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.TL;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Data;

namespace Unigram.Converters
{
    public class MessageToShareConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            var message = value as TLMessage;
            if (message == null)
            {
                return Visibility.Collapsed;
            }

            return Convert(message) ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }

        public static bool Convert(TLMessage message)
        {
            if (message.IsSticker())
            {
                return false;
            }
            else if (message.HasFwdFrom && message.FwdFrom.HasChannelId && !message.IsOut)
            {
                return true;
            }
            else if (message.HasFromId && !message.IsPost)
            {
                if (message.Media is TLMessageMediaEmpty || message.Media == null || message.Media is TLMessageMediaWebPage webpageMedia && !(webpageMedia.WebPage is TLWebPage))
                {
                    return false;
                }

                var user = message.From;
                if (user != null && user.IsBot)
                {
                    return true;
                }

                if (!message.IsOut)
                {
                    if (message.Media is TLMessageMediaGame || message.Media is TLMessageMediaInvoice)
                    {
                        return true;
                    }

                    var parent = message.Parent as TLChannel;
                    if (parent != null && parent.IsMegaGroup)
                    {
                        //TLRPC.Chat chat = MessagesController.getInstance().getChat(messageObject.messageOwner.to_id.channel_id);
                        //return chat != null && chat.username != null && chat.username.length() > 0 && !(messageObject.messageOwner.media instanceof TLRPC.TL_messageMediaContact) && !(messageObject.messageOwner.media instanceof TLRPC.TL_messageMediaGeo);

                        return parent.HasUsername && !(message.Media is TLMessageMediaContact) && !(message.Media is TLMessageMediaGeo);
                    }
                }
            }
            //else if (messageObject.messageOwner.from_id < 0 || messageObject.messageOwner.post)
            else if (message.IsPost)
            {
                //if (messageObject.messageOwner.to_id.channel_id != 0 && (messageObject.messageOwner.via_bot_id == 0 && messageObject.messageOwner.reply_to_msg_id == 0 || messageObject.type != 13))
                //{
                //    return Visibility.Visible;
                //}

                if (message.ToId is TLPeerChannel && (!message.HasViaBotId && !message.HasReplyToMsgId))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
