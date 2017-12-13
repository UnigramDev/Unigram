using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Telegram.Api.TL
{
    public abstract partial class TLChatPhotoBase
    {
        public virtual void Update(TLChatPhotoBase photo)
        {
        }
    }

    public partial class TLChatPhoto
    {
        public override void Update(TLChatPhotoBase photo)
        {
            var photoCommon = photo as TLChatPhoto;
            if (photoCommon != null)
            {
                if (PhotoSmall != null)
                {
                    PhotoSmall.Update(photoCommon.PhotoSmall);
                }
                else
                {
                    PhotoSmall = photoCommon.PhotoSmall;
                }
                if (PhotoBig != null)
                {
                    PhotoBig.Update(photoCommon.PhotoBig);
                }
                else
                {
                    PhotoBig = photoCommon.PhotoBig;
                }
            }
        }
    }

    public partial class TLChatPhotoEmpty
    {
    }
}
