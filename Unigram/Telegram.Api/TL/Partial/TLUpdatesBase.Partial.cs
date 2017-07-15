using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Telegram.Api.TL
{
    public abstract partial class TLUpdatesBase
    {
        public abstract IList<int> GetSeq();

        public abstract IList<int> GetPts();
    }

    public partial class TLUpdatesTooLong
    {
        public override IList<int> GetSeq()
        {
            return new List<int>();
        }

        public override IList<int> GetPts()
        {
            return new List<int>();
        }
    }

    public partial class TLUpdateShortSentMessage
    {
        public override IList<int> GetSeq()
        {
            return new List<int>();
        }

        public override IList<int> GetPts()
        {
            return TLUtils.GetPtsRange(Pts, PtsCount);
        }
    }

    public partial class TLUpdateShortMessage
    {
        public bool IsUnread { get; set; } = true;

        public override IList<int> GetSeq()
        {
            return new List<int>();
        }

        public override IList<int> GetPts()
        {
            return TLUtils.GetPtsRange(Pts, PtsCount);
        }
    }

    public partial class TLUpdateShortChatMessage
    {
        public bool IsUnread { get; set; } = true;

        public override IList<int> GetSeq()
        {
            return new List<int>();
        }

        public override IList<int> GetPts()
        {
            return TLUtils.GetPtsRange(Pts, PtsCount);
        }
    }

    public partial class TLUpdateShort
    {
        public override IList<int> GetSeq()
        {
            return new List<int>();
        }

        public override IList<int> GetPts()
        {
            return Update.GetPts();
        }
    }

    public partial class TLUpdates
    {
        public override IList<int> GetSeq()
        {
            return new List<int> { Seq };
        }

        public override IList<int> GetPts()
        {
            return Updates.SelectMany(x => x.GetPts()).ToList();
        }
    }

    public partial class TLUpdatesCombined
    {
        public override IList<int> GetSeq()
        {
            var list = new List<int>();

            for (var i = SeqStart; i <= Seq; i++)
            {
                list.Add(i);
            }

            return list;
        }

        public override IList<int> GetPts()
        {
            return Updates.SelectMany(x => x.GetPts()).ToList();
        }
    }
}
