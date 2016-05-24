using System;
using System.IO;
using Telegram.Api.Extensions;

namespace Telegram.Api.TL.Functions.Messages
{
    [Flags]
    public enum SendFlags
    {
        Reply = 0x1,
        DisableWebPagePreview = 0x2,
        ReplyMarkup = 0x4,
        Entities = 0x8,
        Channel = 0x10
    }

    public interface IRandomId
    {
        TLLong RandomId { get; }
    }

    public class TLSendMessage : TLObject, IRandomId
    {
        public const uint Signature = 0xfa88427a;

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
                    Unset(ref _flags, (int) SendFlags.Reply);
                }
            }
        }

        public TLString Message { get; set; }

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

        private TLVector<TLMessageEntityBase> _entities;

        public TLVector<TLMessageEntityBase> Entities
        {
            get { return _entities; }
            set
            {
                _entities = value;
                if (_entities != null)
                {
                    Set(ref _flags, (int)SendFlags.Entities);
                }
                else
                {
                    Unset(ref _flags, (int)SendFlags.Entities);
                }
            }
        }

        public void DisableWebPagePreview()
        {
            Set(ref _flags, (int)SendFlags.DisableWebPagePreview);
        }

        public void SetChannelMessage()
        {
            Set(ref _flags, (int)SendFlags.Channel);
        }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Flags.ToBytes(),
                Peer.ToBytes(),
                ToBytes(ReplyToMsgId, Flags, (int)SendFlags.Reply),
                Message.ToBytes(),
                RandomId.ToBytes(),
                ToBytes(ReplyMarkup, Flags, (int)SendFlags.ReplyMarkup),
                ToBytes(Entities, Flags, (int)SendFlags.Entities)
                );
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));

            Flags.ToStream(output);
            Peer.ToStream(output);
            ToStream(output, ReplyToMsgId, Flags, (int)SendFlags.Reply);
            Message.ToStream(output);
            RandomId.ToStream(output);
            ToStream(output, ReplyMarkup, Flags, (int)SendFlags.ReplyMarkup);
            ToStream(output, Entities, Flags, (int)SendFlags.Entities);
        }

        public override TLObject FromStream(Stream input)
        {
            Flags = GetObject<TLInt>(input);
            Peer = GetObject<TLInputPeerBase>(input);
            if (IsSet(Flags, (int) SendFlags.Reply))
            {
                ReplyToMsgId = GetObject<TLInt>(input);
            }
            Message = GetObject<TLString>(input);
            RandomId = GetObject<TLLong>(input);
            if (IsSet(Flags, (int)SendFlags.ReplyMarkup))
            {
                ReplyMarkup = GetObject<TLReplyKeyboardBase>(input);
            }
            if (IsSet(Flags, (int)SendFlags.Entities))
            {
                Entities = GetObject<TLVector<TLMessageEntityBase>>(input);
            }

            return this;
        }
    }
}
