// <auto-generated/>
using System;

namespace Telegram.Api.TL.Methods.Auth
{
	/// <summary>
	/// RCP method auth.bindTempAuthKey
	/// </summary>
	public partial class TLAuthBindTempAuthKey : TLObject
	{
		public Int64 PermAuthKeyId { get; set; }
		public Int64 Nonce { get; set; }
		public Int32 ExpiresAt { get; set; }
		public Byte[] EncryptedMessage { get; set; }

		public TLAuthBindTempAuthKey() { }
		public TLAuthBindTempAuthKey(TLBinaryReader from, TLType type = TLType.AuthBindTempAuthKey)
		{
			Read(from, type);
		}

		public override TLType TypeId { get { return TLType.AuthBindTempAuthKey; } }

		public override void Read(TLBinaryReader from, TLType type = TLType.AuthBindTempAuthKey)
		{
			PermAuthKeyId = from.ReadInt64();
			Nonce = from.ReadInt64();
			ExpiresAt = from.ReadInt32();
			EncryptedMessage = from.ReadByteArray();
		}

		public override void Write(TLBinaryWriter to)
		{
			to.Write(0xCDD42A05);
			to.Write(PermAuthKeyId);
			to.Write(Nonce);
			to.Write(ExpiresAt);
			to.WriteByteArray(EncryptedMessage);
		}
	}
}