// <auto-generated/>
using System;

namespace Telegram.Api.TL
{
	public partial class TLInputNotifyAll : TLInputNotifyPeerBase 
	{
		public TLInputNotifyAll() { }
		public TLInputNotifyAll(TLBinaryReader from, TLType type = TLType.InputNotifyAll)
		{
			Read(from, type);
		}

		public override TLType TypeId { get { return TLType.InputNotifyAll; } }

		public override void Read(TLBinaryReader from, TLType type = TLType.InputNotifyAll)
		{
		}

		public override void Write(TLBinaryWriter to)
		{
			to.Write(0xA429B886);
		}
	}
}