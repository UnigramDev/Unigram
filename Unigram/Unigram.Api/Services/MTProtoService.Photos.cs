using System;
using Telegram.Api.TL;
using Telegram.Api.TL.Functions.Photos;

namespace Telegram.Api.Services
{
    public partial class MTProtoService
    {
        public void UploadProfilePhotoAsync(TLInputFile file, TLString caption, TLInputGeoPointBase geoPoint, TLInputPhotoCropBase crop, Action<TLPhotosPhoto> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLUploadProfilePhoto { File = file, Caption = caption, GeoPoint = geoPoint, Crop = crop };

            SendInformativeMessage("photos.uploadProfilePhoto", obj, callback, faultCallback);
        }

        public void UpdateProfilePhotoAsync(TLInputPhotoBase id, TLInputPhotoCropBase crop, Action<TLPhotoBase> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLUpdateProfilePhoto{ Id = id, Crop = crop };

            SendInformativeMessage("photos.updateProfilePhoto", obj, callback, faultCallback);
        }

        public void GetUserPhotosAsync(TLInputUserBase userId, TLInt offset, TLLong maxId, TLInt limit, Action<TLPhotosBase> callback, Action<TLRPCError> faultCallback = null)
        {
            var obj = new TLGetUserPhotos { UserId = userId, Offset = offset, MaxId = maxId, Limit = limit };

            SendInformativeMessage("photos.getUserPhotos", obj, callback, faultCallback);
        }
    }
}
