using System.IO;
using Telegram.Api.Extensions;

namespace Telegram.Api.TL.Functions.Messages
{
    public class TLStartBot : TLObject, IRandomId
    {
#if LAYER_40
        public const uint Signature = 0x1b3e0ffc;

        public TLInputUserBase Bot { get; set; }

        public TLInt ChatId { get; set; }

        public TLLong RandomId { get; set; }

        public TLString StartParam { get; set; }
#else
        public const uint Signature = 0x1b3e0ffc;

        public TLInputUserBase Bot { get; set; }

        public TLInt ChatId { get; set; }

        public TLLong RandomId { get; set; }

        public TLString StartParam { get; set; }
#endif

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Bot.ToBytes(),
                ChatId.ToBytes(),
                RandomId.ToBytes(),
                StartParam.ToBytes()
            );
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));

            Bot.ToStream(output);
            ChatId.ToStream(output);
            RandomId.ToStream(output);
            StartParam.ToStream(output);
        }

        public override TLObject FromStream(Stream input)
        {
            Bot = GetObject<TLInputUserBase>(input);
            ChatId = GetObject<TLInt>(input);
            RandomId = GetObject<TLLong>(input);
            StartParam = GetObject<TLString>(input);

            return this;
        }
    }
}
