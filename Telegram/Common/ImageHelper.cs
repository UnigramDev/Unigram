//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Brushes;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Telegram.Controls;
using Telegram.Entities;
using Telegram.Native;
using Windows.Foundation;
using Windows.Graphics;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI;
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
        /// Re-encodes an image so that neither side exceeds
        /// <paramref name="requestedMinSide"/>.
        /// </summary>
        /// <param name="requestedMinSide">
        /// Upper bound on width and height, despite the name. Zero leaves the source
        /// size alone.
        /// </param>
        /// <param name="bestQuality">Fant interpolation rather than linear.</param>
        /// <param name="trimStart">
        /// For video sources, the position to take the frame from.
        /// </param>
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
                //var qualityValue = new BitmapTypedValue(0.77, Windows.Foundation.PropertyType.Single);
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
                        // preview is false on purpose. It sets AVFMT_FLAG_NOBUFFER, which the header-only
                        // probe in StorageVideo.CreateAsync wants -- but unbuffered often fails to grab the
                        // first frame, which is the whole point here.
                        using var animation = VideoAnimation.LoadFromFile(new VideoAnimationStreamSource(videoStream), false, false, false);

                        var scaled = Clamp(animation.PixelWidth, animation.PixelHeight, requestedMinSide);

                        width = (int)scaled.Width;
                        height = (int)scaled.Height;

                        var frame = BufferSurface.Create((uint)(width * height * 4));

                        // On failure the buffer is left holding uninitialised memory.
                        return animation.RenderSync(frame, width, height, true, out _)
                            ? frame
                            : null;
                    });

                    if (buffer != null && width > 0 && height > 0)
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
            catch (Exception ex)
            {
                // Callers treat null as "no preview" and substitute a placeholder.
                Logger.Error(ex);
            }

            return null;
        }

        /// <summary>
        /// Decodes a still preview, bounded to <paramref name="requestedMinSide"/> on its
        /// longer side.
        ///
        /// Single frame only: SoftwareBitmapSource holds one, so an animated GIF comes
        /// back static. No GIF reaches this today — StorageThumbnailCache previews photos
        /// through a BitmapImage with DecodePixelWidth and only calls the overload above for
        /// video, leaving its StoragePhoto branch untaken — but a caller that changes
        /// that needs a FrameCount > 1 branch returning a BitmapImage, which can carry
        /// the same bound through DecodePixelWidth and DecodePixelHeight.
        /// </summary>
        public static async Task<ImageSource> GetPreviewBitmapAsync(IRandomAccessStream source, int requestedMinSide = 1280)
        {
            var decoder = await BitmapDecoder.CreateAsync(source);

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

            // Bgra8 premultiplied whatever the file holds: the decoder converts, and it
            // is the only combination SoftwareBitmapSource accepts. Asking for
            // decoder.BitmapPixelFormat instead meant every other format had to fall
            // through to a BitmapImage that decoded at full size, ignoring
            // requestedMinSide.
            var bitmap = await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied, transform, ExifOrientationMode.RespectExifOrientation, ColorManagementMode.DoNotColorManage);

            var bitmapImage = new SoftwareBitmapSource();
            await bitmapImage.SetBitmapAsync(bitmap);

            return bitmapImage;
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

        public static async Task<StorageFile> CropAsync(StorageFile sourceFile, StorageFile file, Rect cropRectangle, int min = 1280, int max = 0, ImageRotation rotation = ImageRotation.None, ImageFlip flip = ImageFlip.None, TimeSpan? trimStart = null, bool bestQuality = false)
        {
            file ??= await ApplicationData.Current.TemporaryFolder.CreateFileAsync("crop.jpg", CreationCollisionOption.ReplaceExisting);

            using (var source = await OpenReadAsync(sourceFile, trimStart))
            using (var destination = await file.OpenAsync(FileAccessMode.ReadWrite))
            {
                var decoder = await BitmapDecoder.CreateAsync(source);
                var transform = ComputeCropTransform(decoder, cropRectangle, rotation, flip, min, max,
                    bestQuality ? BitmapInterpolationMode.Fant : BitmapInterpolationMode.Linear);

                var pixelData = await decoder.GetSoftwareBitmapAsync(decoder.BitmapPixelFormat, decoder.BitmapAlphaMode, transform, ExifOrientationMode.RespectExifOrientation, ColorManagementMode.DoNotColorManage);

                // Not using ATM, quality is too low
                //var propertySet = new BitmapPropertySet();
                //var qualityValue = new BitmapTypedValue(0.77, PropertyType.Single);
                //propertySet.Add("ImageQuality", qualityValue);

                var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.JpegEncoderId, destination);
                encoder.SetSoftwareBitmap(pixelData);
                await encoder.FlushAsync();
            }

            return file;
        }

        /// <summary>
        /// Rotates a rectangle clockwise within a <paramref name="width"/> by
        /// <paramref name="height"/> space, <paramref name="count"/> quarter turns.
        /// The space's own dimensions swap with every turn.
        /// </summary>
        public static Rect RotateArea(Rect area, double width, double height, int count)
        {
            count = count % 4;

            for (int i = 0; i < count; i++)
            {
                area = new Rect(height - area.Bottom, area.X, area.Height, area.Width);

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
                // preview is false on purpose. It sets AVFMT_FLAG_NOBUFFER, which the header-only
                // probe in StorageVideo.CreateAsync wants -- but unbuffered often fails to grab the
                // first frame, which is the whole point here.
                using var animation = await Task.Run(() => VideoAnimation.LoadFromFile(new VideoAnimationStreamSource(videoStream), false, false, false));

                if (generation.TrimStartTime is TimeSpan trimStart && trimStart > TimeSpan.Zero)
                {
                    animation.SeekToMilliseconds((long)trimStart.TotalMilliseconds, false);
                }

                int width = animation.PixelWidth;
                int height = animation.PixelHeight;

                var frame = BufferSurface.Create((uint)(width * height * 4));

                // Throws rather than returning null: StorageThumbnailCache recovers by
                // falling back to the unedited preview, and does so from a catch.
                if (!await Task.Run(() => animation.RenderSync(frame, width, height, true, out _)))
                {
                    throw new InvalidOperationException($"No frame could be rendered from '{source.File.Name}'.");
                }

                using var stream = new InMemoryRandomAccessStream();
                Direct2D.Shared.Encode(frame, stream, width, height, animation.Rotation);

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
            if (generation.RequiresRender)
            {
                using var target = await RenderRotatedAsync(source, generation, maxSize, 0, false);

                var software = await SoftwareBitmap.CreateCopyFromSurfaceAsync(target, BitmapAlphaMode.Premultiplied);
                var rotated = new SoftwareBitmapSource();
                await rotated.SetBitmapAsync(software);

                return rotated;
            }

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

        /// <summary>
        /// Writes a free-angle crop, strokes and objects included, as a JPEG.
        /// </summary>
        public static async Task<StorageFile> CropRotatedAsync(StorageFile sourceFile, StorageFile file, ImageGeneration generation, int min = 1280, int max = 0)
        {
            using (var source = await OpenReadAsync(sourceFile, generation.TrimStartTime))
            using (var target = await RenderRotatedAsync(source, generation, min, max, true))
            using (var destination = await file.OpenAsync(FileAccessMode.ReadWrite))
            {
                await target.SaveAsync(destination, CanvasBitmapFileFormat.Jpeg);
            }

            return file;
        }

        /// <summary>
        /// A story's frame as it is posted, gradient and media only, at <paramref name="size"/>,
        /// with a video's frame at <paramref name="time"/>.
        /// </summary>
        public static async Task<SoftwareBitmap> RenderFrameAsync(StorageFile sourceFile, ImageGeneration generation, Size size, TimeSpan time)
        {
            var frame = new ImageGeneration
            {
                Rectangle = generation.Rectangle,
                Rotation = generation.Rotation,
                Flip = generation.Flip,
                Angle = generation.Angle,
                FrameSize = size,
                FrameTopColor = generation.FrameTopColor,
                FrameBottomColor = generation.FrameBottomColor
            };

            using (var source = await OpenReadAsync(sourceFile, time, true))
            using (var target = await RenderRotatedAsync(source, frame, 0, 0, false))
            {
                var width = (int)target.SizeInPixels.Width;
                var height = (int)target.SizeInPixels.Height;

                var bitmap = SoftwareBitmap.CreateCopyFromBuffer(target.GetPixelBytes().AsBuffer(), BitmapPixelFormat.Bgra8, width, height, BitmapAlphaMode.Premultiplied);
                bitmap.DpiX = 96;
                bitmap.DpiY = 96;

                return bitmap;
            }
        }

        /// <summary>
        /// The decoder only turns by quarters, so a free angle is drawn by Win2D instead. The image
        /// is decoded upright, exactly as ImageCropper previews it, and one matrix then takes it
        /// through the mirror, the quarter turns, the crop and the angle — strokes and objects too,
        /// since they are stored against the upright image.
        /// </summary>
        private static async Task<CanvasRenderTarget> RenderRotatedAsync(IRandomAccessStream source, ImageGeneration generation, int min, int max, bool bestQuality)
        {
            var decoder = await BitmapDecoder.CreateAsync(source);

            var quarter = generation.Rotation is ImageRotation.Clockwise90Degrees or ImageRotation.Clockwise270Degrees;
            var rectangle = generation.Rectangle;

            double orientedWidth = quarter ? decoder.OrientedPixelHeight : decoder.OrientedPixelWidth;
            double orientedHeight = quarter ? decoder.OrientedPixelWidth : decoder.OrientedPixelHeight;

            var crop = new Rect(0, 0, rectangle.Width * orientedWidth, rectangle.Height * orientedHeight);

            int outputWidth;
            int outputHeight;

            // A story is posted at its frame's exact size.
            if (generation.HasFrame)
            {
                outputWidth = (int)generation.FrameSize.Width;
                outputHeight = (int)generation.FrameSize.Height;
            }
            else
            {
                var output = Scale(crop, new Size(orientedWidth, orientedHeight), min, max).Crop;

                outputWidth = Math.Max(1, (int)Math.Round(output.Width));
                outputHeight = Math.Max(1, (int)Math.Round(output.Height));
            }

            // Never decode larger than the output needs; Win2D scales up when the crop is small.
            var factor = Math.Min(1, outputWidth / crop.Width);
            var transform = new BitmapTransform
            {
                ScaledWidth = (uint)Math.Max(1, Math.Round(decoder.PixelWidth * factor)),
                ScaledHeight = (uint)Math.Max(1, Math.Round(decoder.PixelHeight * factor)),
                InterpolationMode = bestQuality ? BitmapInterpolationMode.Fant : BitmapInterpolationMode.Linear
            };

            using var pixels = await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied, transform, ExifOrientationMode.RespectExifOrientation, ColorManagementMode.DoNotColorManage);

            var uprightWidth = (float)pixels.PixelWidth;
            var uprightHeight = (float)pixels.PixelHeight;

            var width = quarter ? uprightHeight : uprightWidth;
            var height = quarter ? uprightWidth : uprightHeight;

            var cropWidth = rectangle.Width * width;
            var center = new Vector2((float)((rectangle.X + rectangle.Width / 2) * width), (float)((rectangle.Y + rectangle.Height / 2) * height));

            var matrix = GetOrientationMatrix(generation.Rotation, generation.Flip, uprightWidth, uprightHeight)
                * Matrix3x2.CreateTranslation(-center)
                * Matrix3x2.CreateRotation((float)(generation.Angle * Math.PI / 180))
                * Matrix3x2.CreateScale((float)(outputWidth / cropWidth))
                * Matrix3x2.CreateTranslation(outputWidth / 2f, outputHeight / 2f);

            // Strokes and objects are laid out on the upright image, which the matrix carries, or in
            // a story's frame, which only scales to the output. Only the image mirrors them.
            var layerSize = generation.HasFrame
                ? new Vector2((float)generation.FrameSize.Width, (float)generation.FrameSize.Height)
                : new Vector2(uprightWidth, uprightHeight);

            var layerScale = generation.HasFrame
                ? outputWidth / layerSize.X
                : (float)(outputWidth / cropWidth);

            var layer = generation.HasFrame
                ? Matrix3x2.CreateScale(layerScale)
                : matrix;

            var mirrored = !generation.HasFrame && generation.Flip != ImageFlip.None;

            var device = ElementComposition.GetSharedDevice();
            var target = new CanvasRenderTarget(device, outputWidth, outputHeight, 96);

            // Win2D sizes the bitmap by the file's DPI. Scaling it back up at draw time is not
            // enough: HighQualityCubic draws through an effect that first resamples the bitmap to
            // that size, so a 300 dpi photo would lose two thirds of its resolution.
            pixels.DpiX = 96;
            pixels.DpiY = 96;

            try
            {
                using (var bitmap = CanvasBitmap.CreateFromSoftwareBitmap(device, pixels))
                using (var session = target.CreateDrawingSession())
                {
                    if (generation.HasFrame)
                    {
                        using var fill = new CanvasLinearGradientBrush(device, generation.FrameTopColor, generation.FrameBottomColor)
                        {
                            StartPoint = Vector2.Zero,
                            EndPoint = new Vector2(0, outputHeight)
                        };

                        session.FillRectangle(0, 0, outputWidth, outputHeight, fill);
                    }
                    else
                    {
                        session.Clear(Colors.Black);
                    }

                    session.Transform = matrix;
                    session.DrawImage(bitmap, 0, 0, bitmap.Bounds, 1, bestQuality ? CanvasImageInterpolation.HighQualityCubic : CanvasImageInterpolation.Linear);
                }

                if (generation.Strokes is { Count: > 0 } strokes)
                {
                    // Their own target, because the eraser draws transparency with a Copy blend.
                    using var overlay = new CanvasRenderTarget(device, outputWidth, outputHeight, 96);
                    using var blur = PencilCanvas.CreateBlur(target, layerSize.X * layerScale);
                    using var brush = new CanvasImageBrush(device, blur)
                    {
                        SourceRectangle = new Rect(0, 0, outputWidth, outputHeight)
                    };

                    using (var session = overlay.CreateDrawingSession())
                    {
                        session.Transform = layer;

                        foreach (var builder in strokes)
                        {
                            PencilCanvas.DrawPath(session, builder, layerSize, brush);
                        }
                    }

                    using (var session = target.CreateDrawingSession())
                    {
                        session.DrawImage(overlay);
                    }
                }

                if (generation.Entities is { Count: > 0 } entities)
                {
                    // Stickers go in as their first frame, at about the size they are drawn.
                    var pixelsPerUnit = layerSize.X / PaintEntityRenderer.Reference * layerScale;
                    var stickers = await PaintEntityRenderer.LoadStickersAsync(device, entities, pixelsPerUnit);

                    try
                    {
                        using (var session = target.CreateDrawingSession())
                        {
                            session.Transform = layer;
                            PaintEntityRenderer.Draw(session, entities, layerSize, mirrored, stickers);
                        }
                    }
                    finally
                    {
                        if (stickers != null)
                        {
                            foreach (var bitmap in stickers.Values)
                            {
                                bitmap.Dispose();
                            }
                        }
                    }
                }
            }
            catch
            {
                // Owned here until it is returned.
                target.Dispose();
                throw;
            }

            return target;
        }

        /// <summary>
        /// Upright pixels to oriented pixels: the flip, then the clockwise quarter turns, the order
        /// <see cref="BitmapTransform"/> applies them in.
        /// </summary>
        public static Matrix3x2 GetOrientationMatrix(ImageRotation rotation, ImageFlip flip, float width, float height)
        {
            var matrix = flip switch
            {
                ImageFlip.Horizontal => new Matrix3x2(-1, 0, 0, 1, width, 0),
                ImageFlip.Vertical => new Matrix3x2(1, 0, 0, -1, 0, height),
                _ => Matrix3x2.Identity
            };

            return rotation switch
            {
                ImageRotation.Clockwise90Degrees => matrix * new Matrix3x2(0, 1, -1, 0, height, 0),
                ImageRotation.Clockwise180Degrees => matrix * new Matrix3x2(-1, 0, 0, -1, width, height),
                ImageRotation.Clockwise270Degrees => matrix * new Matrix3x2(0, -1, 1, 0, 0, width),
                _ => matrix
            };
        }

        // Not precise, a video's frame is the keyframe at or before trimStart.
        public static async Task<IRandomAccessStream> OpenReadAsync(StorageFile sourceFile, TimeSpan? trimStart = null, bool precise = false)
        {
            // The same set EditMediaPopup uses to decide whether a file is a video, so
            // the two cannot disagree about what reaches the frame extraction below.
            if (sourceFile.HasExtension(Constants.VideoTypes))
            {
                return await Task.Run(async () =>
                {
                    using var videoStream = await sourceFile.OpenReadAsync();
                    // preview is false on purpose. It sets AVFMT_FLAG_NOBUFFER, which the header-only
                    // probe in StorageVideo.CreateAsync wants -- but unbuffered often fails to grab the
                    // first frame, which is the whole point here.
                    using var animation = VideoAnimation.LoadFromFile(new VideoAnimationStreamSource(videoStream), false, false, false);

                    if (trimStart > TimeSpan.Zero)
                    {
                        animation.SeekToMilliseconds((long)trimStart.Value.TotalMilliseconds, false);
                    }

                    int width = animation.PixelWidth;
                    int height = animation.PixelHeight;

                    var frame = BufferSurface.Create((uint)(width * height * 4));

                    // Callers pass the result straight to BitmapDecoder, so returning null
                    // would only move the failure. GenerationService and ImageCropper
                    // both catch this.
                    if (!animation.RenderSync(frame, width, height, true, out double seconds))
                    {
                        throw new InvalidOperationException($"No frame could be rendered from '{sourceFile.Name}'.");
                    }

                    // A failed render leaves the buffer alone, so it keeps the last frame reached.
                    while (precise && seconds * 1000 < trimStart?.TotalMilliseconds)
                    {
                        if (!animation.RenderSync(frame, width, height, true, out seconds))
                        {
                            break;
                        }
                    }

                    var stream = new InMemoryRandomAccessStream();
                    Direct2D.Shared.Encode(frame, stream, width, height, animation.Rotation);

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

            using var bitmap = CanvasBitmap.CreateFromSoftwareBitmap(device, file);
            using var target = DrawStrokes(device, bitmap, strokes, rectangle, rotation, flip);

            var stream = new InMemoryRandomAccessStream();

            try
            {
                await target.SaveAsync(stream, CanvasBitmapFileFormat.Jpeg/*, 0.77f*/);
            }
            catch
            {
                // Owned here until it is returned.
                stream.Dispose();
                throw;
            }

            stream.Seek(0);
            return stream;
        }

        public static async Task<StorageFile> DrawStrokesAsync(StorageFile file, IReadOnlyList<SmoothPathBuilder> strokes, Rect rectangle, ImageRotation rotation, ImageFlip flip)
        {
            var device = ElementComposition.GetSharedDevice();

            CanvasRenderTarget target;

            // Scoped so the bitmap is released before the file is reopened for writing,
            // since it was loaded from that same path.
            using (var bitmap = await CanvasBitmap.LoadAsync(device, file.Path))
            {
                target = DrawStrokes(device, bitmap, strokes, rectangle, rotation, flip);
            }

            using (target)
            using (var stream = await file.OpenAsync(FileAccessMode.ReadWrite))
            {
                await target.SaveAsync(stream, CanvasBitmapFileFormat.Jpeg/*, 0.77f*/);
            }

            return file;
        }

        /// <summary>
        /// Composites <paramref name="strokes"/> over <paramref name="bitmap"/> and
        /// returns the result.
        ///
        /// The caller owns both the bitmap passed in and the render target returned.
        /// </summary>
        public static CanvasRenderTarget DrawStrokes(CanvasDevice device, CanvasBitmap bitmap, IReadOnlyList<SmoothPathBuilder> strokes, Rect rectangle, ImageRotation rotation, ImageFlip flip)
        {
            // The strokes are drawn into their own target so the transform below applies
            // to them alone, then composited over the bitmap.
            using var overlay = new CanvasRenderTarget(device, (float)bitmap.Size.Width, (float)bitmap.Size.Height, bitmap.Dpi);

            var target = new CanvasRenderTarget(device, (float)bitmap.Size.Width, (float)bitmap.Size.Height, bitmap.Dpi);

            try
            {
                DrawStrokes(overlay, target, bitmap, strokes, rectangle, rotation, flip);
            }
            catch
            {
                // Owned here until it is returned.
                target.Dispose();
                throw;
            }

            return target;
        }

        private static void DrawStrokes(CanvasRenderTarget overlay, CanvasRenderTarget target, CanvasBitmap bitmap, IReadOnlyList<SmoothPathBuilder> strokes, Rect rectangle, ImageRotation rotation, ImageFlip flip)
        {
            var size = overlay.Size.ToVector2();
            var canvasSize = overlay.Size.ToVector2();

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

            using (var session = overlay.CreateDrawingSession())
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

                using var blur = PencilCanvas.CreateBlur(bitmap, (float)(bitmap.Size.Width / rectangle.Width));
                using var brush = new CanvasImageBrush(overlay, blur)
                {
                    SourceRectangle = new Rect(0, 0, canvasSize.X, canvasSize.Y)
                };

                foreach (var builder in strokes)
                {
                    PencilCanvas.DrawPath(session, builder, size, brush);
                }
            }

            using (var session = target.CreateDrawingSession())
            {
                session.DrawImage(bitmap);
                session.DrawImage(overlay);
            }
        }
    }
}
