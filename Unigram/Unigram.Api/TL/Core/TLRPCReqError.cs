using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Telegram.Api.TL
{
    public class TLRPCReqError : TLRPCError
    {
        public Int64 QueryId;

        public TLRPCReqError() { }
        public TLRPCReqError(TLBinaryReader from, bool fromCache)
        {
            Read(from, fromCache);
        }

        public override TLType TypeId { get { return TLType.RPCReqError; } }

        public override void Read(TLBinaryReader from, bool fromCache)
        {
            QueryId = from.ReadInt64();
            ErrorCode = from.ReadInt32();
            ErrorMessage = from.ReadString();
        }

        public override void Write(TLBinaryWriter to, bool toCache)
        {
            to.Write(0x7AE432F5);
            to.Write(QueryId);
            to.Write(ErrorCode);
            to.Write(ErrorMessage);
        }
    }
}
