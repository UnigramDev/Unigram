// <auto-generated/>
using System;

namespace Telegram.Api.TL.Methods.Contacts
{
	/// <summary>
	/// RCP method contacts.resolveUsername
	/// </summary>
	public partial class TLContactsResolveUsername : TLObject
	{
		public String Username { get; set; }

		public TLContactsResolveUsername() { }
		public TLContactsResolveUsername(TLBinaryReader from, TLType type = TLType.ContactsResolveUsername)
		{
			Read(from, type);
		}

		public override TLType TypeId { get { return TLType.ContactsResolveUsername; } }

		public override void Read(TLBinaryReader from, TLType type = TLType.ContactsResolveUsername)
		{
			Username = from.ReadString();
		}

		public override void Write(TLBinaryWriter to)
		{
			to.Write(0xF93CCBA3);
			to.Write(Username);
		}
	}
}