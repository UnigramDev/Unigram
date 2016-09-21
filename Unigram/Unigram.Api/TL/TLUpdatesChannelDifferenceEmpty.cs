// <auto-generated/>
using System;

namespace Telegram.Api.TL
{
	public partial class TLUpdatesChannelDifferenceEmpty : TLUpdatesChannelDifferenceBase 
	{
		[Flags]
		public enum Flag : Int32
		{
			Final = (1 << 0),
			Timeout = (1 << 1),
		}

		public bool IsFinal { get { return Flags.HasFlag(Flag.Final); } set { Flags = value ? (Flags | Flag.Final) : (Flags & ~Flag.Final); } }
		public bool HasTimeout { get { return Flags.HasFlag(Flag.Timeout); } set { Flags = value ? (Flags | Flag.Timeout) : (Flags & ~Flag.Timeout); } }

		public Flag Flags { get; set; }

		public TLUpdatesChannelDifferenceEmpty() { }
		public TLUpdatesChannelDifferenceEmpty(TLBinaryReader from, bool cache = false)
		{
			Read(from, cache);
		}

		public override TLType TypeId { get { return TLType.UpdatesChannelDifferenceEmpty; } }

		public override void Read(TLBinaryReader from, bool cache = false)
		{
			Flags = (Flag)from.ReadInt32();
			Pts = from.ReadInt32();
			if (HasTimeout) Timeout = from.ReadInt32();
			if (cache) ReadFromCache(from);
		}

		public override void Write(TLBinaryWriter to, bool cache = false)
		{
			UpdateFlags();

			to.Write(0x3E11AFFB);
			to.Write((Int32)Flags);
			to.Write(Pts);
			if (HasTimeout) to.Write(Timeout.Value);
			if (cache) WriteToCache(to);
		}

		private void UpdateFlags()
		{
			HasTimeout = Timeout != null;
		}
	}
}