// <auto-generated/>
using System;

namespace Telegram.Api.TL
{
	public partial class TLInputPeerSelf : TLInputPeerBase 
	{
		public TLInputPeerSelf() { }
		public TLInputPeerSelf(TLBinaryReader from, TLType type = TLType.InputPeerSelf)
		{
			Read(from, type);
		}

		public override TLType TypeId { get { return TLType.InputPeerSelf; } }

		public override void Read(TLBinaryReader from, TLType type = TLType.InputPeerSelf)
		{
		}

		public override void Write(TLBinaryWriter to)
		{
			to.Write(0x7DA07EC9);
		}
	}
}