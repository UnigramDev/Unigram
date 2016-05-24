using System.IO;
using Telegram.Api.Extensions;

namespace Telegram.Api.TL
{
    public abstract class TLEncryptedMessageBase : TLObject
    {
        public TLLong RandomId { get; set; }

        public TLInt ChatId { get; set; }

        public TLInt Date { get; set; }

        public TLString Bytes { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            RandomId = GetObject<TLLong>(bytes, ref position);
            ChatId = GetObject<TLInt>(bytes, ref position);
            Date = GetObject<TLInt>(bytes, ref position);
            Bytes = GetObject<TLString>(bytes, ref position);

            return this;
        }
    }

    public class TLEncryptedMessage : TLEncryptedMessageBase
    {
        public const uint Signature = TLConstructors.TLEncryptedMessage;

        public TLEncryptedFileBase File { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            base.FromBytes(bytes, ref position);
            File = GetObject<TLEncryptedFileBase>(bytes, ref position);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            RandomId.ToStream(output);
            ChatId.ToStream(output);
            Date.ToStream(output);
            Bytes.ToStream(output);
            File.ToStream(output);
        }

        public override TLObject FromStream(Stream input)
        {
            RandomId = GetObject<TLLong>(input);
            ChatId = GetObject<TLInt>(input);
            Date = GetObject<TLInt>(input);
            Bytes = GetObject<TLString>(input);
            File = GetObject<TLEncryptedFileBase>(input);

            return this;
        }
    }

    public class TLEncryptedMessageService : TLEncryptedMessageBase
    {
        public const uint Signature = TLConstructors.TLEncryptedMessageService;

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            base.FromBytes(bytes, ref position);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            RandomId.ToStream(output);
            ChatId.ToStream(output);
            Date.ToStream(output);
            Bytes.ToStream(output);
        }

        public override TLObject FromStream(Stream input)
        {
            RandomId = GetObject<TLLong>(input);
            ChatId = GetObject<TLInt>(input);
            Date = GetObject<TLInt>(input);
            Bytes = GetObject<TLString>(input);

            return this;
        }
    }
}
