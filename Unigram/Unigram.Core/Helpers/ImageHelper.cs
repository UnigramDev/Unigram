using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;

namespace Unigram.Core.Helpers
{
    public static class ImageHelper
    {
        /// <summary>
        /// Resizes and crops source file image so that resized image width/height are not larger than <param name="requestedMinSide"></param>
        /// </summary>
        /// <param name="sourceFile">Source StorageFile</param>
        /// <param name="resizedImageFile">Target StorageFile</param>
        /// <param name="requestedMinSide">Max width/height of the output image</param>
        /// <param name="quality">JPEG compression quality (0.77 for pictures, 0.87 for thumbnails)</param>
        /// <returns></returns>
        public static async Task<StorageFile> ScaleJpegAsync(StorageFile sourceFile, StorageFile resizedImageFile, int requestedMinSide = 1280, double quality = 0.77)
        {
            using (var imageStream = await sourceFile.OpenReadAsync())
            {
                var decoder = await BitmapDecoder.CreateAsync(imageStream);
                if (decoder.FrameCount > 1)
                {
                    throw new InvalidCastException();
                }

                var originalPixelWidth = decoder.PixelWidth;
                var originalPixelHeight = decoder.PixelHeight;

                using (var resizedStream = await resizedImageFile.OpenAsync(FileAccessMode.ReadWrite))
                {
                    BitmapTransform transform;

                    if (decoder.PixelWidth > requestedMinSide || decoder.PixelHeight > requestedMinSide)
                    {
                        double ratioX = (double)requestedMinSide / originalPixelWidth;
                        double ratioY = (double)requestedMinSide / originalPixelHeight;
                        double ratio = Math.Min(ratioX, ratioY);

                        uint width = (uint)(originalPixelWidth * ratio);
                        uint height = (uint)(originalPixelHeight * ratio);

                        transform = new BitmapTransform
                        {
                            ScaledWidth = width,
                            ScaledHeight = height
                        };
                    }
                    else
                    {
                        transform = new BitmapTransform();
                    }

                    var pixelData = await decoder.GetSoftwareBitmapAsync(decoder.BitmapPixelFormat, decoder.BitmapAlphaMode, transform, ExifOrientationMode.RespectExifOrientation, ColorManagementMode.DoNotColorManage);

                    var propertySet = new BitmapPropertySet();
                    var qualityValue = new BitmapTypedValue(quality, Windows.Foundation.PropertyType.Single);
                    propertySet.Add("ImageQuality", qualityValue);

                    var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.JpegEncoderId, resizedStream);
                    encoder.SetSoftwareBitmap(pixelData);
                    await encoder.FlushAsync();
                }
            }

            return resizedImageFile;
        }

        public static async Task<ImageSource> GetPreviewBitmapAsync(StorageFile sourceFile, int requestedMinSide = 1280)
        {
            using (var imageStream = await sourceFile.OpenReadAsync())
            {
                var decoder = await BitmapDecoder.CreateAsync(imageStream);
                if (decoder.BitmapPixelFormat == BitmapPixelFormat.Bgra8)
                {
                    var originalPixelWidth = decoder.PixelWidth;
                    var originalPixelHeight = decoder.PixelHeight;

                    BitmapTransform transform;

                    if (decoder.PixelWidth > requestedMinSide || decoder.PixelHeight > requestedMinSide)
                    {
                        double ratioX = (double)requestedMinSide / originalPixelWidth;
                        double ratioY = (double)requestedMinSide / originalPixelHeight;
                        double ratio = Math.Min(ratioX, ratioY);

                        uint width = (uint)(originalPixelWidth * ratio);
                        uint height = (uint)(originalPixelHeight * ratio);

                        transform = new BitmapTransform
                        {
                            ScaledWidth = width,
                            ScaledHeight = height
                        };
                    }
                    else
                    {
                        transform = new BitmapTransform();
                    }

                    var bitmap = await decoder.GetSoftwareBitmapAsync(decoder.BitmapPixelFormat, BitmapAlphaMode.Premultiplied, transform, ExifOrientationMode.RespectExifOrientation, ColorManagementMode.DoNotColorManage);
                    var bitmapImage = new SoftwareBitmapSource();
                    await bitmapImage.SetBitmapAsync(bitmap);

                    return bitmapImage;
                }
                else
                {
                    var bitmap = new BitmapImage();
                    await bitmap.SetSourceAsync(imageStream);

                    return bitmap;
                }
            }
        }

        public static async Task<Color[]> GetAccentAsync(StorageFile sourceFile)
        {
            var result = new Color[2];

            using (var imageStream = await sourceFile.OpenReadAsync())
            {
                var decoder = await BitmapDecoder.CreateAsync(imageStream);
                var transform = new BitmapTransform
                {
                    ScaledWidth = 1,
                    ScaledHeight = 1
                };

                var pixelData = await decoder.GetPixelDataAsync(decoder.BitmapPixelFormat, decoder.BitmapAlphaMode, transform, ExifOrientationMode.RespectExifOrientation, ColorManagementMode.DoNotColorManage);
                var detach = pixelData.DetachPixelData();

                var hsv = ColorsHelper.RgbToHsv(detach[2], detach[1], detach[0]);
                hsv[1] = Math.Min(1.0, hsv[1] + 0.05 + 0.1 * (1.0 - hsv[1]));
                hsv[2] = Math.Max(0, hsv[2] * 0.65);

                var rgb = ColorsHelper.HsvToRgb(hsv[0], hsv[1], hsv[2]);
                result[0] = Color.FromArgb(0x66, rgb[0], rgb[1], rgb[2]);
                result[1] = Color.FromArgb(0x88, rgb[0], rgb[1], rgb[2]);
            }

            return result;
        }
    }
}
