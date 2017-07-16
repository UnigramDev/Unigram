using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Telegram.Api.TL
{
    public abstract partial class TLUserProfilePhotoBase
    {
        public virtual void Update(TLUserProfilePhotoBase photo) { }
    }
}
