using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Telegram.Api.TL
{
    public abstract partial class TLPhoneCallBase
    {
        public virtual TLInputPhoneCall ToInputPhoneCall()
        {
            throw new NotImplementedException();
        }
    }

    public partial class TLPhoneCall
    {
        public override TLInputPhoneCall ToInputPhoneCall()
        {
            return new TLInputPhoneCall { Id = Id, AccessHash = AccessHash };
        }
    }

    public partial class TLPhoneCallAccepted
    {
        public override TLInputPhoneCall ToInputPhoneCall()
        {
            return new TLInputPhoneCall { Id = Id, AccessHash = AccessHash };
        }
    }

    public partial class TLPhoneCallWaiting
    {
        public override TLInputPhoneCall ToInputPhoneCall()
        {
            return new TLInputPhoneCall { Id = Id, AccessHash = AccessHash };
        }
    }

    public partial class TLPhoneCallRequested
    {
        public override TLInputPhoneCall ToInputPhoneCall()
        {
            return new TLInputPhoneCall { Id = Id, AccessHash = AccessHash };
        }
    }
}
