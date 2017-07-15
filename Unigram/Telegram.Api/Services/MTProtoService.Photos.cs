using System;
using Telegram.Api.Native.TL;
using Telegram.Api.TL;
using Telegram.Api.TL.Photos;
using Telegram.Api.TL.Photos.Methods;

namespace Telegram.Api.Services
{
    public partial class MTProtoService
    {
        public void UploadProfilePhotoAsync(TLInputFile file, Action<TLPhotosPhoto> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLPhotosUploadProfilePhoto { File = file };

            const string caption = "photos.uploadProfilePhoto";
            SendInformativeMessage(caption, obj, callback, faultCallback);
        }

        public void UpdateProfilePhotoAsync(TLInputPhotoBase id, Action<TLUserProfilePhotoBase> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLPhotosUpdateProfilePhoto { Id = id };

            const string caption = "photos.updateProfilePhoto";
            SendInformativeMessage(caption, obj, callback, faultCallback);
        }

        public void GetUserPhotosAsync(TLInputUserBase userId, int offset, long maxId, int limit, Action<TLPhotosPhotosBase> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLPhotosGetUserPhotos { UserId = userId, Offset = offset, MaxId = maxId, Limit = limit };

            const string caption = "photos.getUserPhotos";
            SendInformativeMessage(caption, obj, callback, faultCallback);
        }

        public void DeletePhotosAsync(TLVector<TLInputPhotoBase> id, Action<TLVector<long>> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLPhotosDeletePhotos { Id = id };

            const string caption = "photos.deletePhotos";
            SendInformativeMessage(caption, obj, callback, faultCallback);
        }
    }
}
