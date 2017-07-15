using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Telegram.Api.TL
{
    public abstract partial class TLGeoPointBase
    {
        public virtual TLInputGeoPointBase ToInputGeoPoint()
        {
            throw new NotImplementedException();
        }
    }

    public partial class TLGeoPoint
    {
        public override TLInputGeoPointBase ToInputGeoPoint()
        {
            return new TLInputGeoPoint { Lat = Lat, Long = Long };
        }
    }

    public partial class TLGeoPointEmpty
    {
        public override TLInputGeoPointBase ToInputGeoPoint()
        {
            return new TLInputGeoPointEmpty();
        }
    }
}
