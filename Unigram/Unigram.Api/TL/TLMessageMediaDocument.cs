// <auto-generated/>
using System;

namespace Telegram.Api.TL
{
	public partial class TLMessageMediaDocument : TLMessageMediaBase, ITLMediaCaption 
	{
		public TLDocumentBase Document { get; set; }

		public TLMessageMediaDocument() { }
		public TLMessageMediaDocument(TLBinaryReader from, TLType type = TLType.MessageMediaDocument)
		{
			Read(from, type);
		}

		public override TLType TypeId { get { return TLType.MessageMediaDocument; } }

		public override void Read(TLBinaryReader from, TLType type = TLType.MessageMediaDocument)
		{
			Document = TLFactory.Read<TLDocumentBase>(from);
			Caption = from.ReadString();
		}

		public override void Write(TLBinaryWriter to)
		{
			to.Write(0xF3E02EA8);
			to.WriteObject(Document);
			to.Write(Caption);
		}
	}
}