// <auto-generated/>
using System;

namespace Telegram.Api.TL.Methods.Messages
{
	/// <summary>
	/// RCP method messages.getAllDrafts
	/// </summary>
	public partial class TLMessagesGetAllDrafts : TLObject
	{
		public TLMessagesGetAllDrafts() { }
		public TLMessagesGetAllDrafts(TLBinaryReader from, TLType type = TLType.MessagesGetAllDrafts)
		{
			Read(from, type);
		}

		public override TLType TypeId { get { return TLType.MessagesGetAllDrafts; } }

		public override void Read(TLBinaryReader from, TLType type = TLType.MessagesGetAllDrafts)
		{
		}

		public override void Write(TLBinaryWriter to)
		{
			to.Write(0x6A3F8D65);
		}
	}
}