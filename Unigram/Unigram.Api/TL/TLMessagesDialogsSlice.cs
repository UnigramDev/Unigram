// <auto-generated/>
using System;

namespace Telegram.Api.TL
{
	public partial class TLMessagesDialogsSlice : TLMessagesDialogsBase 
	{
		public Int32 Count { get; set; }

		public TLMessagesDialogsSlice() { }
		public TLMessagesDialogsSlice(TLBinaryReader from, TLType type = TLType.MessagesDialogsSlice)
		{
			Read(from, type);
		}

		public override TLType TypeId { get { return TLType.MessagesDialogsSlice; } }

		public override void Read(TLBinaryReader from, TLType type = TLType.MessagesDialogsSlice)
		{
			Count = from.ReadInt32();
			Dialogs = TLFactory.Read<TLVector<TLDialog>>(from);
			Messages = TLFactory.Read<TLVector<TLMessageBase>>(from);
			Chats = TLFactory.Read<TLVector<TLChatBase>>(from);
			Users = TLFactory.Read<TLVector<TLUserBase>>(from);
		}

		public override void Write(TLBinaryWriter to)
		{
			to.Write(0x71E094F3);
			to.Write(Count);
			to.WriteObject(Dialogs);
			to.WriteObject(Messages);
			to.WriteObject(Chats);
			to.WriteObject(Users);
		}
	}
}