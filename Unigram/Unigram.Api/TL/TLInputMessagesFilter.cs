namespace Telegram.Api.TL
{
    public abstract class TLInputMessagesFilterBase : TLObject { }

    public class TLInputMessagesFilterEmpty : TLInputMessagesFilterBase
    {
        public const uint Signature = TLConstructors.TLInputMessageFilterEmpty;

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);
            return this;
        }

        public override byte[] ToBytes()
        {
            return TLUtils.SignatureToBytes(Signature);
        }
    }

    public class TLInputMessagesFilterPhoto : TLInputMessagesFilterBase
    {
        public const uint Signature = TLConstructors.TLInputMessageFilterPhoto;

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);
            return this;
        }

        public override byte[] ToBytes()
        {
            return TLUtils.SignatureToBytes(Signature);
        }
    }

    public class TLInputMessagesFilterVideo : TLInputMessagesFilterBase
    {
        public const uint Signature = TLConstructors.TLInputMessageFilterVideo;

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);
            return this;
        }

        public override byte[] ToBytes()
        {
            return TLUtils.SignatureToBytes(Signature);
        }
    }

    public class TLInputMessagesFilterPhotoVideo : TLInputMessagesFilterBase
    {
        public const uint Signature = TLConstructors.TLInputMessageFilterPhotoVideo;

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);
            return this;
        }

        public override byte[] ToBytes()
        {
            return TLUtils.SignatureToBytes(Signature);
        }
    }

    public class TLInputMessagesFilterPhotoVideoDocument : TLInputMessagesFilterBase
    {
        public const uint Signature = TLConstructors.TLInputMessageFilterPhotoVideoDocument;

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);
            return this;
        }

        public override byte[] ToBytes()
        {
            return TLUtils.SignatureToBytes(Signature);
        }
    }

    public class TLInputMessagesFilterDocument : TLInputMessagesFilterBase
    {
        public const uint Signature = TLConstructors.TLInputMessageFilterDocument;

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);
            return this;
        }

        public override byte[] ToBytes()
        {
            return TLUtils.SignatureToBytes(Signature);
        }
    }

    public class TLInputMessagesFilterAudio : TLInputMessagesFilterBase
    {
        public const uint Signature = TLConstructors.TLInputMessageFilterAudio;

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);
            return this;
        }

        public override byte[] ToBytes()
        {
            return TLUtils.SignatureToBytes(Signature);
        }
    }

    public class TLInputMessagesFilterAudioDocuments : TLInputMessagesFilterBase
    {
        public const uint Signature = TLConstructors.TLInputMessageFilterAudioDocuments;

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);
            return this;
        }

        public override byte[] ToBytes()
        {
            return TLUtils.SignatureToBytes(Signature);
        }
    }

    public class TLInputMessagesFilterUrl : TLInputMessagesFilterBase
    {
        public const uint Signature = TLConstructors.TLInputMessageFilterUrl;

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);
            return this;
        }

        public override byte[] ToBytes()
        {
            return TLUtils.SignatureToBytes(Signature);
        }
    }
}
