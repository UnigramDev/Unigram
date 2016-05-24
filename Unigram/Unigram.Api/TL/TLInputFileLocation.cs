namespace Telegram.Api.TL
{
    public abstract class TLInputFileLocationBase : TLObject
    {
        public abstract bool LocationEquals(TLInputFileLocationBase location);

        public abstract string GetPartFileName(int partNumbert);

        public abstract string GetFileName();
    }

    public class TLInputFileLocation : TLInputFileLocationBase
    {
        public const uint Signature = TLConstructors.TLInputFileLocation;

        public TLLong VolumeId { get; set; }

        public TLInt LocalId { get; set; }

        public TLLong Secret { get; set; }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                VolumeId.ToBytes(),
                LocalId.ToBytes(),
                Secret.ToBytes());
        }

        public override bool LocationEquals(TLInputFileLocationBase location)
        {
            if (location == null) return false;

            var fileLocation = location as TLInputFileLocation;
            if (fileLocation == null) return false;

            return
                VolumeId.Value == fileLocation.VolumeId.Value
                && LocalId.Value == fileLocation.LocalId.Value
                && Secret.Value == fileLocation.Secret.Value;
        }

        public override string GetPartFileName(int partNumbert)
        {
            return string.Format("file{0}_{1}_{2}_{3}.dat", VolumeId.Value, LocalId.Value, Secret.Value, partNumbert);
        }

        public override string GetFileName()
        {
            return string.Format("file{0}_{1}_{2}.dat", VolumeId.Value, LocalId.Value, Secret.Value);
        }
    }

    public class TLInputVideoFileLocation : TLInputFileLocationBase
    {
        public const uint Signature = TLConstructors.TLInputVideoFileLocation;

        public TLLong Id { get; set; }

        public TLLong AccessHash { get; set; }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Id.ToBytes(),
                AccessHash.ToBytes());
        }

        public override bool LocationEquals(TLInputFileLocationBase location)
        {
            if (location == null) return false;

            var fileLocation = location as TLInputVideoFileLocation;
            if (fileLocation == null) return false;

            return
                Id.Value == fileLocation.Id.Value
                && AccessHash.Value == fileLocation.AccessHash.Value;
        }

        public override string GetPartFileName(int partNumbert)
        {
            return string.Format("video{0}_{1}_{2}.dat", Id.Value, AccessHash.Value, partNumbert);
        }

        public override string GetFileName()
        {
            return string.Format("video{0}_{1}.mp4", Id.Value, AccessHash.Value);
        }
    }

    public class TLInputAudioFileLocation : TLInputFileLocationBase
    {
        public const uint Signature = TLConstructors.TLInputAudioFileLocation;

        public TLLong Id { get; set; }

        public TLLong AccessHash { get; set; }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Id.ToBytes(),
                AccessHash.ToBytes());
        }

        public override bool LocationEquals(TLInputFileLocationBase location)
        {
            if (location == null) return false;

            var fileLocation = location as TLInputAudioFileLocation;
            if (fileLocation == null) return false;

            return
                Id.Value == fileLocation.Id.Value
                && AccessHash.Value == fileLocation.AccessHash.Value;
        }

        public override string GetPartFileName(int partNumbert)
        {
            return string.Format("audio{0}_{1}_{2}.dat", Id.Value, AccessHash.Value, partNumbert);
        }

        public override string GetFileName()
        {
            return string.Format("audio{0}_{1}.mp3", Id.Value, AccessHash.Value);
        }
    }

    public class TLInputDocumentFileLocation : TLInputFileLocationBase
    {
        public const uint Signature = TLConstructors.TLInputDocumentFileLocation;

        public TLLong Id { get; set; }

        public TLLong AccessHash { get; set; }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Id.ToBytes(),
                AccessHash.ToBytes());
        }

        public override bool LocationEquals(TLInputFileLocationBase location)
        {
            if (location == null) return false;

            var fileLocation = location as TLInputDocumentFileLocation;
            if (fileLocation == null) return false;

            return
                Id.Value == fileLocation.Id.Value
                && AccessHash.Value == fileLocation.AccessHash.Value;
        }

        public override string GetPartFileName(int partNumbert)
        {
            return string.Format("document{0}_{1}_{2}.dat", Id.Value, AccessHash.Value, partNumbert);
        }

        public override string GetFileName()
        {
            return string.Format("document{0}_{1}.dat", Id.Value, AccessHash.Value);
        }
    }

    public class TLInputEncryptedFileLocation : TLInputFileLocationBase
    {
        public const uint Signature = TLConstructors.TLInputEncryptedFileLocation;

        public TLLong Id { get; set; }

        public TLLong AccessHash { get; set; }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Id.ToBytes(),
                AccessHash.ToBytes());
        }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Id = GetObject<TLLong>(bytes, ref position);
            AccessHash = GetObject<TLLong>(bytes, ref position);

            return this;
        }

        public override bool LocationEquals(TLInputFileLocationBase location)
        {
            if (location == null) return false;

            var fileLocation = location as TLInputEncryptedFileLocation;
            if (fileLocation == null) return false;

            return
                Id.Value == fileLocation.Id.Value
                && AccessHash.Value == fileLocation.AccessHash.Value;
        }

        public override string GetPartFileName(int partNumbert)
        {
            return string.Format("encrypted{0}_{1}_{2}.dat", Id.Value, AccessHash.Value, partNumbert);
        }

        public override string GetFileName()
        {
            return string.Format("encrypted{0}_{1}.dat", Id.Value, AccessHash.Value);
        }
    }
}
