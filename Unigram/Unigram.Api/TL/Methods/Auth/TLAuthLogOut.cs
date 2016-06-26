// <auto-generated/>
using System;

namespace Telegram.Api.TL.Methods.Auth
{
	/// <summary>
	/// RCP method auth.logOut
	/// </summary>
	public partial class TLAuthLogOut : TLObject
	{
		public TLAuthLogOut() { }
		public TLAuthLogOut(TLBinaryReader from, TLType type = TLType.AuthLogOut)
		{
			Read(from, type);
		}

		public override TLType TypeId { get { return TLType.AuthLogOut; } }

		public override void Read(TLBinaryReader from, TLType type = TLType.AuthLogOut)
		{
		}

		public override void Write(TLBinaryWriter to)
		{
			to.Write(0x5717DA40);
		}
	}
}