// <auto-generated/>
using System;

namespace Telegram.Api.TL.Methods.Account
{
	/// <summary>
	/// RCP method account.deleteAccount
	/// </summary>
	public partial class TLAccountDeleteAccount : TLObject
	{
		public String Reason { get; set; }

		public TLAccountDeleteAccount() { }
		public TLAccountDeleteAccount(TLBinaryReader from, TLType type = TLType.AccountDeleteAccount)
		{
			Read(from, type);
		}

		public override TLType TypeId { get { return TLType.AccountDeleteAccount; } }

		public override void Read(TLBinaryReader from, TLType type = TLType.AccountDeleteAccount)
		{
			Reason = from.ReadString();
		}

		public override void Write(TLBinaryWriter to)
		{
			to.Write(0x418D4E0B);
			to.Write(Reason);
		}
	}
}