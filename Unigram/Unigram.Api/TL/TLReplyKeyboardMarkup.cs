using System.IO;
using Telegram.Api.Extensions;

namespace Telegram.Api.TL
{
    enum ReplyKeyboardFlags
    {
        Resize = 0x1,
        SingleUse = 0x2,
        Personal = 0x4,
    }

    enum ReplyKeyboardCustomFlags
    {
        HasResponse = 0x1,
    }

    public abstract class TLReplyKeyboardBase : TLObject
    {
        public TLInt Flags { get; set; }

        private TLLong _customFlags;

        public TLLong CustomFlags
        {
            get { return _customFlags; }
            set { _customFlags = value; }
        }

        public bool IsResizable
        {
            get { return IsSet(Flags, (int)ReplyKeyboardFlags.Resize); }
        }

        public bool IsSingleUse
        {
            get { return IsSet(Flags, (int)ReplyKeyboardFlags.SingleUse); }
        }

        public bool IsPersonal
        {
            get { return IsSet(Flags, (int)ReplyKeyboardFlags.Personal); }
        }

        public bool HasResponse
        {
            get { return IsSet(CustomFlags, (int) ReplyKeyboardCustomFlags.HasResponse); }
            set { Set(ref _customFlags, (int) ReplyKeyboardCustomFlags.HasResponse);}
        }

        public override string ToString()
        {
            var isPersonal = IsPersonal ? 1 : 0;
            var isResizable = IsResizable ? 1 : 0;
            var isSingleUse = IsSingleUse ? 1 : 0;
            var hasResponse = HasResponse ? 1 : 0;
            return string.Format("{0} {1} {2} {3}", isPersonal, isResizable, isSingleUse, hasResponse);
        }
    }

    public class TLReplyKeyboardMarkup : TLReplyKeyboardBase
    {
        public const uint Signature = TLConstructors.TLReplyKeyboardMarkup;

        public TLVector<TLKeyboardButtonRow> Rows { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Flags = GetObject<TLInt>(bytes, ref position);
            Rows = GetObject<TLVector<TLKeyboardButtonRow>>(bytes, ref position);

            return this;
        }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Flags.ToBytes(),
                Rows.ToBytes());
        }

        public override TLObject FromStream(Stream input)
        {
            Flags = GetObject<TLInt>(input);
            Rows = GetObject<TLVector<TLKeyboardButtonRow>>(input);

            CustomFlags = GetNullableObject<TLLong>(input);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            output.Write(Flags.ToBytes());
            output.Write(Rows.ToBytes());

            CustomFlags.NullableToStream(output);
        }

        public override string ToString()
        {
            return "KM " + base.ToString();
        }
    }

    public class TLReplyKeyboardHide : TLReplyKeyboardBase
    {
        public const uint Signature = TLConstructors.TLReplyKeyboardHide;

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Flags = GetObject<TLInt>(bytes, ref position);

            return this;
        }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Flags.ToBytes());
        }

        public override TLObject FromStream(Stream input)
        {
            Flags = GetObject<TLInt>(input);

            CustomFlags = GetNullableObject<TLLong>(input);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            output.Write(Flags.ToBytes());

            CustomFlags.NullableToStream(output);
        }

        public override string ToString()
        {
            return "KH " + base.ToString();
        }
    }

    public class TLReplyKeyboardForceReply : TLReplyKeyboardBase
    {
        public const uint Signature = TLConstructors.TLReplyKeyboardForceReply;

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Flags = GetObject<TLInt>(bytes, ref position);

            return this;
        }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Flags.ToBytes());
        }

        public override TLObject FromStream(Stream input)
        {
            Flags = GetObject<TLInt>(input);

            CustomFlags = GetNullableObject<TLLong>(input);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            output.Write(Flags.ToBytes());

            CustomFlags.NullableToStream(output);
        }

        public override string ToString()
        {
            return "KFR " + base.ToString();
        }
    }
}
