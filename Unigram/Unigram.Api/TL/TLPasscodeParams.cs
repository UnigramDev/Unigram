using System.IO;
using Telegram.Api.Extensions;

namespace Telegram.Api.TL
{
    public class TLPasscodeParams : TLObject
    {
        public const uint Signature = TLConstructors.TLPasscodeParams;

        public TLString Hash { get; set; }
        public TLString Salt { get; set; }
        public TLBool IsSimple { get; set; }
        public TLInt CloseTime { get; set; }
        public TLInt AutolockTimeout { get; set; }
        public TLBool Locked { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Hash = GetObject<TLString>(bytes, ref position);
            Salt = GetObject<TLString>(bytes, ref position);
            IsSimple = GetObject<TLBool>(bytes, ref position);
            CloseTime = GetObject<TLInt>(bytes, ref position);
            AutolockTimeout = GetObject<TLInt>(bytes, ref position);
            Locked = GetObject<TLBool>(bytes, ref position);

            return this;
        }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(TLUtils.SignatureToBytes(Signature), 
                Hash.ToBytes(), 
                Salt.ToBytes(),
                IsSimple.ToBytes(),
                CloseTime.ToBytes(),
                AutolockTimeout.ToBytes(),
                Locked.ToBytes());
        }

        public override TLObject FromStream(Stream input)
        {
            Hash = GetObject<TLString>(input);
            Salt = GetObject<TLString>(input);
            IsSimple = GetObject<TLBool>(input);
            CloseTime = GetObject<TLInt>(input);
            AutolockTimeout = GetObject<TLInt>(input);
            Locked = GetObject<TLBool>(input);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));

            Hash.ToStream(output);
            Salt.ToStream(output);
            IsSimple.ToStream(output);
            CloseTime.ToStream(output);
            AutolockTimeout.ToStream(output);
            Locked.ToStream(output);
        }
    }
}
