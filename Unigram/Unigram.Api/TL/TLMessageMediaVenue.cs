// <auto-generated/>
using System;

namespace Telegram.Api.TL
{
	public partial class TLMessageMediaVenue : TLMessageMediaBase 
	{
		public String Title { get; set; }
		public String Address { get; set; }
		public String Provider { get; set; }
		public String VenueId { get; set; }

		public TLMessageMediaVenue() { }
		public TLMessageMediaVenue(TLBinaryReader from, TLType type = TLType.MessageMediaVenue)
		{
			Read(from, type);
		}

		public override TLType TypeId { get { return TLType.MessageMediaVenue; } }

		public override void Read(TLBinaryReader from, TLType type = TLType.MessageMediaVenue)
		{
			Geo = TLFactory.Read<TLGeoPointBase>(from);
			Title = from.ReadString();
			Address = from.ReadString();
			Provider = from.ReadString();
			VenueId = from.ReadString();
		}

		public override void Write(TLBinaryWriter to)
		{
			to.Write(0x7912B71F);
			to.WriteObject(Geo);
			to.Write(Title);
			to.Write(Address);
			to.Write(Provider);
			to.Write(VenueId);
		}
	}
}