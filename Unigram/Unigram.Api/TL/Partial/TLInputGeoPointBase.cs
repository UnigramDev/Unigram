using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Telegram.Api.TL
{
    public partial class TLInputGeoPointBase
    {
        public virtual TLGeoPointBase ToGeoPoint()
        {
            throw new NotImplementedException();
        }
    }

    public partial class TLInputGeoPoint
    {
        public override TLGeoPointBase ToGeoPoint()
        {
            return new TLGeoPoint { Lat = Lat, Long = Long };
        }
    }

    public partial class TLInputGeoPointEmpty
    {
        public override TLGeoPointBase ToGeoPoint()
        {
            return new TLGeoPointEmpty();
        }
    }
}
