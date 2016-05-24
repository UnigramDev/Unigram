using System;
using System.IO;
using Telegram.Api.Extensions;

namespace Telegram.Api.TL
{
    [Flags]
    public enum ChatInviteFlags
    {
        Channel = 0x1,
        Broadcast = 0x2,
        Public = 0x4,
    }

    public abstract class TLChatInviteBase : TLObject { }

    public abstract class TLExportedChatInvite : TLChatInviteBase { }

    public class TLChatInviteEmpty : TLExportedChatInvite
    {
        public const uint Signature = TLConstructors.TLChatInviteEmpty;

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

    public class TLChatInviteExported : TLExportedChatInvite
    {
        public const uint Signature = TLConstructors.TLChatInviteExported;

        public TLString Link { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Link = GetObject<TLString>(bytes, ref position);

            return this;
        }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Link.ToBytes());
        }

        public override TLObject FromStream(Stream input)
        {
            Link = GetObject<TLString>(input);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            output.Write(Link.ToBytes());
        }
    }

    public class TLChatInviteAlready : TLChatInviteBase
    {
        public const uint Signature = TLConstructors.TLChatInviteAlready;

        public TLChatBase Chat { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Chat = GetObject<TLChatBase>(bytes, ref position);

            return this;
        }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Chat.ToBytes());
        }

        public override TLObject FromStream(Stream input)
        {
            Chat = GetObject<TLChatBase>(input);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            output.Write(Chat.ToBytes());
        }
    }

    public class TLChatInvite : TLChatInviteBase
    {
        public const uint Signature = TLConstructors.TLChatInvite;

        public TLString Title { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Title = GetObject<TLString>(bytes, ref position);

            return this;
        }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Title.ToBytes());
        }

        public override TLObject FromStream(Stream input)
        {
            Title = GetObject<TLString>(input);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            output.Write(Title.ToBytes());
        }
    }

    public class TLChatInvite40 : TLChatInvite
    {
        public new const uint Signature = TLConstructors.TLChatInvite40;

        public TLInt Flags { get; set; }

        public bool IsChannel { get { return IsSet(Flags, (int) ChatInviteFlags.Channel); } }

        public bool IsBroadcast { get { return IsSet(Flags, (int)ChatInviteFlags.Broadcast); } }

        public bool IsPublic { get { return IsSet(Flags, (int)ChatInviteFlags.Public); } }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Flags = GetObject<TLInt>(bytes, ref position);
            Title = GetObject<TLString>(bytes, ref position);

            return this;
        }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Flags.ToBytes(),
                Title.ToBytes());
        }

        public override TLObject FromStream(Stream input)
        {
            Flags = GetObject<TLInt>(input);
            Title = GetObject<TLString>(input);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            output.Write(Flags.ToBytes());
            output.Write(Title.ToBytes());
        }
    }
}
