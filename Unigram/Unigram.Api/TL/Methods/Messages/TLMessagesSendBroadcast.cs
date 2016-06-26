// <auto-generated/>
using System;

namespace Telegram.Api.TL.Methods.Messages
{
	/// <summary>
	/// RCP method messages.sendBroadcast
	/// </summary>
	public partial class TLMessagesSendBroadcast : TLObject
	{
		public TLVector<TLInputUserBase> Contacts { get; set; }
		public TLVector<Int64> RandomId { get; set; }
		public String Message { get; set; }
		public TLInputMediaBase Media { get; set; }

		public TLMessagesSendBroadcast() { }
		public TLMessagesSendBroadcast(TLBinaryReader from, TLType type = TLType.MessagesSendBroadcast)
		{
			Read(from, type);
		}

		public override TLType TypeId { get { return TLType.MessagesSendBroadcast; } }

		public override void Read(TLBinaryReader from, TLType type = TLType.MessagesSendBroadcast)
		{
			Contacts = TLFactory.Read<TLVector<TLInputUserBase>>(from);
			RandomId = TLFactory.Read<TLVector<Int64>>(from);
			Message = from.ReadString();
			Media = TLFactory.Read<TLInputMediaBase>(from);
		}

		public override void Write(TLBinaryWriter to)
		{
			to.Write(0xBF73F4DA);
			to.WriteObject(Contacts);
			to.WriteObject(RandomId);
			to.Write(Message);
			to.WriteObject(Media);
		}
	}
}