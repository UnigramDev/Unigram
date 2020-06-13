using Microsoft.Graphics.Canvas;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Unigram.Charts;
using Unigram.Controls;
using Unigram.Entities;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Media.Editing;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;

namespace Unigram.Common
{
    public static class ImageHelper
    {
        public static async Task<(int Width, int Height)> GetScaleAsync(StorageFile file, int requestedMinSide = 1280, BitmapEditState editState = null)
        {
            var props = await file.Properties.GetImagePropertiesAsync();
            var width = props.Width;
            var height = props.Height;

            if (editState?.Rectangle is Rect crop)
            {
                width = (uint)(crop.Width * props.Width);
                height = (uint)(crop.Height * props.Height);
            }

            if (width > requestedMinSide || height > requestedMinSide)
            {
                double ratioX = (double)requestedMinSide / width;
                double ratioY = (double)requestedMinSide / height;
                double ratio = Math.Min(ratioX, ratioY);

                if (editState?.Rotation == BitmapRotation.Clockwise90Degrees ||
                    editState?.Rotation == BitmapRotation.Clockwise270Degrees)
                {
                    return ((int)(height * ratio), (int)(width * ratio));
                }

                return ((int)(width * ratio), (int)(height * ratio));
            }

            if (editState?.Rotation == BitmapRotation.Clockwise90Degrees ||
                editState?.Rotation == BitmapRotation.Clockwise270Degrees)
            {
                return ((int)height, (int)width);
            }

            return ((int)width, (int)height);
        }



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
                //if (decoder.FrameCount > 1)
                //{
                //    throw new InvalidCastException();
                //}

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
                            ScaledHeight = height,
                            InterpolationMode = BitmapInterpolationMode.Linear
                        };
                    }
                    else
                    {
                        transform = new BitmapTransform();
                    }

                    var pixelData = await decoder.GetSoftwareBitmapAsync(decoder.BitmapPixelFormat, decoder.BitmapAlphaMode, transform, ExifOrientationMode.RespectExifOrientation, ColorManagementMode.DoNotColorManage);

                    // Not using ATM, quality is too low
                    //var propertySet = new BitmapPropertySet();
                    //var qualityValue = new BitmapTypedValue(quality, Windows.Foundation.PropertyType.Single);
                    //propertySet.Add("ImageQuality", qualityValue);

                    var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.JpegEncoderId, resizedStream/*, propertySet*/);
                    encoder.SetSoftwareBitmap(pixelData);
                    await encoder.FlushAsync();
                }
            }

            return resizedImageFile;
        }

        public static async Task<StorageFile> TranscodeAsync(IRandomAccessStream imageStream, StorageFile resizedImageFile, Guid encoderId)
        {
            var decoder = await BitmapDecoder.CreateAsync(imageStream);
            //if (decoder.FrameCount > 1)
            //{
            //    throw new InvalidCastException();
            //}

            var originalPixelWidth = decoder.PixelWidth;
            var originalPixelHeight = decoder.PixelHeight;

            using (var resizedStream = await resizedImageFile.OpenAsync(FileAccessMode.ReadWrite))
            {
                var pixelData = await decoder.GetSoftwareBitmapAsync(decoder.BitmapPixelFormat, decoder.BitmapAlphaMode, new BitmapTransform(), ExifOrientationMode.RespectExifOrientation, ColorManagementMode.DoNotColorManage);

                var encoder = await BitmapEncoder.CreateAsync(encoderId, resizedStream);
                encoder.SetSoftwareBitmap(pixelData);
                await encoder.FlushAsync();
            }

            return resizedImageFile;
        }

        public static async Task<ImageSource> GetPreviewBitmapAsync(StorageFile sourceFile, int requestedMinSide = 1280)
        {
            try
            {
                if (sourceFile.ContentType.Equals("video/mp4"))
                {
                    var props = await sourceFile.Properties.GetVideoPropertiesAsync();
                    var composition = new MediaComposition();
                    var clip = await MediaClip.CreateFromFileAsync(sourceFile);

                    composition.Clips.Add(clip);

                    using (var imageStream = await composition.GetThumbnailAsync(TimeSpan.Zero, (int)props.GetWidth(), (int)props.GetHeight(), VideoFramePrecision.NearestKeyFrame))
                    {
                        return await GetPreviewBitmapAsync(imageStream, requestedMinSide);
                    }
                }

                using (var imageStream = await sourceFile.OpenReadAsync())
                {
                    return await GetPreviewBitmapAsync(imageStream, requestedMinSide);
                }
            }
            catch
            {
                return null;
            }
        }

        public static async Task<ImageSource> GetPreviewBitmapAsync(IRandomAccessStream imageStream, int requestedMinSide = 1280)
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
                        ScaledHeight = height,
                        InterpolationMode = BitmapInterpolationMode.Linear
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

        public static async Task<Color[]> GetAccentAsync(StorageFile sourceFile)
        {
            var result = new Color[2];

            try
            {
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
            }
            catch { }

            if (result[0] == null)
            {
                result[0] = Color.FromArgb(0x66, 0x7A, 0x8A, 0x96);
            }
            if (result[1] == null)
            {
                result[1] = Color.FromArgb(0x88, 0x7A, 0x8A, 0x96);
            }

            return result;
        }

        public static async Task<StorageFile> CropAsync(StorageFile sourceFile, StorageFile file, Rect cropRectangle, int min = 1280, int max = 0, double quality = 0.77, BitmapRotation rotation = BitmapRotation.None, BitmapFlip flip = BitmapFlip.None)
        {
            if (file == null)
            {
                file = await ApplicationData.Current.TemporaryFolder.CreateFileAsync("crop.jpg", CreationCollisionOption.ReplaceExisting);
            }

            using (var fileStream = await OpenReadAsync(sourceFile))
            using (var outputStream = await file.OpenAsync(FileAccessMode.ReadWrite))
            {
                var decoder = await BitmapDecoder.CreateAsync(fileStream);
                var cropWidth = (double)decoder.PixelWidth;
                var cropHeight = (double)decoder.PixelHeight;

                if (decoder.PixelWidth > 1280 || decoder.PixelHeight > 1280)
                {
                    double ratioX = (double)1280 / cropWidth;
                    double ratioY = (double)1280 / cropHeight;
                    double ratio = Math.Min(ratioX, ratioY);

                    cropWidth = cropWidth * ratio;
                    cropHeight = cropHeight * ratio;
                }

                cropRectangle = new Rect(
                    cropRectangle.X * decoder.PixelWidth,
                    cropRectangle.Y * decoder.PixelHeight,
                    cropRectangle.Width * decoder.PixelWidth,
                    cropRectangle.Height * decoder.PixelHeight);

                var (scaledCrop, scaledSize) = Scale(cropRectangle, new Size(decoder.PixelWidth, decoder.PixelHeight), new Size(cropWidth, cropHeight), min, max);

                var bounds = new BitmapBounds();
                bounds.X = (uint)scaledCrop.X;
                bounds.Y = (uint)scaledCrop.Y;
                bounds.Width = (uint)scaledCrop.Width;
                bounds.Height = (uint)scaledCrop.Height;

                var transform = new BitmapTransform();
                transform.ScaledWidth = (uint)scaledSize.Width;
                transform.ScaledHeight = (uint)scaledSize.Height;
                transform.Bounds = bounds;
                transform.InterpolationMode = BitmapInterpolationMode.Linear;
                transform.Rotation = rotation;
                transform.Flip = flip;

                var pixelData = await decoder.GetSoftwareBitmapAsync(decoder.BitmapPixelFormat, decoder.BitmapAlphaMode, transform, ExifOrientationMode.RespectExifOrientation, ColorManagementMode.DoNotColorManage);

                var propertySet = new BitmapPropertySet();
                var qualityValue = new BitmapTypedValue(quality, PropertyType.Single);
                propertySet.Add("ImageQuality", qualityValue);

                var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.JpegEncoderId, outputStream);
                encoder.SetSoftwareBitmap(pixelData);
                await encoder.FlushAsync();
            }

            return file;
        }

        private static (Rect, Size) Scale(Rect rect, Size start, Size size, int min, int max)
        {
            var width = rect.Width * size.Width / start.Width;
            var height = rect.Height * size.Height / start.Height;

            if (width > min || height > min)
            {
                double ratioX = (double)min / width;
                double ratioY = (double)min / height;
                double ratio = Math.Min(ratioX, ratioY);

                width = width * ratio;
                height = height * ratio;
            }

            if (width < max || height < max)
            {
                double ratioX = (double)max / width;
                double ratioY = (double)max / height;
                double ratio = Math.Min(ratioX, ratioY);

                width = width * ratio;
                height = height * ratio;
            }

            var ratioW = start.Width * width / rect.Width;
            var ratioH = start.Height * height / rect.Height;

            var x = rect.X * ratioW / start.Width;
            var y = rect.Y * ratioH / start.Height;
            var w = rect.Width * ratioW / start.Width;
            var h = rect.Height * ratioH / start.Height;

            return (new Rect(x, y, w, h), new Size(ratioW, ratioH));
        }

        public static async Task<ImageSource> CropAndPreviewAsync(StorageFile sourceFile, BitmapEditState editState)
        {
            if (sourceFile.ContentType.Equals("video/mp4"))
            {
                var props = await sourceFile.Properties.GetVideoPropertiesAsync();
                var composition = new MediaComposition();
                var clip = await MediaClip.CreateFromFileAsync(sourceFile);

                composition.Clips.Add(clip);

                using (var imageStream = await composition.GetThumbnailAsync(TimeSpan.Zero, (int)props.GetWidth(), (int)props.GetHeight(), VideoFramePrecision.NearestKeyFrame))
                {
                    return await CropAndPreviewAsync(imageStream, editState);
                }
            }

            using (var imageStream = await sourceFile.OpenReadAsync())
            {
                return await CropAndPreviewAsync(imageStream, editState);
            }
        }

        public static async Task<ImageSource> CropAndPreviewAsync(IRandomAccessStream imageStream, BitmapEditState editState)
        {
            var decoder = await BitmapDecoder.CreateAsync(imageStream);
            var cropWidth = (double)decoder.PixelWidth;
            var cropHeight = (double)decoder.PixelHeight;

            if (decoder.PixelWidth > 1280 || decoder.PixelHeight > 1280)
            {
                double ratioX = (double)1280 / cropWidth;
                double ratioY = (double)1280 / cropHeight;
                double ratio = Math.Min(ratioX, ratioY);

                cropWidth = cropWidth * ratio;
                cropHeight = cropHeight * ratio;
            }

            var cropRectangle = new Rect(
                editState.Rectangle.X * decoder.PixelWidth,
                editState.Rectangle.Y * decoder.PixelHeight,
                editState.Rectangle.Width * decoder.PixelWidth,
                editState.Rectangle.Height * decoder.PixelHeight);

            var (scaledCrop, scaledSize) = Scale(cropRectangle, new Size(decoder.PixelWidth, decoder.PixelHeight), new Size(cropWidth, cropHeight), 1280, 0);

            var bounds = new BitmapBounds();
            bounds.X = (uint)scaledCrop.X;
            bounds.Y = (uint)scaledCrop.Y;
            bounds.Width = (uint)scaledCrop.Width;
            bounds.Height = (uint)scaledCrop.Height;

            var transform = new BitmapTransform();
            transform.ScaledWidth = (uint)scaledSize.Width;
            transform.ScaledHeight = (uint)scaledSize.Height;
            transform.Bounds = bounds;
            transform.InterpolationMode = BitmapInterpolationMode.Linear;
            transform.Rotation = editState.Rotation;
            transform.Flip = editState.Flip;

            var pixelData = await decoder.GetSoftwareBitmapAsync(decoder.BitmapPixelFormat, BitmapAlphaMode.Premultiplied, transform, ExifOrientationMode.RespectExifOrientation, ColorManagementMode.DoNotColorManage);

            if (editState.Strokes != null)
            {
                var stream = await DrawStrokesAsync(pixelData, editState.Strokes, editState.Rectangle, editState.Rotation, editState.Flip);

                var bitmapImage = new BitmapImage();
                await bitmapImage.SetSourceAsync(stream);

                return bitmapImage;
            }
            else
            {

                var bitmapImage = new SoftwareBitmapSource();
                await bitmapImage.SetBitmapAsync(pixelData);

                return bitmapImage;
            }
        }

        public static async Task<IRandomAccessStream> OpenReadAsync(StorageFile sourceFile)
        {
            if (sourceFile.ContentType.Equals("video/mp4"))
            {
                var props = await sourceFile.Properties.GetVideoPropertiesAsync();
                var composition = new MediaComposition();
                var clip = await MediaClip.CreateFromFileAsync(sourceFile);

                composition.Clips.Add(clip);

                return await composition.GetThumbnailAsync(TimeSpan.Zero, (int)props.GetWidth(), (int)props.GetHeight(), VideoFramePrecision.NearestKeyFrame);
            }

            return await sourceFile.OpenReadAsync();
        }

        public static BitmapTransform ComputeScalingTransformForSourceImage(BitmapDecoder sourceDecoder)
        {
            var transform = new BitmapTransform();

            if (sourceDecoder.PixelWidth > 1280 || sourceDecoder.PixelHeight > 1280)
            {
                double ratioX = (double)1280 / sourceDecoder.PixelWidth;
                double ratioY = (double)1280 / sourceDecoder.PixelHeight;
                double ratio = Math.Min(ratioX, ratioY);

                transform.ScaledWidth = (uint)(sourceDecoder.PixelWidth * ratio);
                transform.ScaledHeight = (uint)(sourceDecoder.PixelHeight * ratio);
                transform.InterpolationMode = BitmapInterpolationMode.Linear;
            }

            return transform;
        }

        public static async Task<IRandomAccessStream> DrawStrokesAsync(SoftwareBitmap file, IReadOnlyList<SmoothPathBuilder> strokes, Rect rectangle, BitmapRotation rotation, BitmapFlip flip)
        {
            var device = CanvasDevice.GetSharedDevice();
            var bitmap = CanvasBitmap.CreateFromSoftwareBitmap(device, file);
            var canvas1 = new CanvasRenderTarget(device, (float)bitmap.Size.Width, (float)bitmap.Size.Height, bitmap.Dpi);
            var canvas2 = new CanvasRenderTarget(device, (float)bitmap.Size.Width, (float)bitmap.Size.Height, bitmap.Dpi);

            var size = canvas1.Size.ToVector2();
            var canvasSize = canvas1.Size.ToVector2();

            var scaleX = 1 / (float)rectangle.Width;
            var scaleY = 1 / (float)rectangle.Height;

            var offsetX = (float)rectangle.X * scaleX;
            var offsetY = (float)rectangle.Y * scaleY;

            if (rotation == BitmapRotation.Clockwise270Degrees ||
                rotation == BitmapRotation.Clockwise90Degrees)
            {
                size = new Vector2(size.Y, size.X);

                scaleX = scaleY;
                scaleY = 1 * 1 / (float)rectangle.Width;
            }

            using (var session = canvas1.CreateDrawingSession())
            {
                switch (rotation)
                {
                    case BitmapRotation.Clockwise90Degrees:
                        var transform1 = Matrix3x2.CreateRotation(MathFEx.ToRadians(90));
                        transform1.Translation = new Vector2(size.Y, 0);
                        session.Transform = transform1;
                        break;
                    case BitmapRotation.Clockwise180Degrees:
                        var transform2 = Matrix3x2.CreateRotation(MathFEx.ToRadians(180));
                        transform2.Translation = new Vector2(size.X, size.Y);
                        session.Transform = transform2;
                        break;
                    case BitmapRotation.Clockwise270Degrees:
                        var transform3 = Matrix3x2.CreateRotation(MathFEx.ToRadians(270));
                        transform3.Translation = new Vector2(0, size.X);
                        session.Transform = transform3;
                        break;
                }

                switch (flip)
                {
                    case BitmapFlip.Horizontal:
                        switch (rotation)
                        {
                            case BitmapRotation.Clockwise90Degrees:
                            case BitmapRotation.Clockwise270Degrees:
                                session.Transform = Matrix3x2.Multiply(session.Transform, Matrix3x2.CreateScale(1, -1, canvasSize / 2));
                                break;
                            default:
                                session.Transform = Matrix3x2.Multiply(session.Transform, Matrix3x2.CreateScale(-1, 1, canvasSize / 2));
                                break;
                        }
                        break;
                    case BitmapFlip.Vertical:
                        switch (rotation)
                        {
                            case BitmapRotation.None:
                            case BitmapRotation.Clockwise180Degrees:
                                session.Transform = Matrix3x2.Multiply(session.Transform, Matrix3x2.CreateScale(1, -1, canvasSize / 2));
                                break;
                            default:
                                session.Transform = Matrix3x2.Multiply(session.Transform, Matrix3x2.CreateScale(-1, 1, canvasSize / 2));
                                break;
                        }
                        break;
                }

                session.Transform = Matrix3x2.Multiply(session.Transform, Matrix3x2.CreateScale(scaleX, scaleY));
                session.Transform = Matrix3x2.Multiply(session.Transform, Matrix3x2.CreateTranslation(-(offsetX * size.X), -(offsetY * size.Y)));

                foreach (var builder in strokes)
                {
                    PencilCanvas.DrawPath(session, builder, size);
                }
            }

            using (var session = canvas2.CreateDrawingSession())
            {
                session.DrawImage(bitmap);
                session.DrawImage(canvas1);
            }

            bitmap.Dispose();

            var stream = new InMemoryRandomAccessStream();
            await canvas2.SaveAsync(stream, CanvasBitmapFileFormat.Jpeg/*, 0.77f*/);

            canvas2.Dispose();
            canvas1.Dispose();

            stream.Seek(0);
            return stream;
        }

        public static async Task<StorageFile> DrawStrokesAsync(StorageFile file, IReadOnlyList<SmoothPathBuilder> strokes, Rect rectangle, BitmapRotation rotation, BitmapFlip flip)
        {
            var device = CanvasDevice.GetSharedDevice();
            var bitmap = await CanvasBitmap.LoadAsync(device, file.Path);
            var canvas1 = new CanvasRenderTarget(device, (float)bitmap.Size.Width, (float)bitmap.Size.Height, bitmap.Dpi);
            var canvas2 = new CanvasRenderTarget(device, (float)bitmap.Size.Width, (float)bitmap.Size.Height, bitmap.Dpi);

            var size = canvas1.Size.ToVector2();
            var canvasSize = canvas1.Size.ToVector2();

            var scaleX = 1 / (float)rectangle.Width;
            var scaleY = 1 / (float)rectangle.Height;

            var offsetX = (float)rectangle.X * scaleX;
            var offsetY = (float)rectangle.Y * scaleY;

            if (rotation == BitmapRotation.Clockwise270Degrees ||
                rotation == BitmapRotation.Clockwise90Degrees)
            {
                size = new Vector2(size.Y, size.X);

                scaleX = scaleY;
                scaleY = 1 * 1 / (float)rectangle.Width;
            }

            using (var session = canvas1.CreateDrawingSession())
            {
                switch (rotation)
                {
                    case BitmapRotation.Clockwise90Degrees:
                        var transform1 = Matrix3x2.CreateRotation(MathFEx.ToRadians(90));
                        transform1.Translation = new Vector2(size.Y, 0);
                        session.Transform = transform1;
                        break;
                    case BitmapRotation.Clockwise180Degrees:
                        var transform2 = Matrix3x2.CreateRotation(MathFEx.ToRadians(180));
                        transform2.Translation = new Vector2(size.X, size.Y);
                        session.Transform = transform2;
                        break;
                    case BitmapRotation.Clockwise270Degrees:
                        var transform3 = Matrix3x2.CreateRotation(MathFEx.ToRadians(270));
                        transform3.Translation = new Vector2(0, size.X);
                        session.Transform = transform3;
                        break;
                }

                switch (flip)
                {
                    case BitmapFlip.Horizontal:
                        switch (rotation)
                        {
                            case BitmapRotation.Clockwise90Degrees:
                            case BitmapRotation.Clockwise270Degrees:
                                session.Transform = Matrix3x2.Multiply(session.Transform, Matrix3x2.CreateScale(1, -1, canvasSize / 2));
                                break;
                            default:
                                session.Transform = Matrix3x2.Multiply(session.Transform, Matrix3x2.CreateScale(-1, 1, canvasSize / 2));
                                break;
                        }
                        break;
                    case BitmapFlip.Vertical:
                        switch (rotation)
                        {
                            case BitmapRotation.None:
                            case BitmapRotation.Clockwise180Degrees:
                                session.Transform = Matrix3x2.Multiply(session.Transform, Matrix3x2.CreateScale(1, -1, canvasSize / 2));
                                break;
                            default:
                                session.Transform = Matrix3x2.Multiply(session.Transform, Matrix3x2.CreateScale(-1, 1, canvasSize / 2));
                                break;
                        }
                        break;
                }

                session.Transform = Matrix3x2.Multiply(session.Transform, Matrix3x2.CreateScale(scaleX, scaleY));
                session.Transform = Matrix3x2.Multiply(session.Transform, Matrix3x2.CreateTranslation(-(offsetX * size.X), -(offsetY * size.Y)));

                foreach (var builder in strokes)
                {
                    PencilCanvas.DrawPath(session, builder, size);
                }
            }

            using (var session = canvas2.CreateDrawingSession())
            {
                session.DrawImage(bitmap);
                session.DrawImage(canvas1);
            }

            bitmap.Dispose();

            using (var stream = await file.OpenAsync(FileAccessMode.ReadWrite))
            {
                await canvas2.SaveAsync(stream, CanvasBitmapFileFormat.Jpeg/*, 0.77f*/);
            }

            canvas2.Dispose();
            canvas1.Dispose();

            return file;
        }
    }
}
