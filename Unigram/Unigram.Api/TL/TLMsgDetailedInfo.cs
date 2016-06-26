// <auto-generated/>
using System;

namespace Telegram.Api.TL
{
	public partial class TLMsgDetailedInfo : TLMsgDetailedInfoBase 
	{
		public Int64 MsgId { get; set; }

		public TLMsgDetailedInfo() { }
		public TLMsgDetailedInfo(TLBinaryReader from, TLType type = TLType.MsgDetailedInfo)
		{
			Read(from, type);
		}

		public override TLType TypeId { get { return TLType.MsgDetailedInfo; } }

		public override void Read(TLBinaryReader from, TLType type = TLType.MsgDetailedInfo)
		{
			MsgId = from.ReadInt64();
			AnswerMsgId = from.ReadInt64();
			Bytes = from.ReadInt32();
			Status = from.ReadInt32();
		}

		public override void Write(TLBinaryWriter to)
		{
			to.Write(0x276D3EC6);
			to.Write(MsgId);
			to.Write(AnswerMsgId);
			to.Write(Bytes);
			to.Write(Status);
		}
	}
}