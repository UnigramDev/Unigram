using System;
using System.Collections.Generic;
using System.Linq;

namespace Telegram.Api.TL
{
    public class TLContainer : TLObject
    {
        public const uint Signature = TLConstructors.TLContainer;

        public List<TLContainerTransportMessage> Messages { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Messages = new List<TLContainerTransportMessage>();

            var length = BitConverter.ToInt32(bytes, position);
            position += 4;
            for (var i = 0; i < length; i++)
            {
                Messages.Add(GetObject<TLContainerTransportMessage>(bytes, ref position));
            }

            return this;
        }

        public override byte[] ToBytes()
        {
            var bytes = new byte[] { };
            bytes = Messages.Aggregate(bytes, (current, next) => current.Concat(next.ToBytes()).ToArray());

            return TLUtils.SignatureToBytes(Signature)
                .Concat(BitConverter.GetBytes(Messages.Count))
                .Concat(bytes)
                .ToArray();
        }
    }
}