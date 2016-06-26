// <auto-generated/>
using System;

namespace Telegram.Api.TL
{
	public partial class TLChatParticipant : TLChatParticipantBase 
	{

		public TLChatParticipant() { }
		public TLChatParticipant(TLBinaryReader from, TLType type = TLType.ChatParticipant)
		{
			Read(from, type);
		}

		public override TLType TypeId { get { return TLType.ChatParticipant; } }

		public override void Read(TLBinaryReader from, TLType type = TLType.ChatParticipant)
		{
			UserId = from.ReadInt32();
			InviterId = from.ReadInt32();
			Date = from.ReadInt32();
		}

		public override void Write(TLBinaryWriter to)
		{
			to.Write(0xC8D7493E);
			to.Write(UserId);
			to.Write(InviterId);
			to.Write(Date);
		}
	}
}