using System;
using System.Threading.Tasks;
using Telegram.Api.TL;
using Telegram.Api.TL.Methods.Photos;

namespace Telegram.Api.Services
{
    public partial class MTProtoService
    {
        // TODO: missing TLPhotosDeletePhotos

        public Task<MTProtoResponse<TLPhotosPhotosBase>> GetUserPhotosAsync(TLInputUserBase userId, int offset, long maxId, int limit)
        {
            return SendInformativeMessage<TLPhotosPhotosBase>("photos.getUserPhotos", new TLPhotosGetUserPhotos { UserId = userId, Offset = offset, MaxId = maxId, Limit = limit });
        }

        public Task<MTProtoResponse<TLPhotoBase>> UpdateProfilePhotoAsync(TLInputPhotoBase id, TLInputPhotoCropBase crop)
        {
            return SendInformativeMessage<TLPhotoBase>("photos.updateProfilePhoto", new TLPhotosUpdateProfilePhoto { Id = id, Crop = crop });
        }

        public Task<MTProtoResponse<TLPhotosPhoto>> UploadProfilePhotoAsync(TLInputFile file, string caption, TLInputGeoPointBase geoPoint, TLInputPhotoCropBase crop)
        {
            return SendInformativeMessage<TLPhotosPhoto>("photos.uploadProfilePhoto", new TLPhotosUploadProfilePhoto { File = file, Caption = caption, GeoPoint = geoPoint, Crop = crop });
        }
    }
}
