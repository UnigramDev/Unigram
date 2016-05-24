using System.IO;
using System.Runtime.Serialization;
using Telegram.Api.Extensions;

namespace Telegram.Api.TL
{
    [KnownType(typeof(TLUserStatusEmpty))]
    [KnownType(typeof(TLUserStatusOnline))]
    [KnownType(typeof(TLUserStatusOffline))]
    [KnownType(typeof(TLUserStatusRecently))]
    [KnownType(typeof(TLUserStatusLastWeek))]
    [KnownType(typeof(TLUserStatusLastMonth))]
    [DataContract]
    public abstract class TLUserStatus : TLObject{ }

    [DataContract]
    public class TLUserStatusEmpty : TLUserStatus
    {
        public const uint Signature = TLConstructors.TLUserStatusEmpty;

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            return this;
        }

        public override byte[] ToBytes()
        {
            return TLUtils.SignatureToBytes(Signature);
        }


        public override TLObject FromStream(Stream input)
        {
            return this;
        }

        public override void ToStream(Stream output)
        {
            var buffer = ToBytes();
            output.Write(buffer, 0, buffer.Length);
        }
    }

    [DataContract]
    public class TLUserStatusOnline : TLUserStatus
    {
        public const uint Signature = TLConstructors.TLUserStatusOnline;

        [DataMember]
        public TLInt Expires { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Expires = GetObject<TLInt>(bytes, ref position);

            return this;
        }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Expires.ToBytes());
        }

        public override TLObject FromStream(Stream input)
        {
            Expires = GetObject<TLInt>(input);

            return this;
        }

        public override void ToStream(Stream output)
        {
            var buffer = ToBytes();
            output.Write(buffer, 0, buffer.Length);
        }
    }

    [DataContract]
    public class TLUserStatusOffline : TLUserStatus
    {
        public const uint Signature = TLConstructors.TLUserStatusOffline;

        [DataMember]
        public TLInt WasOnline { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            WasOnline = GetObject<TLInt>(bytes, ref position);

            return this;
        }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                WasOnline.ToBytes());
        }

        public override TLObject FromStream(Stream input)
        {
            WasOnline = GetObject<TLInt>(input);

            return this;
        }

        public override void ToStream(Stream output)
        {
            var buffer = ToBytes();
            output.Write(buffer, 0, buffer.Length);
        }
    }

    [DataContract]
    public class TLUserStatusRecently : TLUserStatus
    {
        public const uint Signature = TLConstructors.TLUserStatusRecently;

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            return this;
        }

        public override byte[] ToBytes()
        {
            return TLUtils.SignatureToBytes(Signature);
        }


        public override TLObject FromStream(Stream input)
        {
            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
        }
    }

    [DataContract]
    public class TLUserStatusLastWeek : TLUserStatus
    {
        public const uint Signature = TLConstructors.TLUserStatusLastWeek;

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            return this;
        }

        public override byte[] ToBytes()
        {
            return TLUtils.SignatureToBytes(Signature);
        }


        public override TLObject FromStream(Stream input)
        {
            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
        }
    }

    [DataContract]
    public class TLUserStatusLastMonth : TLUserStatus
    {
        public const uint Signature = TLConstructors.TLUserStatusLastMonth;

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            return this;
        }

        public override byte[] ToBytes()
        {
            return TLUtils.SignatureToBytes(Signature);
        }


        public override TLObject FromStream(Stream input)
        {
            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
        }
    }
}