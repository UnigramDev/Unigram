﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Telegram.Api.TL
{
    public partial class TLUserProfilePhoto
    {
        public override void Update(TLUserProfilePhotoBase photo)
        {
            var photoCommon = photo as TLUserProfilePhoto;
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
}
