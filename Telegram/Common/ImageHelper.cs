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
        #region Scaling

        // Contain semantics throughout: the source fits inside a square of the given
        // side. SizeEx.Scale in Telegram.Controls has an identical signature but
        // multiplies by a factor instead.

        /// <summary>
        /// Factor that fits <paramref name="width"/> by <paramref name="height"/> inside
        /// a square of <paramref name="side"/>, touching it on the longer edge. Greater
        /// than 1 when the source is smaller than the box; use <see cref="Clamp"/> for a
        /// bound that never scales up.
        /// </summary>
        public static double FitRatio(double width, double height, double side)
        {
            return Math.Min(side / width, side / height);
        }

        /// <summary><see cref="FitRatio"/> applied to the size itself.</summary>
        public static Size Fit(double width, double height, double side)
        {
            var ratio = FitRatio(width, height, side);
            return new Size(width * ratio, height * ratio);
        }

        /// <summary>
        /// <see cref="Fit"/> when the size exceeds <paramref name="maxSide"/>, otherwise
        /// the size unchanged. Never scales up.
        /// </summary>
        public static Size Clamp(double width, double height, double maxSide)
        {
            if (width > maxSide || height > maxSide)
            {
                return Fit(width, height, maxSide);
            }

            return new Size(width, height);
        }

        #endregion

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

                    var scaled = Clamp(width, height, requestedMinSide);

                    return new SizeInt32
                    {
                        Width = (int)scaled.Width,
                        Height = (int)scaled.Height
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
                // An empty transform, not one set to the source's own size: the decoder
                // resamples whenever ScaledWidth and ScaledHeight are set.
                BitmapTransform transform;

                if (requestedMinSide > 0 && (originalPixelWidth > requestedMinSide || originalPixelHeight > requestedMinSide))
                {
                    var scaled = Fit(originalPixelWidth, originalPixelHeight, requestedMinSide);

                    transform = new BitmapTransform
                    {
                        ScaledWidth = (uint)scaled.Width,
                        ScaledHeight = (uint)scaled.Height,
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

                        var scaled = Clamp(animation.PixelWidth, animation.PixelHeight, requestedMinSide);

                        width = (int)scaled.Width;
                        height = (int)scaled.Height;

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
                BitmapTransform transform;

                if (decoder.PixelWidth > requestedMinSide || decoder.PixelHeight > requestedMinSide)
                {
                    var scaled = Fit(decoder.PixelWidth, decoder.PixelHeight, requestedMinSide);

                    transform = new BitmapTransform
                    {
                        ScaledWidth = (uint)scaled.Width,
                        ScaledHeight = (uint)scaled.Height,
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

        /// <summary>
        /// Builds the decoder transform for a crop: the source region to read, the size
        /// of the resized image, and the rotation and flip to apply. Shared by
        /// <see cref="CropAsync"/> and <see cref="CropAndPreviewAsync"/>.
        /// </summary>
        /// <param name="cropRectangle">
        /// Normalized when its right and bottom edges are within 1, in pixels otherwise,
        /// the whole image when empty.
        /// </param>
        private static BitmapTransform ComputeCropTransform(BitmapDecoder decoder, Rect cropRectangle,
            ImageRotation rotation, ImageFlip flip, int min, int max, BitmapInterpolationMode interpolation)
        {
            var imageSize = new Size(decoder.PixelWidth, decoder.PixelHeight);

            // TODO: cropRectangle comes already translated, so no rotation/flip needs to be applied to it
            // I don't really like this, but at the same time I don't like the idea of "unapplying" the transform in ImageCropper
            //
            // The size is therefore swapped twice: first to expand a normalized rectangle
            // against the cropper's dimensions, then back so ScaledWidth and ScaledHeight
            // describe the source, which the decoder scales before rotating.
            var rotated = rotation is ImageRotation.Clockwise90Degrees or ImageRotation.Clockwise270Degrees;

            if (rotated)
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

            if (rotated)
            {
                (imageSize.Width, imageSize.Height) = (imageSize.Height, imageSize.Width);
            }

            return new BitmapTransform
            {
                ScaledWidth = (uint)imageSize.Width,
                ScaledHeight = (uint)imageSize.Height,
                Bounds = new BitmapBounds
                {
                    X = (uint)cropRectangle.X,
                    Y = (uint)cropRectangle.Y,
                    Width = (uint)cropRectangle.Width,
                    Height = (uint)cropRectangle.Height
                },
                Rotation = (BitmapRotation)rotation,
                Flip = (BitmapFlip)flip,
                InterpolationMode = interpolation
            };
        }

        public static async Task<StorageFile> CropAsync(StorageFile sourceFile, StorageFile file, Rect cropRectangle, int min = 1280, int max = 0, double quality = 0.77, ImageRotation rotation = ImageRotation.None, ImageFlip flip = ImageFlip.None, TimeSpan? trimStart = null, bool bestQuality = false)
        {
            file ??= await ApplicationData.Current.TemporaryFolder.CreateFileAsync("crop.jpg", CreationCollisionOption.ReplaceExisting);

            using (var source = await OpenReadAsync(sourceFile))
            using (var destination = await file.OpenAsync(FileAccessMode.ReadWrite))
            {
                var decoder = await BitmapDecoder.CreateAsync(source);
                var transform = ComputeCropTransform(decoder, cropRectangle, rotation, flip, min, max,
                    bestQuality ? BitmapInterpolationMode.Fant : BitmapInterpolationMode.Linear);

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

        /// <summary>
        /// Scales a crop rectangle and its source image by the same factor, so the crop
        /// covers the same region once the decoder has resized the image.
        ///
        /// The factor is derived from the crop, bounded above by <paramref name="min"/>
        /// and below by <paramref name="max"/>. Returns the rectangle in the resized
        /// image's coordinates and the size of that resized image.
        /// </summary>
        private static (Rect Crop, Size Image) Scale(Rect rect, Size image, int min, int max)
        {
            var scaled = Clamp(rect.Width, rect.Height, min);

            // Then up to the floor. The larger ratio, because both sides must reach
            // max: the smaller one brings the longer edge down to it and leaves the
            // shorter edge below.
            if (scaled.Width < max || scaled.Height < max)
            {
                double ratio = Math.Max(max / scaled.Width, max / scaled.Height);

                scaled.Width *= ratio;
                scaled.Height *= ratio;
            }

            // One factor per axis. The scaling above is uniform so the two agree, but a
            // zero-width or zero-height crop must keep yielding NaN as it did before.
            var ratioW = scaled.Width / rect.Width;
            var ratioH = scaled.Height / rect.Height;

            return (new Rect(rect.X * ratioW, rect.Y * ratioH, scaled.Width, scaled.Height),
                new Size(image.Width * ratioW, image.Height * ratioH));
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
            var transform = ComputeCropTransform(decoder, generation.Rectangle, generation.Rotation, generation.Flip,
                maxSize, 0, BitmapInterpolationMode.Linear);

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

        public static BitmapTransform ComputeScalingTransformForSourceImage(BitmapDecoder sourceDecoder, int maxSide = 1280)
        {
            var transform = new BitmapTransform();

            if (sourceDecoder.PixelWidth > maxSide || sourceDecoder.PixelHeight > maxSide)
            {
                var scaled = Fit(sourceDecoder.PixelWidth, sourceDecoder.PixelHeight, maxSide);

                transform.ScaledWidth = (uint)scaled.Width;
                transform.ScaledHeight = (uint)scaled.Height;
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
