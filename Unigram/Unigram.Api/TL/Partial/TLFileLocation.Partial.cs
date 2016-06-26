using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Telegram.Api.TL
{
    public partial class TLFileLocation
    {
        public TLInputFileLocation ToInputFileLocation()
        {
            return new TLInputFileLocation
            {
                LocalId = LocalId,
                Secret = Secret,
                VolumeId = VolumeId
            };
        }
    }
}
