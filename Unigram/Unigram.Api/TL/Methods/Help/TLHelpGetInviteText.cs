// <auto-generated/>
using System;

namespace Telegram.Api.TL.Methods.Help
{
	/// <summary>
	/// RCP method help.getInviteText
	/// </summary>
	public partial class TLHelpGetInviteText : TLObject
	{
		public TLHelpGetInviteText() { }
		public TLHelpGetInviteText(TLBinaryReader from, TLType type = TLType.HelpGetInviteText)
		{
			Read(from, type);
		}

		public override TLType TypeId { get { return TLType.HelpGetInviteText; } }

		public override void Read(TLBinaryReader from, TLType type = TLType.HelpGetInviteText)
		{
		}

		public override void Write(TLBinaryWriter to)
		{
			to.Write(0x4D392343);
		}
	}
}