// <auto-generated/>
using System;

namespace Telegram.Api.TL
{
	public partial class TLReplyKeyboardMarkup : TLReplyMarkupBase 
	{
		[Flags]
		public enum Flag : Int32
		{
			Resize = (1 << 0),
			SingleUse = (1 << 1),
			Selective = (1 << 2),
		}

		public bool IsResize { get { return Flags.HasFlag(Flag.Resize); } set { Flags = value ? (Flags | Flag.Resize) : (Flags & ~Flag.Resize); } }
		public bool IsSingleUse { get { return Flags.HasFlag(Flag.SingleUse); } set { Flags = value ? (Flags | Flag.SingleUse) : (Flags & ~Flag.SingleUse); } }
		public bool IsSelective { get { return Flags.HasFlag(Flag.Selective); } set { Flags = value ? (Flags | Flag.Selective) : (Flags & ~Flag.Selective); } }

		public Flag Flags { get; set; }

		public TLReplyKeyboardMarkup() { }
		public TLReplyKeyboardMarkup(TLBinaryReader from, bool cache = false)
		{
			Read(from, cache);
		}

		public override TLType TypeId { get { return TLType.ReplyKeyboardMarkup; } }

		public override void Read(TLBinaryReader from, bool cache = false)
		{
			Flags = (Flag)from.ReadInt32();
			Rows = TLFactory.Read<TLVector<TLKeyboardButtonRow>>(from, cache);
			if (cache) ReadFromCache(from);
		}

		public override void Write(TLBinaryWriter to, bool cache = false)
		{
			UpdateFlags();

			to.Write(0x3502758C);
			to.Write((Int32)Flags);
			to.WriteObject(Rows, cache);
			if (cache) WriteToCache(to);
		}

		private void UpdateFlags()
		{
		}
	}
}