using System;
using Telegram.Api.TL;
using Telegram.Api.TL.Methods.Photos;

namespace Telegram.Api.Services
{
    public partial class MTProtoService
    {
        public void UploadProfilePhotoCallback(TLInputFile file, Action<TLPhotosPhoto> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLPhotosUploadProfilePhoto { File = file };

            SendInformativeMessage("photos.uploadProfilePhoto", obj, callback, faultCallback);
        }

        public void UpdateProfilePhotoCallback(TLInputPhotoBase id, Action<TLPhotoBase> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLPhotosUpdateProfilePhoto { Id = id };

            SendInformativeMessage("photos.updateProfilePhoto", obj, callback, faultCallback);
        }

        public void GetUserPhotosCallback(TLInputUserBase userId, int offset, long maxId, int limit, Action<TLPhotosPhotosBase> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLPhotosGetUserPhotos { UserId = userId, Offset = offset, MaxId = maxId, Limit = limit };

            SendInformativeMessage("photos.getUserPhotos", obj, callback, faultCallback);
        }
    }
}
