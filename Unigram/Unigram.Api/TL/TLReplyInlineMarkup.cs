// <auto-generated/>
using System;

namespace Telegram.Api.TL
{
	public partial class TLReplyInlineMarkup : TLReplyMarkupBase 
	{

		public TLReplyInlineMarkup() { }
		public TLReplyInlineMarkup(TLBinaryReader from, TLType type = TLType.ReplyInlineMarkup)
		{
			Read(from, type);
		}

		public override TLType TypeId { get { return TLType.ReplyInlineMarkup; } }

		public override void Read(TLBinaryReader from, TLType type = TLType.ReplyInlineMarkup)
		{
			Rows = TLFactory.Read<TLVector<TLKeyboardButtonRow>>(from);
		}

		public override void Write(TLBinaryWriter to)
		{
			to.Write(0x48A30254);
			to.WriteObject(Rows);
		}
	}
}