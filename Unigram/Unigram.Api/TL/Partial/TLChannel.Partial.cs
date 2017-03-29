using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Helpers;

namespace Telegram.Api.TL
{
    public partial class TLChannel : ITLReadMaxId, ITLInputPeer
    {
        public Int32 AdminsCount { get; set; }

        public Int32? ParticipantsCount { get; set; }

        public TLVector<int> ParticipantIds { get; set; }

        public Int32? ReadInboxMaxId { get; set; }

        public Int32? ReadOutboxMaxId { get; set; }

        public Int32? PinnedMsgId { get; set; }

        public Int32? HiddenPinnedMsgId { get; set; }

        public Int32? Pts { get; set; }

        int ITLReadMaxId.ReadInboxMaxId
        {
            get
            {
                return ReadInboxMaxId ?? 0;
            }
            set
            {
                ReadInboxMaxId = value;
            }
        }

        int ITLReadMaxId.ReadOutboxMaxId
        {
            get
            {
                return ReadOutboxMaxId ?? 0;
            }
            set
            {
                ReadOutboxMaxId = value;
            }
        }

        public override void Update(TLChatBase chatBase)
        {
            base.Update(chatBase);

            var channel = chatBase as TLChannel;
            if (channel != null)
            {
                if (channel.ReadInboxMaxId != 0 && (ReadInboxMaxId == 0 || ReadInboxMaxId < channel.ReadInboxMaxId))
                {
                    ReadInboxMaxId = channel.ReadInboxMaxId;
                }
                if (channel.ReadOutboxMaxId != 0 && (ReadOutboxMaxId == 0 || ReadOutboxMaxId < channel.ReadOutboxMaxId))
                {
                    ReadOutboxMaxId = channel.ReadOutboxMaxId;
                }
            }
        }

        public TLInputChannelBase ToInputChannel()
        {
            return new TLInputChannel { ChannelId = Id, AccessHash = AccessHash.Value };
        }

        public string ExtractRestrictionReason()
        {
            if (HasRestrictionReason)
            {
                var fullTypeEnd = RestrictionReason.IndexOf(':');
                if (fullTypeEnd <= 0)
                {
                    return null;
                }

                // {fulltype} is in "{type}-{tag}-{tag}-{tag}" format
                // if we find "all" tag we return the restriction string
                var typeTags = RestrictionReason.Substring(0, fullTypeEnd).Split('-')[1];
#if STORE_RESTRICTIVE
                var restrictionApplies = typeTags.Contains("all") || typeTags.Contains("ios");
#else
                var restrictionApplies = typeTags.Contains("all");
#endif
                if (restrictionApplies)
                {
                    return RestrictionReason.Substring(fullTypeEnd + 1).Trim();
                }
            }

            return null;
        }
    }
}
