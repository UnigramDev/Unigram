using System.IO;
using Telegram.Api.Extensions;

namespace Telegram.Api.TL.Functions.Messages
{
    public class TLSendMedia : TLObject, IRandomId
    {
        public const uint Signature = 0xc8f16791;

        private TLInt _flags;

        public TLInt Flags
        {
            get { return _flags; }
            set { _flags = value; }
        }

        public TLInputPeerBase Peer { get; set; }

        private TLInt _replyToMsgId;

        public TLInt ReplyToMsgId
        {
            get { return _replyToMsgId; }
            set
            {
                _replyToMsgId = value;
                if (_replyToMsgId != null && _replyToMsgId.Value > 0)
                {
                    Set(ref _flags, (int)SendFlags.Reply);
                }
                else
                {
                    Unset(ref _flags, (int)SendFlags.Reply);
                }
            }
        }

        public TLInputMediaBase Media { get; set; }

        public TLLong RandomId { get; set; }

        private TLReplyKeyboardBase _replyMarkup;

        public TLReplyKeyboardBase ReplyMarkup
        {
            get { return _replyMarkup; }
            set
            {
                _replyMarkup = value;
                if (_replyMarkup != null)
                {
                    Set(ref _flags, (int)SendFlags.ReplyMarkup);
                }
                else
                {
                    Unset(ref _flags, (int)SendFlags.ReplyMarkup);
                }
            }
        }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Flags.ToBytes(),
                Peer.ToBytes(),
                ToBytes(ReplyToMsgId, Flags, (int)SendFlags.Reply),
                Media.ToBytes(),
                RandomId.ToBytes(),
                ToBytes(ReplyMarkup, Flags, (int)SendFlags.ReplyMarkup)
            );
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));

            Flags.ToStream(output);
            Peer.ToStream(output);
            ToStream(output, ReplyToMsgId, Flags, (int)SendFlags.Reply);
            Media.ToStream(output);
            RandomId.ToStream(output);
            ToStream(output, ReplyMarkup, Flags, (int)SendFlags.ReplyMarkup);
        }

        public override TLObject FromStream(Stream input)
        {
            Flags = GetObject<TLInt>(input);
            Peer = GetObject<TLInputPeerBase>(input);
            if (IsSet(Flags, (int)SendFlags.Reply))
            {
                ReplyToMsgId = GetObject<TLInt>(input);
            }
            Media = GetObject<TLInputMediaBase>(input);
            RandomId = GetObject<TLLong>(input);
            if (IsSet(Flags, (int)SendFlags.ReplyMarkup))
            {
                ReplyMarkup = GetObject<TLReplyKeyboardBase>(input);
            }

            return this;
        }

        public void SetChannelMessage()
        {
            Set(ref _flags, (int) SendFlags.Channel);
        }
    }
}
