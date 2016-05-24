using System;

namespace Telegram.Api.TL.Functions.Messages
{
#if LAYER_40
    [Flags]
    public enum SearchFlags
    {
        Important = 0x1,
    }

    public class TLSearch : TLObject
    {
        public const uint Signature = 0xd4569248;

        private TLInt _flags;

        public TLInt Flags
        {
            get { return _flags; }
            set { _flags = value; }
        }

        public TLInputPeerBase Peer { get; set; }

        public TLString Query { get; set; }

        public TLInputMessagesFilterBase Filter { get; set; }

        public TLInt MinDate { get; set; }

        public TLInt MaxDate { get; set; }

        public TLInt Offset { get; set; }

        public TLInt MaxId { get; set; }

        public TLInt Limit { get; set; }

        public void SetImportant()
        {
            Set(ref _flags, (int) SearchFlags.Important);
        }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Flags.ToBytes(),
                Peer.ToBytes(),
                Query.ToBytes(),
                Filter.ToBytes(),
                MinDate.ToBytes(),
                MaxDate.ToBytes(),
                Offset.ToBytes(),
                MaxId.ToBytes(),
                Limit.ToBytes());
        }
    }
#else
    public class TLSearch : TLObject
    {
        public const string Signature = "#7e9f2ab";

        public TLInputPeerBase Peer { get; set; }

        public TLString Query { get; set; }

        public TLInputMessagesFilterBase Filter { get; set; }

        public TLInt MinDate { get; set; }

        public TLInt MaxDate { get; set; }

        public TLInt Offset { get; set; }

        public TLInt MaxId { get; set; }

        public TLInt Limit { get; set; }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Peer.ToBytes(),
                Query.ToBytes(),
                Filter.ToBytes(),
                MinDate.ToBytes(),
                MaxDate.ToBytes(),
                Offset.ToBytes(),
                MaxId.ToBytes(),
                Limit.ToBytes());
        }
    }
#endif
}
