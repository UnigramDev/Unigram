using System.IO;
using System.Linq;
using Telegram.Api.Extensions;

namespace Telegram.Api.TL
{
    public abstract class TLPeerBase : TLObject
    {
        public TLInt Id { get; set; }
    }

    public class TLPeerUser : TLPeerBase
    {
        public const uint Signature = TLConstructors.TLPeerUser;

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Id = GetObject<TLInt>(bytes, ref position);

            return this;
        }

        public override byte[] ToBytes()
        {
            return TLUtils.SignatureToBytes(Signature)
                .Concat(Id.ToBytes())
                .ToArray();
        }

        public override TLObject FromStream(Stream input)
        {
            Id = GetObject<TLInt>(input);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            output.Write(Id.ToBytes());
        }

        public override string ToString()
        {
            return "UserId=" + Id;
        }
    }

    public class TLPeerChat : TLPeerBase
    {
        public const uint Signature = TLConstructors.TLPeerChat;

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Id = GetObject<TLInt>(bytes, ref position);

            return this;
        }

        public override byte[] ToBytes()
        {
            return TLUtils.SignatureToBytes(Signature)
                .Concat(Id.ToBytes())
                .ToArray();
        }

        public override TLObject FromStream(Stream input)
        {
            Id = GetObject<TLInt>(input);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            output.Write(Id.ToBytes());
        }

        public override string ToString()
        {
            return "ChatId=" + Id;
        }
    }

    public class TLPeerEncryptedChat : TLPeerBase
    {
        public const uint Signature = TLConstructors.TLPeerEncryptedChat;

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Id = GetObject<TLInt>(bytes, ref position);

            return this;
        }

        public override byte[] ToBytes()
        {
            return TLUtils.SignatureToBytes(Signature)
                .Concat(Id.ToBytes())
                .ToArray();
        }

        public override TLObject FromStream(Stream input)
        {
            Id = GetObject<TLInt>(input);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            output.Write(Id.ToBytes());
        }

        public override string ToString()
        {
            return "EncryptedChatId=" + Id;
        }
    }

    public class TLPeerBroadcast : TLPeerBase
    {
        public const uint Signature = TLConstructors.TLPeerBroadcastChat;

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Id = GetObject<TLInt>(bytes, ref position);

            return this;
        }

        public override byte[] ToBytes()
        {
            return TLUtils.SignatureToBytes(Signature)
                .Concat(Id.ToBytes())
                .ToArray();
        }

        public override TLObject FromStream(Stream input)
        {
            Id = GetObject<TLInt>(input);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            output.Write(Id.ToBytes());
        }

        public override string ToString()
        {
            return "BroadcastChatId=" + Id;
        }
    }

    public class TLPeerChannel : TLPeerBase
    {
        public const uint Signature = TLConstructors.TLPeerChannel;

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Id = GetObject<TLInt>(bytes, ref position);

            return this;
        }

        public override byte[] ToBytes()
        {
            return TLUtils.SignatureToBytes(Signature)
                .Concat(Id.ToBytes())
                .ToArray();
        }

        public override TLObject FromStream(Stream input)
        {
            Id = GetObject<TLInt>(input);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            output.Write(Id.ToBytes());
        }

        public override string ToString()
        {
            return "ChannelId=" + Id;
        }
    }
}
