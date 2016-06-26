using System.Collections.Generic;
using Telegram.Api.TL;

namespace Telegram.Api.Services.Updates
{
    //public class UpdatesBySeqComparer : IComparer<TLUpdatesBase>
    //{
    //    private const int XIsLessThanY = -1;
    //    private const int XEqualsY = 0;
    //    private const int XIsGreaterThanY = 1;

    //    public int Compare(TLUpdatesBase x, TLUpdatesBase y)
    //    {
    //        var xSeq = x.GetSeq();
    //        var ySeq = y.GetSeq();

    //        if (xSeq == null && ySeq == null)
    //        {
    //            return x is TLUpdatesShort ? XIsLessThanY : XIsGreaterThanY;
    //        }

    //        if (xSeq == null)
    //        {
    //            return XIsGreaterThanY;
    //        }

    //        if (ySeq == null)
    //        {
    //            return XIsLessThanY;
    //        }

    //        return xSeq.Value < ySeq.Value ? XIsLessThanY : (xSeq == ySeq ? XEqualsY : XIsGreaterThanY);
    //    }
    //}
}