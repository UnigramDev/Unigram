// <auto-generated/>
using System;

namespace Telegram.Api.TL.Methods.Contacts
{
	/// <summary>
	/// RCP method contacts.importContacts
	/// </summary>
	public partial class TLContactsImportContacts : TLObject
	{
		public TLVector<TLInputContactBase> Contacts { get; set; }
		public Boolean Replace { get; set; }

		public TLContactsImportContacts() { }
		public TLContactsImportContacts(TLBinaryReader from, TLType type = TLType.ContactsImportContacts)
		{
			Read(from, type);
		}

		public override TLType TypeId { get { return TLType.ContactsImportContacts; } }

		public override void Read(TLBinaryReader from, TLType type = TLType.ContactsImportContacts)
		{
			Contacts = TLFactory.Read<TLVector<TLInputContactBase>>(from);
			Replace = from.ReadBoolean();
		}

		public override void Write(TLBinaryWriter to)
		{
			to.Write(0xDA30B32D);
			to.WriteObject(Contacts);
			to.Write(Replace);
		}
	}
}