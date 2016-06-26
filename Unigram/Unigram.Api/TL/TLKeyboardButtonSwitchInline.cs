// <auto-generated/>
using System;

namespace Telegram.Api.TL
{
	public partial class TLKeyboardButtonSwitchInline : TLKeyboardButtonBase 
	{
		public String Query { get; set; }

		public TLKeyboardButtonSwitchInline() { }
		public TLKeyboardButtonSwitchInline(TLBinaryReader from, TLType type = TLType.KeyboardButtonSwitchInline)
		{
			Read(from, type);
		}

		public override TLType TypeId { get { return TLType.KeyboardButtonSwitchInline; } }

		public override void Read(TLBinaryReader from, TLType type = TLType.KeyboardButtonSwitchInline)
		{
			Text = from.ReadString();
			Query = from.ReadString();
		}

		public override void Write(TLBinaryWriter to)
		{
			to.Write(0xEA1B7A14);
			to.Write(Text);
			to.Write(Query);
		}
	}
}