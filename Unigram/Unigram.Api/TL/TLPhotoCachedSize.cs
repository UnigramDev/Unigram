// <auto-generated/>
using System;

namespace Telegram.Api.TL
{
	public partial class TLPhotoCachedSize : TLPhotoSizeBase 
	{
		public Byte[] Bytes { get; set; }

		public TLPhotoCachedSize() { }
		public TLPhotoCachedSize(TLBinaryReader from, TLType type = TLType.PhotoCachedSize)
		{
			Read(from, type);
		}

		public override TLType TypeId { get { return TLType.PhotoCachedSize; } }

		public override void Read(TLBinaryReader from, TLType type = TLType.PhotoCachedSize)
		{
			Type = from.ReadString();
			Location = TLFactory.Read<TLFileLocationBase>(from);
			W = from.ReadInt32();
			H = from.ReadInt32();
			Bytes = from.ReadByteArray();
		}

		public override void Write(TLBinaryWriter to)
		{
			to.Write(0xE9A734FA);
			to.Write(Type);
			to.WriteObject(Location);
			to.Write(W);
			to.Write(H);
			to.WriteByteArray(Bytes);
		}
	}
}