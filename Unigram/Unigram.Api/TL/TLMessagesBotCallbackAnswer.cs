// <auto-generated/>
using System;

namespace Telegram.Api.TL
{
	public partial class TLMessagesBotCallbackAnswer : TLObject 
	{
		[Flags]
		public enum Flag : Int32
		{
			Alert = (1 << 1),
			HasUrl = (1 << 3),
			Message = (1 << 0),
			Url = (1 << 2),
		}

		public bool IsAlert { get { return Flags.HasFlag(Flag.Alert); } set { Flags = value ? (Flags | Flag.Alert) : (Flags & ~Flag.Alert); } }
		public bool IsHasUrl { get { return Flags.HasFlag(Flag.HasUrl); } set { Flags = value ? (Flags | Flag.HasUrl) : (Flags & ~Flag.HasUrl); } }
		public bool HasMessage { get { return Flags.HasFlag(Flag.Message); } set { Flags = value ? (Flags | Flag.Message) : (Flags & ~Flag.Message); } }
		public bool HasUrl { get { return Flags.HasFlag(Flag.Url); } set { Flags = value ? (Flags | Flag.Url) : (Flags & ~Flag.Url); } }

		public Flag Flags { get; set; }
		public String Message { get; set; }
		public String Url { get; set; }

		public TLMessagesBotCallbackAnswer() { }
		public TLMessagesBotCallbackAnswer(TLBinaryReader from, bool cache = false)
		{
			Read(from, cache);
		}

		public override TLType TypeId { get { return TLType.MessagesBotCallbackAnswer; } }

		public override void Read(TLBinaryReader from, bool cache = false)
		{
			Flags = (Flag)from.ReadInt32();
			if (HasMessage) Message = from.ReadString();
			if (HasUrl) Url = from.ReadString();
			if (cache) ReadFromCache(from);
		}

		public override void Write(TLBinaryWriter to, bool cache = false)
		{
			UpdateFlags();

			to.Write(0xB10DF1FB);
			to.Write((Int32)Flags);
			if (HasMessage) to.Write(Message);
			if (HasUrl) to.Write(Url);
			if (cache) WriteToCache(to);
		}

		private void UpdateFlags()
		{
			HasMessage = Message != null;
			HasUrl = Url != null;
		}
	}
}