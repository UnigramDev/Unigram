// <auto-generated/>
using System;

namespace Telegram.Api.TL
{
	public partial class TLEncryptedMessage : TLEncryptedMessageBase 
	{
		public TLEncryptedFileBase File { get; set; }

		public TLEncryptedMessage() { }
		public TLEncryptedMessage(TLBinaryReader from, TLType type = TLType.EncryptedMessage)
		{
			Read(from, type);
		}

		public override TLType TypeId { get { return TLType.EncryptedMessage; } }

		public override void Read(TLBinaryReader from, TLType type = TLType.EncryptedMessage)
		{
			RandomId = from.ReadInt64();
			ChatId = from.ReadInt32();
			Date = from.ReadInt32();
			Bytes = from.ReadByteArray();
			File = TLFactory.Read<TLEncryptedFileBase>(from);
		}

		public override void Write(TLBinaryWriter to)
		{
			to.Write(0xED18C118);
			to.Write(RandomId);
			to.Write(ChatId);
			to.Write(Date);
			to.WriteByteArray(Bytes);
			to.WriteObject(File);
		}
	}
}