using System.IO;
using Telegram.Api.Extensions;

namespace Telegram.Api.TL
{
    public abstract class TLSendMessageActionBase : TLObject { }

    public class TLSendMessageTypingAction : TLSendMessageActionBase
    {
        public const uint Signature = TLConstructors.TLSendMessageTypingAction;

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            return this;
        }

        public override byte[] ToBytes()
        {
            return TLUtils.SignatureToBytes(Signature);
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
        }

        public override TLObject FromStream(Stream input)
        {
            return this;
        }
    }

    public class TLSendMessageCancelAction : TLSendMessageActionBase
    {
        public const uint Signature = TLConstructors.TLSendMessageCancelAction;

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            return this;
        }

        public override byte[] ToBytes()
        {
            return TLUtils.SignatureToBytes(Signature);
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
        }

        public override TLObject FromStream(Stream input)
        {
            return this;
        }
    }

    public class TLSendMessageRecordVideoAction : TLSendMessageActionBase
    {
        public const uint Signature = TLConstructors.TLSendMessageRecordVideoAction;

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            return this;
        }

        public override byte[] ToBytes()
        {
            return TLUtils.SignatureToBytes(Signature);
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
        }

        public override TLObject FromStream(Stream input)
        {
            return this;
        }
    }

    public class TLSendMessageUploadVideoAction : TLSendMessageActionBase
    {
        public const uint Signature = TLConstructors.TLSendMessageUploadVideoAction;

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            return this;
        }

        public override byte[] ToBytes()
        {
            return TLUtils.SignatureToBytes(Signature);
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
        }

        public override TLObject FromStream(Stream input)
        {
            return this;
        }
    }

    public class TLSendMessageUploadVideoAction28 : TLSendMessageUploadVideoAction
    {
        public new const uint Signature = TLConstructors.TLSendMessageUploadVideoAction28;

        public TLInt Progress { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Progress = GetObject<TLInt>(bytes, ref position);

            return this;
        }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(TLUtils.SignatureToBytes(Signature),
                Progress.ToBytes());
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            Progress.ToStream(output);
        }

        public override TLObject FromStream(Stream input)
        {
            Progress = GetObject<TLInt>(input);

            return this;
        }
    }

    public class TLSendMessageRecordAudioAction : TLSendMessageActionBase
    {
        public const uint Signature = TLConstructors.TLSendMessageRecordAudioAction;

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            return this;
        }

        public override byte[] ToBytes()
        {
            return TLUtils.SignatureToBytes(Signature);
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
        }

        public override TLObject FromStream(Stream input)
        {
            return this;
        }
    }

    public class TLSendMessageUploadAudioAction : TLSendMessageActionBase
    {
        public const uint Signature = TLConstructors.TLSendMessageUploadAudioAction;

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            return this;
        }

        public override byte[] ToBytes()
        {
            return TLUtils.SignatureToBytes(Signature);
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
        }

        public override TLObject FromStream(Stream input)
        {
            return this;
        }
    }

    public class TLSendMessageUploadAudioAction28 : TLSendMessageUploadAudioAction
    {
        public new const uint Signature = TLConstructors.TLSendMessageUploadAudioAction28;

        public TLInt Progress { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Progress = GetObject<TLInt>(bytes, ref position);

            return this;
        }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(TLUtils.SignatureToBytes(Signature),
                Progress.ToBytes());
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            Progress.ToStream(output);
        }

        public override TLObject FromStream(Stream input)
        {
            Progress = GetObject<TLInt>(input);

            return this;
        }
    }

    public class TLSendMessageUploadPhotoAction : TLSendMessageActionBase
    {
        public const uint Signature = TLConstructors.TLSendMessageUploadPhotoAction;

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            return this;
        }

        public override byte[] ToBytes()
        {
            return TLUtils.SignatureToBytes(Signature);
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
        }

        public override TLObject FromStream(Stream input)
        {
            return this;
        }
    }

    public class TLSendMessageUploadPhotoAction28 : TLSendMessageUploadPhotoAction
    {
        public new const uint Signature = TLConstructors.TLSendMessageUploadPhotoAction28;

        public TLInt Progress { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Progress = GetObject<TLInt>(bytes, ref position);

            return this;
        }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(TLUtils.SignatureToBytes(Signature),
                Progress.ToBytes());
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            Progress.ToStream(output);
        }

        public override TLObject FromStream(Stream input)
        {
            Progress = GetObject<TLInt>(input);

            return this;
        }
    }

    public class TLSendMessageUploadDocumentAction : TLSendMessageActionBase
    {
        public const uint Signature = TLConstructors.TLSendMessageUploadDocumentAction;

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            return this;
        }

        public override byte[] ToBytes()
        {
            return TLUtils.SignatureToBytes(Signature);
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
        }

        public override TLObject FromStream(Stream input)
        {
            return this;
        }
    }

    public class TLSendMessageUploadDocumentAction28 : TLSendMessageUploadDocumentAction
    {
        public new const uint Signature = TLConstructors.TLSendMessageUploadDocumentAction28;

        public TLInt Progress { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Progress = GetObject<TLInt>(bytes, ref position);

            return this;
        }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(TLUtils.SignatureToBytes(Signature),
                Progress.ToBytes());
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            Progress.ToStream(output);
        }

        public override TLObject FromStream(Stream input)
        {
            Progress = GetObject<TLInt>(input);

            return this;
        }
    }

    public class TLSendMessageGeoLocationAction : TLSendMessageActionBase
    {
        public const uint Signature = TLConstructors.TLSendMessageGeoLocationAction;

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            return this;
        }

        public override byte[] ToBytes()
        {
            return TLUtils.SignatureToBytes(Signature);
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
        }

        public override TLObject FromStream(Stream input)
        {
            return this;
        }
    }

    public class TLSendMessageChooseContactAction : TLSendMessageActionBase
    {
        public const uint Signature = TLConstructors.TLSendMessageChooseContactAction;

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            return this;
        }

        public override byte[] ToBytes()
        {
            return TLUtils.SignatureToBytes(Signature);
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
        }

        public override TLObject FromStream(Stream input)
        {
            return this;
        }
    }
}
