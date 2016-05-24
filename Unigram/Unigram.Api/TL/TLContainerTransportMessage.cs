using System;
using System.Diagnostics;

namespace Telegram.Api.TL
{
    public class TLTransportMessageWithIdBase : TLObject
    {
        public TLLong MessageId { get; set; }
    }

    public class TLContainerTransportMessage : TLTransportMessageWithIdBase
    {
        public TLInt SeqNo { get; set; }
        public TLInt MessageDataLength { get; set; }
        public TLObject MessageData { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            MessageId = GetObject<TLLong>(bytes, ref position);
            SeqNo = GetObject<TLInt>(bytes, ref position);
            MessageDataLength = GetObject<TLInt>(bytes, ref position);
            MessageData = GetObject<TLObject>(bytes, ref position);

            Debug.WriteLine("  <<{0, -28} MsgId {1} SeqNo {2, 4}", "containerMessage", MessageId, SeqNo);

            return this;
        }

        public override byte[] ToBytes()
        {
            var objectBytes = MessageData.ToBytes();

            return TLUtils.Combine(
                MessageId.ToBytes(),
                SeqNo.ToBytes(),
                BitConverter.GetBytes(objectBytes.Length),
                objectBytes);
        }
    }

    public class TLTransportMessage : TLContainerTransportMessage
    {
        public TLLong Salt { get; set; }
        public TLLong SessionId { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            Salt = GetObject<TLLong>(bytes, ref position);
            SessionId = GetObject<TLLong>(bytes, ref position);
            
            MessageId = GetObject<TLLong>(bytes, ref position);
            SeqNo = GetObject<TLInt>(bytes, ref position);
            MessageDataLength = GetObject<TLInt>(bytes, ref position);
            MessageData = GetObject<TLObject>(bytes, ref position);

            Debug.WriteLine("<<{3, -30} MsgId {0} SeqNo {2, 4} SessionId {1}", MessageId, SessionId, SeqNo, "message");

            return this;
        }

        public override byte[] ToBytes()
        {
            var objectBytes = MessageData.ToBytes();

            return TLUtils.Combine(
               Salt.ToBytes(),
               SessionId.ToBytes(),
               MessageId.ToBytes(),
               SeqNo.ToBytes(),
               BitConverter.GetBytes(objectBytes.Length),
               objectBytes);
        }
    }
}