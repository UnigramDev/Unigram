using System.IO;
using Telegram.Api.Extensions;

namespace Telegram.Api.TL
{
    public abstract class TLDecryptedMessageActionBase : TLObject { }

    #region Additional
    public class TLDecryptedMessageActionEmpty : TLDecryptedMessageActionBase
    {
        public const uint Signature = TLConstructors.TLDecryptedMessageActionEmpty;

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
    #endregion

    public class TLDecryptedMessageActionSetMessageTTL : TLDecryptedMessageActionBase
    {
        public const uint Signature = TLConstructors.TLDecryptedMessageActionSetMessageTTL;

        public TLInt TTLSeconds { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            TTLSeconds = GetObject<TLInt>(bytes, ref position);

            return this;
        }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                TTLSeconds.ToBytes());
        }

        public override TLObject FromStream(Stream input)
        {
            TTLSeconds = GetObject<TLInt>(input);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            output.Write(TTLSeconds.ToBytes());
        }
    }

    public class TLDecryptedMessageActionReadMessages : TLDecryptedMessageActionBase
    {
        public const uint Signature = TLConstructors.TLDecryptedMessageActionReadMessages;

        public TLVector<TLLong> RandomIds { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            RandomIds = GetObject<TLVector<TLLong>>(bytes, ref position);

            return this;
        }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                RandomIds.ToBytes());
        }

        public override TLObject FromStream(Stream input)
        {
            RandomIds = GetObject<TLVector<TLLong>>(input);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            output.Write(RandomIds.ToBytes());
        }
    }

    public class TLDecryptedMessageActionDeleteMessages : TLDecryptedMessageActionBase
    {
        public const uint Signature = TLConstructors.TLDecryptedMessageActionDeleteMessages;

        public TLVector<TLLong> RandomIds { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            RandomIds = GetObject<TLVector<TLLong>>(bytes, ref position);

            return this;
        }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                RandomIds.ToBytes());
        }
        
        public override TLObject FromStream(Stream input)
        {
            RandomIds = GetObject<TLVector<TLLong>>(input);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            output.Write(RandomIds.ToBytes());
        }
    }

    public class TLDecryptedMessageActionScreenshotMessages : TLDecryptedMessageActionBase
    {
        public const uint Signature = TLConstructors.TLDecryptedMessageActionScreenshotMessages;

        public TLVector<TLLong> RandomIds { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            RandomIds = GetObject<TLVector<TLLong>>(bytes, ref position);

            return this;
        }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                RandomIds.ToBytes());
        }

        public override TLObject FromStream(Stream input)
        {
            RandomIds = GetObject<TLVector<TLLong>>(input);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            output.Write(RandomIds.ToBytes());
        }
    }

    public class TLDecryptedMessageActionFlushHistory : TLDecryptedMessageActionBase
    {
        public const uint Signature = TLConstructors.TLDecryptedMessageActionFlushHistory;

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

    public class TLDecryptedMessageActionNotifyLayer : TLDecryptedMessageActionBase
    {
        public const uint Signature = TLConstructors.TLDecryptedMessageActionNotifyLayer;

        public TLInt Layer { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Layer = GetObject<TLInt>(bytes, ref position);

            return this;
        }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Layer.ToBytes());
        }

        public override TLObject FromStream(Stream input)
        {
            Layer = GetObject<TLInt>(input);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            output.Write(Layer.ToBytes());
        }
    }

    public class TLDecryptedMessageActionResend : TLDecryptedMessageActionBase
    {
        public const uint Signature = TLConstructors.TLDecryptedMessageActionResend;

        public TLInt StartSeqNo { get; set; }

        public TLInt EndSeqNo { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            StartSeqNo = GetObject<TLInt>(bytes, ref position);
            EndSeqNo = GetObject<TLInt>(bytes, ref position);

            return this;
        }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                StartSeqNo.ToBytes(),
                EndSeqNo.ToBytes());
        }

        public override TLObject FromStream(Stream input)
        {
            StartSeqNo = GetObject<TLInt>(input);
            EndSeqNo = GetObject<TLInt>(input);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            output.Write(StartSeqNo.ToBytes());
            output.Write(EndSeqNo.ToBytes());
        }
    }

    public class TLDecryptedMessageActionTyping : TLDecryptedMessageActionBase
    {
        public const uint Signature = TLConstructors.TLDecryptedMessageActionTyping;

        public TLSendMessageActionBase Action { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Action = GetObject<TLSendMessageActionBase>(bytes, ref position);

            return this;
        }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Action.ToBytes());
        }

        public override TLObject FromStream(Stream input)
        {
            Action = GetObject<TLSendMessageActionBase>(input);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            output.Write(Action.ToBytes());
        }
    }

    public class TLDecryptedMessageActionRequestKey : TLDecryptedMessageActionBase
    {
        public const uint Signature = TLConstructors.TLDecryptedMessageActionRequestKey;

        public TLLong ExchangeId { get; set; }

        public TLString GA { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            ExchangeId = GetObject<TLLong>(bytes, ref position);
            GA = GetObject<TLString>(bytes, ref position);

            return this;
        }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                ExchangeId.ToBytes(),
                GA.ToBytes());
        }

        public override TLObject FromStream(Stream input)
        {
            ExchangeId = GetObject<TLLong>(input);
            GA = GetObject<TLString>(input);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            output.Write(ExchangeId.ToBytes());
            output.Write(GA.ToBytes());
        }
    }

    public class TLDecryptedMessageActionAcceptKey : TLDecryptedMessageActionBase
    {
        public const uint Signature = TLConstructors.TLDecryptedMessageActionAcceptKey;

        public TLLong ExchangeId { get; set; }

        public TLString GB { get; set; }

        public TLLong KeyFingerprint { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            ExchangeId = GetObject<TLLong>(bytes, ref position);
            GB = GetObject<TLString>(bytes, ref position);
            KeyFingerprint = GetObject<TLLong>(bytes, ref position);

            return this;
        }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                ExchangeId.ToBytes(),
                GB.ToBytes(),
                KeyFingerprint.ToBytes());
        }

        public override TLObject FromStream(Stream input)
        {
            ExchangeId = GetObject<TLLong>(input);
            GB = GetObject<TLString>(input);
            KeyFingerprint = GetObject<TLLong>(input);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            output.Write(ExchangeId.ToBytes());
            output.Write(GB.ToBytes());
            output.Write(KeyFingerprint.ToBytes());
        }
    }

    public class TLDecryptedMessageActionAbortKey : TLDecryptedMessageActionBase
    {
        public const uint Signature = TLConstructors.TLDecryptedMessageActionAbortKey;

        public TLLong ExchangeId { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            ExchangeId = GetObject<TLLong>(bytes, ref position);

            return this;
        }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                ExchangeId.ToBytes());
        }

        public override TLObject FromStream(Stream input)
        {
            ExchangeId = GetObject<TLLong>(input);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            output.Write(ExchangeId.ToBytes());
        }
    }

    public class TLDecryptedMessageActionCommitKey : TLDecryptedMessageActionBase
    {
        public const uint Signature = TLConstructors.TLDecryptedMessageActionCommitKey;

        public TLLong ExchangeId { get; set; }

        public TLLong KeyFingerprint { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            ExchangeId = GetObject<TLLong>(bytes, ref position);
            KeyFingerprint = GetObject<TLLong>(bytes, ref position);

            return this;
        }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                ExchangeId.ToBytes(),
                KeyFingerprint.ToBytes());
        }

        public override TLObject FromStream(Stream input)
        {
            ExchangeId = GetObject<TLLong>(input);
            KeyFingerprint = GetObject<TLLong>(input);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            output.Write(ExchangeId.ToBytes());
            output.Write(KeyFingerprint.ToBytes());
        }
    }

    public class TLDecryptedMessageActionNoop : TLDecryptedMessageActionBase
    {
        public const uint Signature = TLConstructors.TLDecryptedMessageActionNoop;

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            return this;
        }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature));
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
