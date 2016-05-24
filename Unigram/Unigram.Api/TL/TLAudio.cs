using System;
using System.IO;
using Telegram.Api.Aggregator;
using Telegram.Api.Extensions;
using Telegram.Api.Services.Cache;

namespace Telegram.Api.TL
{
    public abstract class TLAudioBase : TLObject
    {
        public TLLong Id { get; set; }

        public virtual int AudioSize{get { return 0; }}
    }

    public class TLAudioEmpty : TLAudioBase
    {
        public const uint Signature = TLConstructors.TLAudioEmpty;

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Id = GetObject<TLLong>(bytes, ref position);

            return this;
        }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(TLUtils.SignatureToBytes(Signature), Id.ToBytes());
        }

        public override TLObject FromStream(Stream input)
        {
            Id = GetObject<TLLong>(input);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));

            Id.ToStream(output);
        }
    }

    public class TLAudio33 : TLAudio
    {
        public new const uint Signature = TLConstructors.TLAudio33;

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Id = GetObject<TLLong>(bytes, ref position);
            AccessHash = GetObject<TLLong>(bytes, ref position);
            //UserId = GetObject<TLInt>(bytes, ref position);
            Date = GetObject<TLInt>(bytes, ref position);
            Duration = GetObject<TLInt>(bytes, ref position);
            MimeType = GetObject<TLString>(bytes, ref position);
            Size = GetObject<TLInt>(bytes, ref position);
            DCId = GetObject<TLInt>(bytes, ref position);

            return this;
        }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Id.ToBytes(),
                AccessHash.ToBytes(),
                //UserId.ToBytes(),
                Date.ToBytes(),
                Duration.ToBytes(),
                MimeType.ToBytes(),
                Size.ToBytes(),
                DCId.ToBytes());
        }

        public override TLObject FromStream(Stream input)
        {
            Id = GetObject<TLLong>(input);
            AccessHash = GetObject<TLLong>(input);
            //UserId = GetObject<TLInt>(input);
            Date = GetObject<TLInt>(input);
            Duration = GetObject<TLInt>(input);
            MimeType = GetObject<TLString>(input);
            Size = GetObject<TLInt>(input);
            DCId = GetObject<TLInt>(input);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));

            Id.ToStream(output);
            AccessHash.ToStream(output);
            //UserId.ToStream(output);
            Date.ToStream(output);
            Duration.ToStream(output);
            MimeType.ToStream(output);
            Size.ToStream(output);
            DCId.ToStream(output);
        }
    }

    public class TLAudio : TLAudioBase
    {
        public const uint Signature = TLConstructors.TLAudio;

        public TLLong AccessHash { get; set; }

        public TLInt UserId { get; set; }

        public TLInt Date { get; set; }

        public TLInt Duration { get; set; }

        public TLString MimeType { get; set; }

        public TLInt Size { get; set; }

        public TLInt DCId { get; set; }

        public string GetFileName()
        {
            return string.Format("audio{0}_{1}.mp3", Id, AccessHash);
        }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Id = GetObject<TLLong>(bytes, ref position);
            AccessHash = GetObject<TLLong>(bytes, ref position);
            UserId = GetObject<TLInt>(bytes, ref position);
            Date = GetObject<TLInt>(bytes, ref position);
            Duration = GetObject<TLInt>(bytes, ref position);
            MimeType = GetObject<TLString>(bytes, ref position);
            Size = GetObject<TLInt>(bytes, ref position);
            DCId = GetObject<TLInt>(bytes, ref position);

            return this;
        }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Id.ToBytes(),
                AccessHash.ToBytes(),
                UserId.ToBytes(),
                Date.ToBytes(),
                Duration.ToBytes(),
                MimeType.ToBytes(),
                Size.ToBytes(),
                DCId.ToBytes());
        }

        public override TLObject FromStream(Stream input)
        {
            Id = GetObject<TLLong>(input);
            AccessHash = GetObject<TLLong>(input);
            UserId = GetObject<TLInt>(input);
            Date = GetObject<TLInt>(input);
            Duration = GetObject<TLInt>(input);
            MimeType = GetObject<TLString>(input);
            Size = GetObject<TLInt>(input);
            DCId = GetObject<TLInt>(input);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));

            Id.ToStream(output);
            AccessHash.ToStream(output);
            UserId.ToStream(output);
            Date.ToStream(output);
            Duration.ToStream(output);
            MimeType.ToStream(output);
            Size.ToStream(output);
            DCId.ToStream(output);
        }

        #region Additional

        public override int AudioSize
        {
            get
            {
                return Size != null ? Size.Value : 0;
            }
        }

        public string DurationString
        {
            get
            {
                var timeSpan = TimeSpan.FromSeconds(Duration.Value);

                if (timeSpan.Hours > 0)
                {
                    return timeSpan.ToString(@"h\:mm\:ss");
                }

                return timeSpan.ToString(@"m\:ss");
            }
        }

        public TLUserBase User
        {
            get
            {
                var cacheService = InMemoryCacheService.Instance;
                return cacheService.GetUser(UserId);
            }
        }
        #endregion

        public TLInputAudioFileLocation ToInputFileLocation()
        {
            return new TLInputAudioFileLocation { AccessHash = AccessHash, Id = Id };
        }
    }
}
