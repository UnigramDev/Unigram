﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Telegram.Api.TL
{
    public class TLMessageContainer : TLObject
    {
        public List<TLContainerTransportMessage> Messages { get; set; }

        public TLMessageContainer() { }
        public TLMessageContainer(TLBinaryReader from, TLType type = TLType.MsgContainer)
        {
            Read(from, type);
        }

        public override TLType TypeId { get { return TLType.MsgContainer; } }

        public override void Read(TLBinaryReader from, TLType type = TLType.MsgContainer)
        {
            Messages = new List<TLContainerTransportMessage>();

            var count = from.ReadUInt32();
            for (int i = 0; i < count; i++)
            {
                Messages.Add(new TLContainerTransportMessage(from));
            }
        }

        public override void Write(TLBinaryWriter to)
        {
            to.Write(0x73F1F8DC);
            to.Write(Messages.Count);
            
            foreach (var item in Messages)
            {
                item.Write(to);
            }
        }
    }
}
