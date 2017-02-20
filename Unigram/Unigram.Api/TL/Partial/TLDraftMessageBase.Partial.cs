using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.TL.Methods.Messages;

namespace Telegram.Api.TL
{
    public abstract partial class TLDraftMessageBase
    {
        public abstract TLMessagesSaveDraft ToSaveDraftObject(TLInputPeerBase peer);
    }

    public partial class TLDraftMessageEmpty
    {
        public override TLMessagesSaveDraft ToSaveDraftObject(TLInputPeerBase peer)
        {
            return new TLMessagesSaveDraft
            {
                Peer = peer,
                Message = string.Empty
            };
        }
    }

    public partial class TLDraftMessage
    {
        public override TLMessagesSaveDraft ToSaveDraftObject(TLInputPeerBase peer)
        {
            var obj = new TLMessagesSaveDraft
            {
                ReplyToMsgId = ReplyToMsgId,
                Peer = peer,
                Message = Message,
                Entities = Entities,
            };

            if (IsNoWebPage)
            {
                obj.IsNoWebPage = true;
            }

            return obj;
        }
    }
}
