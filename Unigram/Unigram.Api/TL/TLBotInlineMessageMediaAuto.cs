// <auto-generated/>
using System;

namespace Telegram.Api.TL
{
	public partial class TLBotInlineMessageMediaAuto : TLBotInlineMessageBase, ITLMediaCaption 
	{
		[Flags]
		public enum Flag : int
		{
			ReplyMarkup = (1 << 2),
		}

		public bool HasReplyMarkup { get { return Flags.HasFlag(Flag.ReplyMarkup); } set { Flags = value ? (Flags | Flag.ReplyMarkup) : (Flags & ~Flag.ReplyMarkup); } }

		public Flag Flags { get; set; }
		public String Caption { get; set; }

		public TLBotInlineMessageMediaAuto() { }
		public TLBotInlineMessageMediaAuto(TLBinaryReader from, TLType type = TLType.BotInlineMessageMediaAuto)
		{
			Read(from, type);
		}

		public override TLType TypeId { get { return TLType.BotInlineMessageMediaAuto; } }

		public override void Read(TLBinaryReader from, TLType type = TLType.BotInlineMessageMediaAuto)
		{
			Flags = (Flag)from.ReadInt32();
			Caption = from.ReadString();
			if (HasReplyMarkup) { ReplyMarkup = TLFactory.Read<TLReplyMarkupBase>(from); }
		}

		public override void Write(TLBinaryWriter to)
		{
			to.Write(0xA74B15B);
			to.Write((Int32)Flags);
			to.Write(Caption);
			if (HasReplyMarkup) to.WriteObject(ReplyMarkup);
		}
	}
}