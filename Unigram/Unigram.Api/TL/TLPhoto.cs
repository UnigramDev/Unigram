// <auto-generated/>
using System;

namespace Telegram.Api.TL
{
	public partial class TLPhoto : TLPhotoBase 
	{
		public Int64 AccessHash { get; set; }
		public Int32 Date { get; set; }
		public TLVector<TLPhotoSizeBase> Sizes { get; set; }

		public TLPhoto() { }
		public TLPhoto(TLBinaryReader from, TLType type = TLType.Photo)
		{
			Read(from, type);
		}

		public override TLType TypeId { get { return TLType.Photo; } }

		public override void Read(TLBinaryReader from, TLType type = TLType.Photo)
		{
			Id = from.ReadInt64();
			AccessHash = from.ReadInt64();
			Date = from.ReadInt32();
			Sizes = TLFactory.Read<TLVector<TLPhotoSizeBase>>(from);
		}

		public override void Write(TLBinaryWriter to)
		{
			to.Write(0xCDED42FE);
			to.Write(Id);
			to.Write(AccessHash);
			to.Write(Date);
			to.WriteObject(Sizes);
		}
	}
}