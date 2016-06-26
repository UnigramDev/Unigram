// <auto-generated/>
using System;

namespace Telegram.Api.TL
{
	public partial class TLSendMessageUploadDocumentAction : TLSendMessageActionBase 
	{

		public TLSendMessageUploadDocumentAction() { }
		public TLSendMessageUploadDocumentAction(TLBinaryReader from, TLType type = TLType.SendMessageUploadDocumentAction)
		{
			Read(from, type);
		}

		public override TLType TypeId { get { return TLType.SendMessageUploadDocumentAction; } }

		public override void Read(TLBinaryReader from, TLType type = TLType.SendMessageUploadDocumentAction)
		{
			Progress = from.ReadInt32();
		}

		public override void Write(TLBinaryWriter to)
		{
			to.Write(0xAA0CD9E4);
			to.Write(Progress);
		}
	}
}