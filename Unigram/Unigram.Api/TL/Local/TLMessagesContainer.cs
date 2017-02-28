using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Telegram.Api.TL;

namespace Telegram.Api.TL
{
    // TODO: find a better name, this is confusing with TLMessageContainer :P
    public class TLMessagesContainter : TLMessageBase
    {
        public const uint Signature = 4294967058u;

        public int EditUntil
        {
            get;
            set;
        }

        public new TLObject From
        {
            get
            {
                if (FwdMessages != null && FwdMessages.Count > 0)
                {
                    var message48 = FwdMessages[0] as TLMessage;
                    if (message48 != null)
                    {
                        var fwdHeader = message48.FwdFrom;
                        if (fwdHeader != null)
                        {
                            if (fwdHeader.HasChannelId)
                            {
                                return InMemoryCacheService.Current.GetChat(fwdHeader.ChannelId.Value);
                            }
                            if (fwdHeader.HasFromId)
                            {
                                return InMemoryCacheService.Current.GetUser(fwdHeader.FromId.Value);
                            }
                        }
                    }

                    return FwdMessages[0].FwdFrom;
                }

                if (EditMessage != null)
                {
                    var message = EditMessage as TLMessage;
                    if (message != null)
                    {
                        var fwdHeader = message.FwdFrom;
                        if (fwdHeader != null)
                        {
                            if (fwdHeader.HasChannelId)
                            {
                                return InMemoryCacheService.Current.GetChat(fwdHeader.ChannelId.Value);
                            }
                            if (fwdHeader.HasFromId)
                            {
                                return InMemoryCacheService.Current.GetUser(fwdHeader.FromId.Value);
                            }
                        }
                    }
                    return EditMessage.From;
                }
                return null;
            }
        }

        public TLVector<TLMessage> FwdMessages { get; set; }

        public TLMessageMediaBase Media
        {
            get
            {
                if (FwdMessages != null && FwdMessages.Count > 0)
                {
                    return FwdMessages[0].Media;
                }
                if (EditMessage != null)
                {
                    return EditMessage.Media;
                }
                return null;
            }
        }

        public string Message
        {
            get
            {
                if (FwdMessages != null && FwdMessages.Count > 0)
                {
                    return FwdMessages[0].Message;
                }
                if (EditMessage != null)
                {
                    return EditMessage.Message;
                }
                return null;
            }
        }

        public TLMessageMediaBase WebPageMedia { get; set; }

        public TLMessage EditMessage { get; set; }
    }
}
