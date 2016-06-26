// <auto-generated/>
using System;

namespace Telegram.Api.TL
{
	public partial class TLMessagesPeerDialogs : TLObject 
	{
		public TLVector<TLDialog> Dialogs { get; set; }
		public TLVector<TLMessageBase> Messages { get; set; }
		public TLVector<TLChatBase> Chats { get; set; }
		public TLVector<TLUserBase> Users { get; set; }
		public TLUpdatesState State { get; set; }

		public TLMessagesPeerDialogs() { }
		public TLMessagesPeerDialogs(TLBinaryReader from, TLType type = TLType.MessagesPeerDialogs)
		{
			Read(from, type);
		}

		public override TLType TypeId { get { return TLType.MessagesPeerDialogs; } }

		public override void Read(TLBinaryReader from, TLType type = TLType.MessagesPeerDialogs)
		{
			Dialogs = TLFactory.Read<TLVector<TLDialog>>(from);
			Messages = TLFactory.Read<TLVector<TLMessageBase>>(from);
			Chats = TLFactory.Read<TLVector<TLChatBase>>(from);
			Users = TLFactory.Read<TLVector<TLUserBase>>(from);
			State = TLFactory.Read<TLUpdatesState>(from);
		}

		public override void Write(TLBinaryWriter to)
		{
			to.Write(0x3371C354);
			to.WriteObject(Dialogs);
			to.WriteObject(Messages);
			to.WriteObject(Chats);
			to.WriteObject(Users);
			to.WriteObject(State);
		}
	}
}