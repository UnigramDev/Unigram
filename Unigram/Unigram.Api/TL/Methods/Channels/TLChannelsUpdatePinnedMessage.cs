// <auto-generated/>
using System;

namespace Telegram.Api.TL.Methods.Channels
{
	/// <summary>
	/// RCP method channels.updatePinnedMessage
	/// </summary>
	public partial class TLChannelsUpdatePinnedMessage : TLObject
	{
		[Flags]
		public enum Flag : Int32
		{
			Silent = (1 << 0),
		}

		public bool IsSilent { get { return Flags.HasFlag(Flag.Silent); } set { Flags = value ? (Flags | Flag.Silent) : (Flags & ~Flag.Silent); } }

		public Flag Flags { get; set; }
		public TLInputChannelBase Channel { get; set; }
		public Int32 Id { get; set; }

		public TLChannelsUpdatePinnedMessage() { }
		public TLChannelsUpdatePinnedMessage(TLBinaryReader from, bool cache = false)
		{
			Read(from, cache);
		}

		public override TLType TypeId { get { return TLType.ChannelsUpdatePinnedMessage; } }

		public override void Read(TLBinaryReader from, bool cache = false)
		{
			Flags = (Flag)from.ReadInt32();
			Channel = TLFactory.Read<TLInputChannelBase>(from, cache);
			Id = from.ReadInt32();
			if (cache) ReadFromCache(from);
		}

		public override void Write(TLBinaryWriter to, bool cache = false)
		{
			UpdateFlags();

			to.Write(0xA72DED52);
			to.Write((Int32)Flags);
			to.WriteObject(Channel, cache);
			to.Write(Id);
			if (cache) WriteToCache(to);
		}

		private void UpdateFlags()
		{
		}
	}
}