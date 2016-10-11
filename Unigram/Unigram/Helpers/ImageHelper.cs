using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;

namespace Unigram.Helpers
{
    public static class ImageHelper
    {
        /// <summary>
        /// Resizes and crops source file image so that resized image width/height are not larger than <param name="requestedMinSide"></param>
        /// </summary>
        /// <param name="sourceFile">Source StorageFile</param>
        /// <param name="requestedMinSide">Width/Height of the output image</param>
        /// <param name="resizedImageFile">Target StorageFile</param>
        /// <returns></returns>
        public static async Task<StorageFile> ScaleJpegAsync(StorageFile sourceFile, int requestedMinSide, StorageFile resizedImageFile)
        {
            using (var imageStream = await sourceFile.OpenReadAsync())
            {
                var decoder = await BitmapDecoder.CreateAsync(imageStream);
                var originalPixelWidth = decoder.PixelWidth;
                var originalPixelHeight = decoder.PixelHeight;

                using (var resizedStream = await resizedImageFile.OpenAsync(FileAccessMode.ReadWrite))
                {
                    BitmapTransform transform;

                    if (decoder.PixelWidth > requestedMinSide || decoder.PixelHeight > requestedMinSide)
                    {
                        double widthRatio = (double)requestedMinSide / originalPixelWidth;
                        double heightRatio = (double)requestedMinSide / originalPixelHeight;
                        uint aspectHeight = (uint)requestedMinSide;
                        uint aspectWidth = (uint)requestedMinSide;

                        if (originalPixelWidth > originalPixelHeight)
                        {
                            aspectWidth = (uint)(heightRatio * originalPixelWidth);
                        }
                        else
                        {
                            aspectHeight = (uint)(widthRatio * originalPixelHeight);
                        }

                        transform = new BitmapTransform
                        {
                            ScaledHeight = aspectHeight,
                            ScaledWidth = aspectWidth
                        };
                    }
                    else
                    {
                        transform = new BitmapTransform();
                    }

                    var pixelData = await decoder.GetPixelDataAsync(decoder.BitmapPixelFormat, decoder.BitmapAlphaMode, transform, ExifOrientationMode.RespectExifOrientation, ColorManagementMode.DoNotColorManage);
                    var pixels = pixelData.DetachPixelData();

                    var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.JpegEncoderId, resizedStream);
                    encoder.SetPixelData(decoder.BitmapPixelFormat, BitmapAlphaMode.Premultiplied, decoder.OrientedPixelWidth, decoder.OrientedPixelHeight, decoder.DpiX, decoder.DpiY, pixels);
                    await encoder.FlushAsync();
                }
            }

            return resizedImageFile;
        }
    }
}
