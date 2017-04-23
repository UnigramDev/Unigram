using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Telegram.Api.TL
{
    public partial class TLPhotoBase
    {
        public virtual TLInputPhotoBase ToInputPhoto()
        {
            throw new NotImplementedException();
        }
    }

    public partial class TLPhoto
    {
        public override TLInputPhotoBase ToInputPhoto()
        {
            return new TLInputPhoto { Id = Id, AccessHash = AccessHash };
        }
    }

    public partial class TLPhotoEmpty
    {
        public override TLInputPhotoBase ToInputPhoto()
        {
            return new TLInputPhotoEmpty();
        }
    }
}
