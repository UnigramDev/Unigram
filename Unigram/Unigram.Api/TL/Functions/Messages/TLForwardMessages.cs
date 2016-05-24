using System;
using System.IO;
using Telegram.Api.Extensions;

namespace Telegram.Api.TL.Functions.Messages
{
#if LAYER_40

    [Flags]
    public enum ForwardFlags
    {
        AsAdmin = 0x10
    }

    public class TLForwardMessages : TLObject, IRandomId
    {
        public const uint Signature = 0x708e0195;

        private TLInt _flags;

        public TLInt Flags
        {
            get { return _flags; }
            set { _flags = value; }
        }

        public TLInputPeerBase FromPeer { get; set; }

        public TLInputPeerBase ToPeer { get; set; }

        public TLVector<TLInt> Id { get; set; }

        public TLVector<TLLong> RandomIds { get; set; }

        public TLLong RandomId
        {
            get
            {
                if (RandomIds != null && RandomIds.Count > 0)
                {
                    return RandomIds[0];
                }

                return new TLLong(0);
            }
        }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Flags.ToBytes(),
                FromPeer.ToBytes(),
                Id.ToBytes(),
                RandomIds.ToBytes(),
                ToPeer.ToBytes());
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));

            Flags.ToStream(output);
            FromPeer.ToStream(output);
            Id.ToStream(output);
            RandomIds.ToStream(output);
            ToPeer.ToStream(output);
        }

        public override TLObject FromStream(Stream input)
        {

            Flags = GetObject<TLInt>(input);
            FromPeer = GetObject<TLInputPeerBase>(input);
            Id = GetObject<TLVector<TLInt>>(input);
            RandomIds = GetObject<TLVector<TLLong>>(input);
            ToPeer = GetObject<TLInputPeerBase>(input);

            return this;
        }

        public void SetAsAdmin()
        {
            Set(ref _flags, (int)ForwardFlags.AsAdmin);
        }
    }
#else
    public class TLForwardMessages : TLObject, IRandomId
    {
        public const uint Signature = 0xded42045;
        
        public TLInputPeerBase Peer { get; set; }

        public TLVector<TLInt> Id { get; set; }

        public TLVector<TLLong> RandomIds { get; set; }

        public TLLong RandomId
        {
            get
            {
                if (RandomIds != null && RandomIds.Count > 0)
                {
                    return RandomIds[0];
                }

                return new TLLong(0);
            }
        }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Peer.ToBytes(),
                Id.ToBytes(),
                RandomIds.ToBytes());
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));

            Peer.ToStream(output);
            Id.ToStream(output);
            RandomIds.ToStream(output);
        }

        public override TLObject FromStream(Stream input)
        {
            Peer = GetObject<TLInputPeerBase>(input);
            Id = GetObject<TLVector<TLInt>>(input);
            RandomIds = GetObject<TLVector<TLLong>>(input);

            return this;
        }
    }
#endif
}
