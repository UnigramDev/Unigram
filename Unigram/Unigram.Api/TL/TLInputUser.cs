// <auto-generated/>
using System;

namespace Telegram.Api.TL
{
	public partial class TLInputUser : TLInputUserBase 
	{
		public Int32 UserId { get; set; }
		public Int64 AccessHash { get; set; }

		public TLInputUser() { }
		public TLInputUser(TLBinaryReader from, TLType type = TLType.InputUser)
		{
			Read(from, type);
		}

		public override TLType TypeId { get { return TLType.InputUser; } }

		public override void Read(TLBinaryReader from, TLType type = TLType.InputUser)
		{
			UserId = from.ReadInt32();
			AccessHash = from.ReadInt64();
		}

		public override void Write(TLBinaryWriter to)
		{
			to.Write(0xD8292816);
			to.Write(UserId);
			to.Write(AccessHash);
		}
	}
}