// <auto-generated/>
using System;

namespace Telegram.Api.TL.Methods.Account
{
	/// <summary>
	/// RCP method account.getWallPapers
	/// </summary>
	public partial class TLAccountGetWallPapers : TLObject
	{
		public TLAccountGetWallPapers() { }
		public TLAccountGetWallPapers(TLBinaryReader from, TLType type = TLType.AccountGetWallPapers)
		{
			Read(from, type);
		}

		public override TLType TypeId { get { return TLType.AccountGetWallPapers; } }

		public override void Read(TLBinaryReader from, TLType type = TLType.AccountGetWallPapers)
		{
		}

		public override void Write(TLBinaryWriter to)
		{
			to.Write(0xC04CFAC2);
		}
	}
}