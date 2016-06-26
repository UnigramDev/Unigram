﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Telegram.Api.TL
{
    public class TLRPCResult : TLObject
    {
        public long RequestMsgId { get; set; }
        public object Query { get; set; }
        //public TLObject Query { get; set; }

        public TLRPCResult() { }
        public TLRPCResult(TLBinaryReader from, TLType type = TLType.RPCResult)
        {
            Read(from, type);
        }

        public override TLType TypeId { get { return TLType.RPCResult; } }

        public override void Read(TLBinaryReader from, TLType type = TLType.RPCResult)
        {
            RequestMsgId = from.ReadInt64();
            Query = TLFactory.Read<object>(from);
            //Query = TLFactory.Read<TLObject>(from);
        }

        //public override void Write(TLBinaryWriter to)
        //{
        //    to.Write(0xF35C6D01);
        //    to.Write(RequestMsgId);
        //    Query.Write(to);
        //}
    }
}
