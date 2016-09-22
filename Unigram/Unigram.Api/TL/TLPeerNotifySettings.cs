// <auto-generated/>
using System;

namespace Telegram.Api.TL
{
	public partial class TLPeerNotifySettings : TLPeerNotifySettingsBase 
	{
		[Flags]
		public enum Flag : Int32
		{
			ShowPreviews = (1 << 0),
			Silent = (1 << 1),
		}

		public bool IsShowPreviews { get { return Flags.HasFlag(Flag.ShowPreviews); } set { Flags = value ? (Flags | Flag.ShowPreviews) : (Flags & ~Flag.ShowPreviews); } }
		public bool IsSilent { get { return Flags.HasFlag(Flag.Silent); } set { Flags = value ? (Flags | Flag.Silent) : (Flags & ~Flag.Silent); } }

		public Flag Flags { get; set; }
		public Int32 MuteUntil { get; set; }
		public String Sound { get; set; }

		public TLPeerNotifySettings() { }
		public TLPeerNotifySettings(TLBinaryReader from, bool cache = false)
		{
			Read(from, cache);
		}

		public override TLType TypeId { get { return TLType.PeerNotifySettings; } }

		public override void Read(TLBinaryReader from, bool cache = false)
		{
			Flags = (Flag)from.ReadInt32();
			MuteUntil = from.ReadInt32();
			Sound = from.ReadString();
			if (cache) ReadFromCache(from);
		}

		public override void Write(TLBinaryWriter to, bool cache = false)
		{
			UpdateFlags();

			to.Write(0x9ACDA4C0);
			to.Write((Int32)Flags);
			to.Write(MuteUntil);
			to.Write(Sound);
			if (cache) WriteToCache(to);
		}

		private void UpdateFlags()
		{
		}
	}
}