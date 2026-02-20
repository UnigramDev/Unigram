//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Microsoft.Graphics.Canvas;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Telegram.Controls;
using Telegram.Entities;
using Telegram.Native;
using Windows.Foundation;
using Windows.Graphics;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;

namespace Telegram.Common
{
    public static class ImageHelper
    {
        public static Size Scale(this Size size, double requestedMaxSide)
        {
            double ratioX = (double)requestedMaxSide / size.Width;
            double ratioY = (double)requestedMaxSide / size.Height;
            double ratio = Math.Max(ratioX, ratioY);

            return new Size(size.Width * ratio, size.Height * ratio);
        }

        public static Size Scale(double width, double height, double requestedMaxSide)
        {
            double ratioX = (double)requestedMaxSide / width;
            double ratioY = (double)requestedMaxSide / height;
            double ratio = Math.Max(ratioX, ratioY);

            return new Size(width * ratio, height * ratio);
        }

        public static double ScaleRatioMin(double width, double height, double requestedMaxSide)
        {
            double ratioX = (double)requestedMaxSide / width;
            double ratioY = (double)requestedMaxSide / height;
            double ratio = Math.Min(ratioX, ratioY);

            return ratio;
        }

        public static Size ScaleMin(double width, double height, double requestedMaxSide)
        {
            double ratioX = (double)requestedMaxSide / width;
            double ratioY = (double)requestedMaxSide / height;
            double ratio = Math.Min(ratioX, ratioY);

            return new Size(width * ratio, height * ratio);
        }

        public static async Task<SizeInt32> GetScaleAsync(StorageFile file, bool allowMultipleFrames = false, int requestedMinSide = 1280, ImageGeneration generation = null)
        {
            try
            {
                using (var source = await file.OpenReadAsync())
                {
                    var decoder = await BitmapDecoder.CreateAsync(source);
                    if (decoder.FrameCount > 1 && !allowMultipleFrames)
                    {
                        return new SizeInt32 { Width = 0, Height = 0 };
                    }

                    var width = decoder.PixelWidth;
                    var height = decoder.PixelHeight;

                    if (generation?.Rotation is ImageRotation.Clockwise90Degrees or ImageRotation.Clockwise270Degrees)
                    {
                        (width, height) = (height, width);
                    }

                    if (generation?.Rectangle is Rect crop && (crop.X != 0 || crop.Y != 0 || crop.Right != 1 || crop.Bottom != 1))
                    {
                        width = (uint)(crop.Width * width);
                        height = (uint)(crop.Height * height);
                    }

                    if (width > requestedMinSide || height > requestedMinSide)
                    {
                        double ratioX = (double)requestedMinSide / width;
                        double ratioY = (double)requestedMinSide / height;
                        double ratio = Math.Min(ratioX, ratioY);

                        return new SizeInt32
                        {
                            Width = (int)(width * ratio),
                            Height = (int)(height * ratio)
                        };
                    }

                    return new SizeInt32
                    {
                        Width = (int)(width),
                        Height = (int)(height)
                    };
                }
            }
            catch
            {
                return new SizeInt32
                {
                    Width = 0,
                    Height = 0
                };
            }
        }



        /// <summary>
        /// Resizes and crops source file image so that resized image width/height are not larger than <param name="requestedMinSide"></param>
        /// </summary>
        /// <param name="sourceFile">Source StorageFile</param>
        /// <param name="resizedImageFile">Target StorageFile</param>
        /// <param name="requestedMinSide">Max width/height of the output image</param>
        /// <param name="quality">JPEG compression quality (0.77 for pictures, 0.87 for thumbnails)</param>
        /// <returns></returns>
        public static async Task<StorageFile> ScaleAsync(Guid encoderId, StorageFile sourceFile, StorageFile resizedImageFile, int requestedMinSide, bool bestQuality = false, TimeSpan? trimStart = null)
        {
            using (var source = await OpenReadAsync(sourceFile, trimStart))
            {
                return await ScaleAsync(encoderId, source, resizedImageFile, requestedMinSide, bestQuality);
            }
        }

        public static async Task<StorageFile> ScaleAsync(Guid encoderId, IRandomAccessStream source, StorageFile resizedImageFile, int requestedMinSide, bool bestQuality = false)
        {
            var decoder = await BitmapDecoder.CreateAsync(source);
            //if (decoder.FrameCount > 1)
            //{
            //    throw new InvalidCastException();
            //}

            var originalPixelWidth = decoder.PixelWidth;
            var originalPixelHeight = decoder.PixelHeight;

            using (var resizedStream = await resizedImageFile.OpenAsync(FileAccessMode.ReadWrite))
            {
                BitmapTransform transform;

                if (requestedMinSide > 0 && (decoder.PixelWidth > requestedMinSide || decoder.PixelHeight > requestedMinSide))
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
                        InterpolationMode = bestQuality
                            ? BitmapInterpolationMode.Fant
                            : BitmapInterpolationMode.Linear
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

                var encoder = await BitmapEncoder.CreateAsync(encoderId, resizedStream/*, propertySet*/);
                encoder.SetSoftwareBitmap(pixelData);
                await encoder.FlushAsync();
            }

            return resizedImageFile;
        }

        public static async Task<StorageFile> TranscodeAsync(IRandomAccessStream source, StorageFile resizedImageFile, Guid encoderId)
        {
            var decoder = await BitmapDecoder.CreateAsync(source);
            //if (decoder.FrameCount > 1)
            //{
            //    throw new InvalidCastException();
            //}

            using (var resizedStream = await resizedImageFile.OpenAsync(FileAccessMode.ReadWrite))
            {
                var pixelData = await decoder.GetSoftwareBitmapAsync(decoder.BitmapPixelFormat, decoder.BitmapAlphaMode, new BitmapTransform(), ExifOrientationMode.RespectExifOrientation, ColorManagementMode.DoNotColorManage);

                var encoder = await BitmapEncoder.CreateAsync(encoderId, resizedStream);
                encoder.SetSoftwareBitmap(pixelData);
                await encoder.FlushAsync();
            }

            return resizedImageFile;
        }

        public static async Task<ImageSource> GetPreviewBitmapAsync(StorageMedia source, int requestedMinSide = 1280)
        {
            try
            {
                if (source is StorageVideo)
                {
                    int width = 0;
                    int height = 0;

                    var buffer = await Task.Run(async () =>
                    {
                        using var videoStream = await source.File.OpenReadAsync();
                        using var animation = VideoAnimation.LoadFromFile(new VideoAnimationStreamSource(videoStream), false, false, false);

                        if (animation.PixelWidth > requestedMinSide || animation.PixelHeight > requestedMinSide)
                        {
                            double ratioX = (double)requestedMinSide / animation.PixelWidth;
                            double ratioY = (double)requestedMinSide / animation.PixelHeight;
                            double ratio = Math.Min(ratioX, ratioY);

                            width = (int)(animation.PixelWidth * ratio);
                            height = (int)(animation.PixelHeight * ratio);
                        }
                        else
                        {
                            width = animation.PixelWidth;
                            height = animation.PixelHeight;
                        }

                        var frame = BufferSurface.Create((uint)(width * height * 4));
                        animation.RenderSync(frame, width, height, true, out _);

                        return frame;
                    });

                    if (width > 0 && height > 0)
                    {
                        var bitmap = new WriteableBitmap(width, height);
                        BufferSurface.Copy(buffer, bitmap.PixelBuffer);

                        return bitmap;
                    }
                }
                else if (source is StoragePhoto)
                {
                    using var imageStream = await source.File.OpenReadAsync();
                    return await GetPreviewBitmapAsync(imageStream, requestedMinSide);
                }
            }
            catch { }

            return null;
        }

        public static async Task<ImageSource> GetPreviewBitmapAsync(IRandomAccessStream source, int requestedMinSide = 1280)
        {
            var decoder = await BitmapDecoder.CreateAsync(source);
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
                await bitmap.SetSourceAsync(source);

                return bitmap;
            }
        }

        public static async Task<StorageFile> CropAsync(StorageFile sourceFile, StorageFile file, Rect cropRectangle, int min = 1280, int max = 0, double quality = 0.77, ImageRotation rotation = ImageRotation.None, ImageFlip flip = ImageFlip.None, TimeSpan? trimStart = null, bool bestQuality = false)
        {
            file ??= await ApplicationData.Current.TemporaryFolder.CreateFileAsync("crop.jpg", CreationCollisionOption.ReplaceExisting);

            using (var source = await OpenReadAsync(sourceFile))
            using (var destination = await file.OpenAsync(FileAccessMode.ReadWrite))
            {
                var decoder = await BitmapDecoder.CreateAsync(source);
                var imageSize = new Size(decoder.PixelWidth, decoder.PixelHeight);

                // TODO: cropRectangle comes already translated, so no rotation/flip needs to be applied to it
                // I don't really like this, but at the same time I don't like the idea of "unapplying" the transform in ImageCropper
                if (rotation is ImageRotation.Clockwise90Degrees or ImageRotation.Clockwise270Degrees)
                {
                    (imageSize.Width, imageSize.Height) = (imageSize.Height, imageSize.Width);
                }

                if (cropRectangle == default || (cropRectangle.Width == 0 && cropRectangle.Height == 0))
                {
                    cropRectangle = new Rect(0, 0, decoder.PixelWidth, decoder.PixelHeight);
                }
                else if (cropRectangle.Right <= 1 && cropRectangle.Bottom <= 1)
                {
                    cropRectangle = new Rect(
                        cropRectangle.X * imageSize.Width,
                        cropRectangle.Y * imageSize.Height,
                        cropRectangle.Width * imageSize.Width,
                        cropRectangle.Height * imageSize.Height);
                }

                (cropRectangle, imageSize) = Scale(cropRectangle, imageSize, min, max);

                if (rotation is ImageRotation.Clockwise90Degrees or ImageRotation.Clockwise270Degrees)
                {
                    (imageSize.Width, imageSize.Height) = (imageSize.Height, imageSize.Width);
                }

                //if (flip != ImageFlip.None)
                //{
                //    cropRectangle = FlipArea(cropRectangle, imageSize.Width, imageSize.Height, flip);
                //}

                //if (rotation != ImageRotation.None)
                //{
                //    cropRectangle = RotateArea(cropRectangle, imageSize.Width, imageSize.Height, (int)rotation);
                //}

                var bounds = new BitmapBounds();
                bounds.X = (uint)cropRectangle.X;
                bounds.Y = (uint)cropRectangle.Y;
                bounds.Width = (uint)cropRectangle.Width;
                bounds.Height = (uint)cropRectangle.Height;

                var transform = new BitmapTransform
                {
                    ScaledWidth = (uint)imageSize.Width,
                    ScaledHeight = (uint)imageSize.Height,
                    Bounds = bounds,
                    Rotation = (BitmapRotation)rotation,
                    Flip = (BitmapFlip)flip,
                    InterpolationMode = bestQuality
                            ? BitmapInterpolationMode.Fant
                            : BitmapInterpolationMode.Linear
                };

                var pixelData = await decoder.GetSoftwareBitmapAsync(decoder.BitmapPixelFormat, decoder.BitmapAlphaMode, transform, ExifOrientationMode.RespectExifOrientation, ColorManagementMode.DoNotColorManage);

                // Not using ATM, quality is too low
                //var propertySet = new BitmapPropertySet();
                //var qualityValue = new BitmapTypedValue(quality, PropertyType.Single);
                //propertySet.Add("ImageQuality", qualityValue);

                var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.JpegEncoderId, destination);
                encoder.SetSoftwareBitmap(pixelData);
                await encoder.FlushAsync();
            }

            return file;
        }

        public static Rect RotateArea(Rect area, double width, double height, int count)
        {
            count = count % 4;

            for (int i = 0; i < count; i++)
            {
                var point = new Point(height - area.Bottom, width - (width - area.X));
                area = new Rect(point.X, point.Y, area.Height, area.Width);

                (width, height) = (height, width);
            }

            return area;
        }

        public static Rect FlipArea(Rect area, double width, double height, ImageFlip flip)
        {
            if (flip == ImageFlip.Horizontal)
            {
                var newX = width - area.Right;
                return new Rect(newX, area.Y, area.Width, area.Height);
            }
            else if (flip == ImageFlip.Vertical)
            {
                var newY = height - area.Bottom;
                return new Rect(area.X, newY, area.Width, area.Height);
            }

            return area;
        }

        private static (Rect, Size) Scale(Rect rect, Size start, int min, int max)
        {
            var width = rect.Width;
            var height = rect.Height;

            if (width > min || height > min)
            {
                double ratioX = min / width;
                double ratioY = min / height;
                double ratio = Math.Min(ratioX, ratioY);

                width *= ratio;
                height *= ratio;
            }

            if (width < max || height < max)
            {
                double ratioX = max / width;
                double ratioY = max / height;
                double ratio = Math.Min(ratioX, ratioY);

                width *= ratio;
                height *= ratio;
            }

            var ratioW = start.Width * width / rect.Width;
            var ratioH = start.Height * height / rect.Height;

            var x = rect.X * ratioW / start.Width;
            var y = rect.Y * ratioH / start.Height;
            var w = rect.Width * ratioW / start.Width;
            var h = rect.Height * ratioH / start.Height;

            return (new Rect(x, y, w, h), new Size(ratioW, ratioH));
        }

        public static async Task<ImageSource> CropAndPreviewAsync(StorageMedia source, ImageGeneration generation, int maxSize = 1280)
        {
            if (source is StorageVideo)
            {
                using var videoStream = await source.File.OpenReadAsync();
                using var animation = await Task.Run(() => VideoAnimation.LoadFromFile(new VideoAnimationStreamSource(videoStream), false, false, false));

                if (generation.TrimStartTime is TimeSpan trimStart && trimStart > TimeSpan.Zero)
                {
                    animation.SeekToMilliseconds((long)trimStart.TotalMilliseconds, false);
                }

                int width = animation.PixelWidth;
                int height = animation.PixelHeight;

                var frame = BufferSurface.Create((uint)(width * height * 4));
                await Task.Run(() => animation.RenderSync(frame, width, height, true, out _));

                using var stream = new InMemoryRandomAccessStream();
                PlaceholderHelper.Background.Encode(frame, stream, width, height, animation.Rotation);

                return await CropAndPreviewAsync(stream, generation, maxSize);
            }
            else
            {
                using var imageStream = await source.File.OpenReadAsync();
                return await CropAndPreviewAsync(imageStream, generation, maxSize);
            }
        }

        public static async Task<ImageSource> CropAndPreviewAsync(IRandomAccessStream source, ImageGeneration generation, int maxSize = 1280)
        {
            var decoder = await BitmapDecoder.CreateAsync(source);

            var cropRectangle = generation.Rectangle;
            var imageSize = new Size(decoder.PixelWidth, decoder.PixelHeight);

            // TODO: cropRectangle comes already translated, so no rotation/flip needs to be applied to it
            // I don't really like this, but at the same time I don't like the idea of "unapplying" the transform in ImageCropper
            if (generation.Rotation is ImageRotation.Clockwise90Degrees or ImageRotation.Clockwise270Degrees)
            {
                (imageSize.Width, imageSize.Height) = (imageSize.Height, imageSize.Width);
            }

            if (cropRectangle == default || (cropRectangle.Width == 0 && cropRectangle.Height == 0))
            {
                cropRectangle = new Rect(0, 0, decoder.PixelWidth, decoder.PixelHeight);
            }
            else if (cropRectangle.Right <= 1 && cropRectangle.Bottom <= 1)
            {
                cropRectangle = new Rect(
                    cropRectangle.X * imageSize.Width,
                    cropRectangle.Y * imageSize.Height,
                    cropRectangle.Width * imageSize.Width,
                    cropRectangle.Height * imageSize.Height);
            }

            (cropRectangle, imageSize) = Scale(cropRectangle, imageSize, maxSize, 0);

            if (generation.Rotation is ImageRotation.Clockwise90Degrees or ImageRotation.Clockwise270Degrees)
            {
                (imageSize.Width, imageSize.Height) = (imageSize.Height, imageSize.Width);
            }

            var bounds = new BitmapBounds();
            bounds.X = (uint)cropRectangle.X;
            bounds.Y = (uint)cropRectangle.Y;
            bounds.Width = (uint)cropRectangle.Width;
            bounds.Height = (uint)cropRectangle.Height;

            var transform = new BitmapTransform();
            transform.ScaledWidth = (uint)imageSize.Width;
            transform.ScaledHeight = (uint)imageSize.Height;
            transform.Bounds = bounds;
            transform.InterpolationMode = BitmapInterpolationMode.Linear;
            transform.Rotation = (BitmapRotation)generation.Rotation;
            transform.Flip = (BitmapFlip)generation.Flip;

            var pixelData = await decoder.GetSoftwareBitmapAsync(decoder.BitmapPixelFormat, BitmapAlphaMode.Premultiplied, transform, ExifOrientationMode.RespectExifOrientation, ColorManagementMode.DoNotColorManage);

            if (generation.Strokes != null)
            {
                using (var stream = await DrawStrokesAsync(pixelData, generation.Strokes, generation.Rectangle, generation.Rotation, generation.Flip))
                {
                    var bitmapImage = new BitmapImage();
                    await bitmapImage.SetSourceAsync(stream);

                    return bitmapImage;
                }
            }
            else
            {
                var bitmapImage = new SoftwareBitmapSource();
                await bitmapImage.SetBitmapAsync(pixelData);

                return bitmapImage;
            }
        }

        public static async Task<IRandomAccessStream> OpenReadAsync(StorageFile sourceFile, TimeSpan? trimStart = null)
        {
            if (sourceFile.FileType.Equals(".mp4", StringComparison.OrdinalIgnoreCase))
            {
                return await Task.Run(async () =>
                {
                    using var videoStream = await sourceFile.OpenReadAsync();
                    using var animation = VideoAnimation.LoadFromFile(new VideoAnimationStreamSource(videoStream), false, false, false);

                    if (trimStart > TimeSpan.Zero)
                    {
                        animation.SeekToMilliseconds((long)trimStart.Value.TotalMilliseconds, false);
                    }

                    int width = animation.PixelWidth;
                    int height = animation.PixelHeight;

                    var frame = BufferSurface.Create((uint)(width * height * 4));
                    var result = animation.RenderSync(frame, width, height, true, out _);

                    var stream = new InMemoryRandomAccessStream();
                    PlaceholderHelper.Background.Encode(frame, stream, width, height, animation.Rotation);

                    return stream;
                });
            }
            else
            {
                return await sourceFile.OpenReadAsync();
            }
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

        public static async Task<IRandomAccessStream> DrawStrokesAsync(SoftwareBitmap file, IReadOnlyList<SmoothPathBuilder> strokes, Rect rectangle, ImageRotation rotation, ImageFlip flip)
        {
            var device = ElementComposition.GetSharedDevice();
            var bitmap = CanvasBitmap.CreateFromSoftwareBitmap(device, file);

            var stream = new InMemoryRandomAccessStream();

            using (var canvas2 = DrawStrokes(device, bitmap, strokes, rectangle, rotation, flip))
            {
                await canvas2.SaveAsync(stream, CanvasBitmapFileFormat.Jpeg/*, 0.77f*/);
            }

            stream.Seek(0);
            return stream;
        }

        public static async Task<StorageFile> DrawStrokesAsync(StorageFile file, IReadOnlyList<SmoothPathBuilder> strokes, Rect rectangle, ImageRotation rotation, ImageFlip flip)
        {
            var device = ElementComposition.GetSharedDevice();
            var bitmap = await CanvasBitmap.LoadAsync(device, file.Path);

            using (var canvas2 = DrawStrokes(device, bitmap, strokes, rectangle, rotation, flip))
            using (var stream = await file.OpenAsync(FileAccessMode.ReadWrite))
            {
                await canvas2.SaveAsync(stream, CanvasBitmapFileFormat.Jpeg/*, 0.77f*/);
            }

            return file;
        }

        public static CanvasRenderTarget DrawStrokes(CanvasDevice device, CanvasBitmap bitmap, IReadOnlyList<SmoothPathBuilder> strokes, Rect rectangle, ImageRotation rotation, ImageFlip flip)
        {
            var canvas1 = new CanvasRenderTarget(device, (float)bitmap.Size.Width, (float)bitmap.Size.Height, bitmap.Dpi);
            var canvas2 = new CanvasRenderTarget(device, (float)bitmap.Size.Width, (float)bitmap.Size.Height, bitmap.Dpi);

            var size = canvas1.Size.ToVector2();
            var canvasSize = canvas1.Size.ToVector2();

            var scaleX = 1 / (float)rectangle.Width;
            var scaleY = 1 / (float)rectangle.Height;

            var offsetX = (float)rectangle.X * scaleX;
            var offsetY = (float)rectangle.Y * scaleY;

            if (rotation is ImageRotation.Clockwise270Degrees or ImageRotation.Clockwise90Degrees)
            {
                size = new Vector2(size.Y, size.X);

                scaleX = scaleY;
                scaleY = 1 * 1 / (float)rectangle.Width;
            }

            using (var session = canvas1.CreateDrawingSession())
            {
                switch (rotation)
                {
                    case ImageRotation.Clockwise90Degrees:
                        var transform1 = Matrix3x2.CreateRotation(MathFEx.ToRadians(90));
                        transform1.Translation = new Vector2(size.Y, 0);
                        session.Transform = transform1;
                        break;
                    case ImageRotation.Clockwise180Degrees:
                        var transform2 = Matrix3x2.CreateRotation(MathFEx.ToRadians(180));
                        transform2.Translation = new Vector2(size.X, size.Y);
                        session.Transform = transform2;
                        break;
                    case ImageRotation.Clockwise270Degrees:
                        var transform3 = Matrix3x2.CreateRotation(MathFEx.ToRadians(270));
                        transform3.Translation = new Vector2(0, size.X);
                        session.Transform = transform3;
                        break;
                }

                switch (flip)
                {
                    case ImageFlip.Horizontal:
                        switch (rotation)
                        {
                            case ImageRotation.Clockwise90Degrees:
                            case ImageRotation.Clockwise270Degrees:
                                session.Transform = Matrix3x2.Multiply(session.Transform, Matrix3x2.CreateScale(1, -1, canvasSize / 2));
                                break;
                            default:
                                session.Transform = Matrix3x2.Multiply(session.Transform, Matrix3x2.CreateScale(-1, 1, canvasSize / 2));
                                break;
                        }
                        break;
                    case ImageFlip.Vertical:
                        switch (rotation)
                        {
                            case ImageRotation.None:
                            case ImageRotation.Clockwise180Degrees:
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
            canvas1.Dispose();

            return canvas2;
        }
    }
}
