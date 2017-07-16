using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Telegram.Api.TL
{
    public abstract partial class TLFileLocationBase
    {
        public virtual void Update(TLFileLocationBase fileLocation)
        {
            if (fileLocation != null)
            {
                //if (Buffer == null || LocalId.Value != fileLocation.LocalId.Value)
                //{
                //    Buffer = fileLocation.Buffer;
                //}

                VolumeId = fileLocation.VolumeId;
                LocalId = fileLocation.LocalId;
                Secret = fileLocation.Secret;
            }
        }
    }
}
