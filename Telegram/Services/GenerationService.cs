//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Microsoft.Graphics.Canvas;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Entities;
using Telegram.Native;
using Telegram.Native.Media;
using Telegram.Native.Opus;
using Telegram.Streams;
using Telegram.Td.Api;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Streams;
using Windows.UI.Xaml.Hosting;

namespace Telegram.Services
{
    public interface IGenerationService
    {

    }

    public enum ConversionType
    {
        Copy,
        Compress,
        HighQuality,
        Screenshot,
        Opus,
        Transcode,
        TranscodeThumbnail,
        DocumentThumbnail,
        AlbumCover,
        // TDLib
        Url
    }

    public partial class GenerationService : IGenerationService
    //, IHandle<UpdateFileGenerationStart>
    //, IHandle<UpdateFileGenerationStop>
    {
        private readonly IClientService _clientService;
        private readonly IEventAggregator _aggregator;

        // The transcodes under way, so that TDLib stopping a generation stops its work.
        private readonly ConcurrentDictionary<long, VideoTranscoder> _transcoders = new();

        public GenerationService(IClientService clientService, IEventAggregator aggregator)
        {
            _clientService = clientService;
            _aggregator = aggregator;

            _aggregator.Subscribe<UpdateFileGenerationStart>(this, Handle)
                .Subscribe<UpdateFileGenerationStop>(Handle);
        }

        /// <summary>
        /// Turns a file the user picked into the input TDLib should be given for it: a path where
        /// TDLib can read it directly, and a generation otherwise.
        /// </summary>
        public static async Task<InputFile> PrepareAsync(StorageFile file, ConversionType conversion = ConversionType.Copy, string arguments = null, bool forceCopy = false)
        {
            // Taken even for the branches that never generate: TDLib then uploads straight from the
            // user's own path, and the picker's grant on it dies with the session, so an upload the
            // user reopens the app to resume would find nothing to read.
            var token = GenerationSources.Add(file);
            var path = file.Path;

            if (NativeUtils.IsFileReadable(path, out long fileSize, out long fileTime))
            {
                if (conversion == ConversionType.Copy && arguments == null && !forceCopy)
                {
                    return new InputFileLocal(path);
                }

                if (conversion == ConversionType.Compress)
                {
                    path = Path.ChangeExtension(path, ".jpg");
                }

                return new InputFileGenerated(path, string.Format("{0}#{1}#{2}#{3}", token, conversion, arguments, fileTime), fileSize);
            }

            if (string.IsNullOrEmpty(path))
            {
                path = file.FolderRelativeId;
            }

            if (conversion == ConversionType.Compress)
            {
                path = Path.ChangeExtension(path, ".jpg");
            }

            try
            {
                var props = await file.GetBasicPropertiesAsync();
                return new InputFileGenerated(path, string.Format("{0}#{1}#{2}#{3:s}", token, conversion, arguments, props.DateModified), (long)props.Size);
            }
            catch
            {
                return new InputFileGenerated(path, string.Format("{0}#{1}#{2}#{3}", token, conversion, arguments, 0), 0);
            }
        }

        public static async Task<InputThumbnail> PrepareVideoThumbnailAsync(StorageVideo file, VideoGeneration video = null, string arguments = null)
        {
            double originalWidth = file.Width;
            double originalHeight = file.Height;

            if (video.HasOutputSize)
            {
                var output = GetVideoOutputSize(video, originalWidth, originalHeight);

                originalWidth = output.Width;
                originalHeight = output.Height;
            }
            else if (!video.CropRectangle.IsEmpty)
            {
                originalWidth = video.CropRectangle.Width;
                originalHeight = video.CropRectangle.Height;
            }

            double ratioX = 90 / originalWidth;
            double ratioY = 90 / originalHeight;
            double ratio = Math.Min(ratioX, ratioY);

            int width = (int)(originalWidth * ratio);
            int height = (int)(originalHeight * ratio);

            return new InputThumbnail(await PrepareAsync(file.File, ConversionType.TranscodeThumbnail, arguments), width, height);
        }

        public async void Handle(UpdateFileGenerationStart update)
        {
            var args = update.Conversion.Split('#', StringSplitOptions.RemoveEmptyEntries);
            if (args.Length < 2)
            {
                _clientService.Send(new FinishFileGeneration(update.GenerationId, new Error(500, "Invalid generation arguments")));
                return;
            }

            // A conversion that took the app down is not tried again as it was: TDLib would resume
            // it on every launch, and the app would never stay up. A transcode gets one more go
            // without the hardware encoder, the one part of it that runs a vendor's code.
            var crashes = GenerationGuard.Begin(update.Conversion);
            var tolerated = args[1].Equals(nameof(ConversionType.Transcode), StringComparison.OrdinalIgnoreCase) ? 1 : 0;

            if (crashes > tolerated)
            {
                Logger.Error("The last attempt at this generation closed the app");

                GenerationGuard.End(update.Conversion);
                _clientService.Send(new FinishFileGeneration(update.GenerationId, new Error(500, "FILE_GENERATE_LOCATION_INVALID The last attempt to prepare this file closed the app")));
                return;
            }

            try
            {
                await HandleAsync(update, args, crashes == 0);
            }
            finally
            {
                GenerationGuard.End(update.Conversion);
            }
        }

        private async Task HandleAsync(UpdateFileGenerationStart update, string[] args, bool allowHardware)
        {
            if (Enum.TryParse(args[1], true, out ConversionType conversion))
            {
                // Url is the only conversion requested by TDLib
                if (conversion == ConversionType.Url)
                {
                    await DownloadAsync(update);
                }
                else
                {
                    // TODO: unify some stuff, such as retrieving source and destination file,
                    // deleting the temp files, updating the future access list.

                    // TODO: figure out a way to remove the file from the future access list:
                    // the same file can go through multiple generations simultaneously.

                    if (conversion == ConversionType.Copy)
                    {
                        await CopyAsync(update, args);
                    }
                    else if (conversion == ConversionType.Compress || conversion == ConversionType.HighQuality)
                    {
                        await CompressAsync(update, conversion == ConversionType.HighQuality, args);
                    }
                    else if (conversion == ConversionType.Screenshot)
                    {
                        await ScreenshotAsync(update, args);
                    }
                    else if (conversion == ConversionType.Opus)
                    {
                        await TranscodeOpusAsync(update, args);
                    }
                    else if (conversion == ConversionType.Transcode)
                    {
                        await TranscodeAsync(update, args, allowHardware);
                    }
                    else if (conversion == ConversionType.TranscodeThumbnail)
                    {
                        await ThumbnailTranscodeAsync(update, args);
                    }
                    else if (conversion == ConversionType.DocumentThumbnail)
                    {
                        await ThumbnailDocumentAsync(update, args);
                    }
                    else if (conversion == ConversionType.AlbumCover)
                    {
                        await AlbumCoverAsync(update, args);
                    }
                }
            }
            else
            {
                _clientService.Send(new FinishFileGeneration(update.GenerationId, new Error(500, "FILE_GENERATE_LOCATION_INVALID Unknown conversion type")));
            }
        }

        public void Handle(UpdateFileGenerationStop update)
        {
            if (_transcoders.TryGetValue(update.GenerationId, out VideoTranscoder transcoder))
            {
                transcoder.Cancel();
            }
        }

        private bool IsTemporary(StorageFile source)
        {
            if (string.IsNullOrEmpty(source.Path))
            {
                return false;
            }

            var path1 = Path.GetDirectoryName(source.Path).TrimEnd('\\');
            var path2 = ApplicationData.Current.TemporaryFolder.Path.TrimEnd('\\');

            return string.Equals(path1, path2, StringComparison.OrdinalIgnoreCase);
        }

        private async Task DownloadAsync(UpdateFileGenerationStart update)
        {
            try
            {
                if (!Uri.TryCreate(update.OriginalPath, UriKind.Absolute, out Uri result))
                {
                    return;
                }

                var client = new HttpClient();
                client.DefaultRequestHeaders.ExpectContinue = false;
                var temp = await StorageFile.GetFileFromPathAsync(update.DestinationPath);
                var request = new HttpRequestMessage(HttpMethod.Get, result);
                var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
                var length = int.Parse(response.Content.Headers.GetValues("Content-Length").FirstOrDefault());

                _clientService.Send(new SetFileGenerationProgress(update.GenerationId, length, 0));

                using (var fs = await temp.OpenAsync(FileAccessMode.ReadWrite))
                {
                    using (var stream = await response.Content.ReadAsStreamAsync())
                    {
                        var inputStream = stream.AsInputStream();
                        int totalBytesRead = 0;
                        while (true)
                        {
                            // Read from the web.
                            IBuffer buffer = new Windows.Storage.Streams.Buffer(1024);
                            buffer = await inputStream.ReadAsync(
                                buffer,
                                buffer.Capacity,
                                InputStreamOptions.None);

                            if (buffer.Length == 0)
                            {
                                // There is nothing else to read.
                                break;
                            }

                            // Report progress.
                            totalBytesRead += (int)buffer.Length;
                            _clientService.Send(new SetFileGenerationProgress(update.GenerationId, length, totalBytesRead));

                            // Write to file.
                            await fs.WriteAsync(buffer);
                        }

                        inputStream.Dispose();
                        fs.Dispose();
                    }
                }

                _clientService.Send(new FinishFileGeneration(update.GenerationId, null));
            }
            catch (Exception ex)
            {
                _clientService.Send(new FinishFileGeneration(update.GenerationId, new Error(500, "FILE_GENERATE_LOCATION_INVALID " + ex.ToString())));
            }
        }

        private async Task CopyAsync(UpdateFileGenerationStart update, string[] args)
        {
            try
            {
                var file = await GenerationSources.GetFileAsync(args[0]);
                var temp = await StorageFile.GetFileFromPathAsync(update.DestinationPath);

                if (IsTemporary(file))
                {
                    await file.MoveAndReplaceAsync(temp);
                }
                else
                {
                    await file.CopyAndReplaceAsync(temp);
                }

                _clientService.Send(new FinishFileGeneration(update.GenerationId, null));
            }
            catch (Exception ex)
            {
                _clientService.Send(new FinishFileGeneration(update.GenerationId, new Error(500, "FILE_GENERATE_LOCATION_INVALID " + ex.ToString())));
            }

            //StorageApplicationPermissions.FutureAccessList.Remove(args[0]);
        }

        private async Task CompressAsync(UpdateFileGenerationStart update, bool highQuality, string[] args)
        {
            try
            {
                var file = await GenerationSources.GetFileAsync(args[0]);
                var temp = await StorageFile.GetFileFromPathAsync(update.DestinationPath);

                var maxSize = highQuality
                    ? Constants.ImageHighQuality
                    : Constants.ImageStandardQuality;

                if (args.Length > 3)
                {
                    var generation = JsonSerializer.Deserialize(args[2], GenerationJsonContext.Default.ImageGeneration);
                    var rectangle = generation.Rectangle;

                    if (generation.RequiresRender)
                    {
                        // A photo that would go out without one of its stickers fails instead.
                        if (!await PaintEntityRenderer.LoadFilesAsync(_clientService, generation.Entities))
                        {
                            throw new InvalidOperationException("A sticker drawn on the photo could not be downloaded.");
                        }

                        await ImageHelper.CropRotatedAsync(file, temp, generation, maxSize, generation.MinimumSize);
                    }
                    else
                    {
                        await ImageHelper.CropAsync(file, temp, rectangle, maxSize, generation.MinimumSize, rotation: generation.Rotation, flip: generation.Flip, bestQuality: true);

                        var drawing = generation.Strokes;
                        if (drawing != null && drawing.Count > 0)
                        {
                            await ImageHelper.DrawStrokesAsync(temp, drawing, rectangle, generation.Rotation, generation.Flip);
                        }
                    }
                }
                else
                {
                    await ImageHelper.ScaleAsync(BitmapEncoder.JpegEncoderId, file, temp, maxSize, true);
                }

                _clientService.Send(new FinishFileGeneration(update.GenerationId, null));

                if (IsTemporary(file))
                {
                    await file.DeleteAsync();
                }
            }
            catch (Exception ex)
            {
                _clientService.Send(new FinishFileGeneration(update.GenerationId, new Error(500, "FILE_GENERATE_LOCATION_INVALID " + ex.ToString())));
            }

            //StorageApplicationPermissions.FutureAccessList.Remove(args[0]);
        }

        private async Task ScreenshotAsync(UpdateFileGenerationStart update, string[] args)
        {
            try
            {
                var file = await GenerationSources.GetFileAsync(args[0]);
                var temp = await StorageFile.GetFileFromPathAsync(update.DestinationPath);

                await ImageHelper.ScaleAsync(BitmapEncoder.PngEncoderId, file, temp, 0, true);

                _clientService.Send(new FinishFileGeneration(update.GenerationId, null));

                if (IsTemporary(file))
                {
                    await file.DeleteAsync();
                }
            }
            catch (Exception ex)
            {
                _clientService.Send(new FinishFileGeneration(update.GenerationId, new Error(500, "FILE_GENERATE_LOCATION_INVALID " + ex.ToString())));
            }

            //StorageApplicationPermissions.FutureAccessList.Remove(args[0]);
        }

        private async Task ThumbnailAsync(UpdateFileGenerationStart update, string[] args)
        {
            try
            {
                var file = await GenerationSources.GetFileAsync(args[0]);
                var temp = await StorageFile.GetFileFromPathAsync(update.DestinationPath);

                if (args.Length > 3)
                {
                    var rect = JsonSerializer.Deserialize(args[2], GenerationJsonContext.Default.Rect);
                    await ImageHelper.CropAsync(file, temp, rect, 90);
                }
                else
                {
                    await ImageHelper.ScaleAsync(BitmapEncoder.JpegEncoderId, file, temp, 90, false);
                }

                _clientService.Send(new FinishFileGeneration(update.GenerationId, null));
            }
            catch (Exception ex)
            {
                _clientService.Send(new FinishFileGeneration(update.GenerationId, new Error(500, "FILE_GENERATE_LOCATION_INVALID " + ex.ToString())));
            }

            //StorageApplicationPermissions.FutureAccessList.Remove(args[0]);
        }

        private async Task TranscodeOpusAsync(UpdateFileGenerationStart update, string[] args)
        {
            try
            {
                var file = await GenerationSources.GetFileAsync(args[0]);

                using (var opus = new OpusOutput(update.DestinationPath))
                {
                    if (opus.IsValid)
                    {
                        opus.Transcode(file.Path);
                    }
                    else
                    {
                        _clientService.Send(new FinishFileGeneration(update.GenerationId, new Error(500, "FILE_GENERATE_LOCATION_INVALID can't access the file")));
                        return;
                    }
                }

                _clientService.Send(new FinishFileGeneration(update.GenerationId, null));

                if (IsTemporary(file))
                {
                    await file.DeleteAsync();
                }
            }
            catch (Exception ex)
            {
                _clientService.Send(new FinishFileGeneration(update.GenerationId, new Error(500, "FILE_GENERATE_LOCATION_INVALID " + ex.ToString())));
            }

            //StorageApplicationPermissions.FutureAccessList.Remove(args[0]);
        }

        // The longest side a cropped video is encoded at when no quality was picked.
        private const double VideoMaxSide = 1920;

        private async Task TranscodeAsync(UpdateFileGenerationStart update, string[] args, bool allowHardware)
        {
            try
            {
                var generation = JsonSerializer.Deserialize(args[2], GenerationJsonContext.Default.VideoGeneration);
                if (!generation.Mute && !generation.Transcode)
                {
                    await CopyAsync(update, args);
                    return;
                }

                var file = await GenerationSources.GetFileAsync(args[0]);
                if (string.IsNullOrEmpty(file.Path))
                {
                    throw new InvalidOperationException("The video has no path to read it from");
                }

                var probe = await ProbeVideoAsync(file);
                var output = GetVideoOutputSize(generation, probe.Width, probe.Height);
                var transform = GetVideoTransform(generation, (float)probe.Width, (float)probe.Height, output);

                var options = new VideoTranscoderOptions
                {
                    InputPath = file.Path,
                    OutputPath = update.DestinationPath,
                    StartTime = (long)(generation.TrimStartTime?.TotalMilliseconds ?? 0),
                    StopTime = (long)(generation.TrimStopTime?.TotalMilliseconds ?? 0),
                    Mute = generation.Mute,
                    OutputWidth = (int)output.Width,
                    OutputHeight = (int)output.Height,
                    Transform = transform,
                    VideoBitrate = generation.VideoBitrate != 0
                        ? (int)generation.VideoBitrate
                        : MakeVideoBitrate(probe.Height, probe.Width, probe.Bitrate, (int)output.Height, (int)output.Width),
                    MaxFrameRate = 60,
                    HardwareEncoding = allowHardware && !AppSettings.Diagnostics.DisableHardwareVideoEncoding
                };

                var transcoder = new VideoTranscoder();
                _transcoders[update.GenerationId] = transcoder;

                var animations = new List<IDisposable>();

                try
                {
                    if (generation.HasOverlay)
                    {
                        await AddOverlaysAsync(options, generation, output, transform, new Vector2((float)probe.Width, (float)probe.Height), animations);
                    }

                    var id = update.GenerationId;
                    var handler = new VideoTranscoderProgressHandler(progress => _clientService.Send(new SetFileGenerationProgress(id, 100, (long)(progress * 100))));

                    await Task.Run(() => transcoder.Transcode(options, handler));
                }
                finally
                {
                    _transcoders.TryRemove(update.GenerationId, out _);

                    foreach (var animation in animations)
                    {
                        animation.Dispose();
                    }
                }

                _clientService.Send(new FinishFileGeneration(update.GenerationId, null));
            }
            catch (Exception ex)
            {
                _clientService.Send(new FinishFileGeneration(update.GenerationId, new Error(500, "FILE_GENERATE_LOCATION_INVALID " + ex.ToString())));
            }
        }

        // The phone's cap on an animated sticker's frames, in pixels a side.
        private const int OverlayAnimationSide = 512;

        // The edit, drawn over the video in the order it was made: strokes and still objects baked
        // into one image per run, the size of the output, and each animated sticker a layer of its
        // own between them, decoded frame by frame as the video goes.
        private async Task AddOverlaysAsync(VideoTranscoderOptions options, VideoGeneration generation, Size output, Matrix3x2 transform, Vector2 upright, List<IDisposable> animations)
        {
            var entities = generation.Entities ?? Array.Empty<PaintEntity>();

            // A video that would go out without one of its stickers fails instead, as a photo does.
            if (!await PaintEntityRenderer.LoadFilesAsync(_clientService, entities))
            {
                throw new InvalidOperationException("A sticker drawn on the video could not be downloaded.");
            }

            var device = ElementComposition.GetSharedDevice();
            var mirrored = generation.Flip != ImageFlip.None;

            // How many output pixels a unit of the renderer's comes to, for decoding stills at about
            // the size they are drawn.
            var pixelsPerUnit = upright.X / PaintEntityRenderer.Reference * new Vector2(transform.M11, transform.M12).Length();

            var strokes = generation.Strokes;
            var still = new List<PaintEntity>();

            foreach (var entity in entities)
            {
                if (IsAnimatedOverlay(entity))
                {
                    await AddStillOverlayAsync(options, device, strokes, still, output, transform, upright, mirrored, pixelsPerUnit);
                    AddAnimatedOverlay(options, entity, transform, upright, mirrored, animations);

                    strokes = null;
                    still.Clear();
                }
                else
                {
                    still.Add(entity);
                }
            }

            await AddStillOverlayAsync(options, device, strokes, still, output, transform, upright, mirrored, pixelsPerUnit);
        }

        private static bool IsAnimatedOverlay(PaintEntity entity)
        {
            return PaintEntityRenderer.IsSticker(entity)
                && entity.StickerFile?.Local.Path is string path
                && !path.HasExtension(".webp");
        }

        private static async Task AddStillOverlayAsync(VideoTranscoderOptions options, CanvasDevice device, IReadOnlyList<SmoothPathBuilder> strokes, List<PaintEntity> entities, Size output, Matrix3x2 transform, Vector2 upright, bool mirrored, float pixelsPerUnit)
        {
            if (entities.Count == 0 && strokes is not { Count: > 0 })
            {
                return;
            }

            var width = (int)output.Width;
            var height = (int)output.Height;

            using var target = new CanvasRenderTarget(device, width, height, 96);

            // The strokes first and on their own, so that the eraser's transparent Copy blend only
            // ever takes away strokes.
            using (var session = target.CreateDrawingSession())
            {
                session.Clear(Windows.UI.Colors.Transparent);
                session.Transform = transform;

                if (strokes != null)
                {
                    foreach (var builder in strokes)
                    {
                        PencilCanvas.DrawPath(session, builder, upright);
                    }
                }
            }

            if (entities.Count > 0)
            {
                var stickers = await PaintEntityRenderer.LoadStickersAsync(device, entities, pixelsPerUnit);

                try
                {
                    using var session = target.CreateDrawingSession();
                    session.Transform = transform;

                    PaintEntityRenderer.Draw(session, entities, upright, mirrored, stickers);
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

            var pixels = BufferSurface.Create((uint)(width * height * 4));
            target.GetPixelBytes(pixels);

            options.AddLayer(new VideoTranscoderLayer
            {
                Pixels = pixels,
                PixelWidth = width,
                PixelHeight = height
            });
        }

        // A sticker that cannot be decoded is left out, as a photo's is.
        private static void AddAnimatedOverlay(VideoTranscoderOptions options, PaintEntity entity, Matrix3x2 transform, Vector2 upright, bool mirrored, List<IDisposable> animations)
        {
            var file = entity.StickerFile;
            var path = file.Local.Path;

            var bounds = PaintEntityRenderer.GetStickerBounds(entity);
            var matrix = PaintEntityRenderer.GetTransform(entity, bounds, upright, mirrored) * transform;

            // Decoded at the size it is drawn, up to the cap.
            var scale = new Vector2(matrix.M11, matrix.M12).Length();
            var fit = Math.Min(1, OverlayAnimationSide / (Math.Max(bounds.Width, bounds.Height) * scale));

            var width = Math.Max(1, (int)Math.Round(bounds.Width * scale * fit));
            var height = Math.Max(1, (int)Math.Round(bounds.Height * scale * fit));

            var layer = new VideoTranscoderLayer
            {
                White = entity.StickerTinted
            };

            try
            {
                if (path.HasExtension(".tgs", ".json"))
                {
                    var lottie = LottieAnimation.LoadFromFile(path, width, height, false, null);
                    if (lottie == null)
                    {
                        return;
                    }

                    animations.Add(lottie);

                    layer.Lottie = lottie;
                    width = lottie.PixelWidth;
                    height = lottie.PixelHeight;
                }
                else
                {
                    var size = file.Size > 0 ? file.Size : file.Local.DownloadedSize;

                    var video = VideoAnimation.LoadFromFile(new LocalFileSource(path, size), false, false, false);
                    if (video == null)
                    {
                        return;
                    }

                    animations.Add(video);
                    layer.Video = video;
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
                return;
            }

            layer.PixelWidth = width;
            layer.PixelHeight = height;

            // From the decoded pixels to the sticker's place in the renderer's units, then on to the
            // output's pixels.
            layer.Transform = Matrix3x2.CreateScale((float)(bounds.Width / width), (float)(bounds.Height / height))
                * Matrix3x2.CreateTranslation((float)bounds.X, (float)bounds.Y)
                * matrix;

            options.AddLayer(layer);
        }

        private readonly record struct VideoProbe(double Width, double Height, int Bitrate);

        // The frame's upright size, as the transcoder sees it once the source's own rotation is
        // applied, and a bitrate taken from the file's size, which no container reliably states.
        private static async Task<VideoProbe> ProbeVideoAsync(StorageFile file)
        {
            var properties = await file.GetBasicPropertiesAsync();

            using var stream = await file.OpenReadAsync();
            using var animation = await Task.Run(() => VideoAnimation.LoadFromFile(new VideoAnimationStreamSource(stream), false, false, false));

            if (animation == null)
            {
                throw new InvalidOperationException("The video could not be read");
            }

            var quarter = animation.Rotation is 90 or 270;
            var width = quarter ? animation.PixelHeight : animation.PixelWidth;
            var height = quarter ? animation.PixelWidth : animation.PixelHeight;

            var seconds = animation.Duration / 1000.0;
            var bitrate = seconds > 0 ? (int)Math.Min(int.MaxValue, properties.Size * 8 / seconds) : 0;

            return new VideoProbe(width, height, bitrate);
        }

        /// <summary>
        /// The size a video comes out at: a fixed one when asked for, else the crop's, fitted to
        /// the longest side allowed. Sides are multiples of four, as encoders like them.
        /// </summary>
        public static Size GetVideoOutputSize(VideoGeneration generation, double width, double height)
        {
            if (generation.OutputSize.Width > 0 && generation.OutputSize.Height > 0)
            {
                return new Size(RoundDown(generation.OutputSize.Width), RoundDown(generation.OutputSize.Height));
            }

            var crop = GetVideoCrop(generation, width, height);
            var scale = Math.Min(1, (generation.MaxSide > 0 ? generation.MaxSide : VideoMaxSide) / Math.Max(crop.Width, crop.Height));

            return new Size(RoundDown(crop.Width * scale), RoundDown(crop.Height * scale));
        }

        private static double RoundDown(double value)
        {
            return Math.Max(4, Math.Floor(value / 4) * 4);
        }

        // In pixels of the frame once turned and flipped.
        private static Rect GetVideoCrop(VideoGeneration generation, double width, double height)
        {
            var quarter = generation.Rotation is ImageRotation.Clockwise90Degrees or ImageRotation.Clockwise270Degrees;
            var orientedWidth = quarter ? height : width;
            var orientedHeight = quarter ? width : height;

            if (generation.HasRectangle)
            {
                var rectangle = generation.Rectangle;
                return new Rect(rectangle.X * orientedWidth, rectangle.Y * orientedHeight, rectangle.Width * orientedWidth, rectangle.Height * orientedHeight);
            }
            else if (generation.CropRectangle.Width > 0 && generation.CropRectangle.Height > 0)
            {
                return generation.CropRectangle;
            }

            return new Rect(0, 0, orientedWidth, orientedHeight);
        }

        // From the upright frame to the output, as a photo is drawn: turned and flipped, cropped
        // about the crop's centre, turned by the free angle, and scaled to fill the output.
        private static Matrix3x2 GetVideoTransform(VideoGeneration generation, float width, float height, Size output)
        {
            var crop = GetVideoCrop(generation, width, height);
            var center = new Vector2((float)(crop.X + crop.Width / 2), (float)(crop.Y + crop.Height / 2));

            return ImageHelper.GetOrientationMatrix(generation.Rotation, generation.Flip, width, height)
                * Matrix3x2.CreateTranslation(-center)
                * Matrix3x2.CreateRotation((float)(generation.Angle * Math.PI / 180))
                * Matrix3x2.CreateScale((float)(output.Width / crop.Width), (float)(output.Height / crop.Height))
                * Matrix3x2.CreateTranslation((float)output.Width / 2, (float)output.Height / 2);
        }

        // As the official apps pick it: the source's bitrate for the pixels kept, within a range
        // set by the output's shorter side.
        private static int MakeVideoBitrate(double originalHeight, double originalWidth, int originalBitrate, int height, int width)
        {
            float compressFactor;
            float minCompressFactor;
            int maxBitrate;

            if (Math.Min(height, width) >= 1080)
            {
                maxBitrate = 6800000;
                compressFactor = 1;
                minCompressFactor = 1;
            }
            else if (Math.Min(height, width) >= 720)
            {
                maxBitrate = 2600000;
                compressFactor = 1;
                minCompressFactor = 1;
            }
            else if (Math.Min(height, width) >= 480)
            {
                maxBitrate = 1000000;
                compressFactor = 0.75f;
                minCompressFactor = 0.9f;
            }
            else
            {
                maxBitrate = 750000;
                compressFactor = 0.6f;
                minCompressFactor = 0.7f;
            }

            var remeasuredBitrate = (int)(originalBitrate / Math.Min(originalHeight / height, originalWidth / width));
            remeasuredBitrate = (int)(remeasuredBitrate * compressFactor);

            var minBitrate = (int)(minCompressFactor * 2000f * 1000f * 1.13f / (1280f * 720f / (width * height)));

            if (originalBitrate < minBitrate)
            {
                return remeasuredBitrate > 0 ? remeasuredBitrate : minBitrate;
            }

            if (remeasuredBitrate > maxBitrate)
            {
                return maxBitrate;
            }

            return Math.Max(remeasuredBitrate, minBitrate);
        }

        private async Task ThumbnailTranscodeAsync(UpdateFileGenerationStart update, string[] args)
        {
            try
            {
                var file = await GenerationSources.GetFileAsync(args[0]);
                var temp = await StorageFile.GetFileFromPathAsync(update.DestinationPath);

                var generation = JsonSerializer.Deserialize(args[2], GenerationJsonContext.Default.VideoGeneration);

                if (generation.HasRectangle)
                {
                    var image = new ImageGeneration
                    {
                        Rectangle = generation.Rectangle,
                        Rotation = generation.Rotation,
                        Flip = generation.Flip,
                        Angle = generation.Angle
                    };

                    await ImageHelper.CropRotatedAsync(file, temp, image, 320);
                }
                else if (args.Length > 3)
                {
                    var rectangle = generation.CropRectangle;

                    await ImageHelper.CropAsync(file, temp, rectangle, 320, 0, rotation: generation.Rotation, flip: generation.Flip, bestQuality: false);

                    //var drawing = generation.Strokes;
                    //if (drawing != null && drawing.Count > 0)
                    //{
                    //    await ImageHelper.DrawStrokesAsync(temp, drawing, rectangle, generation.Rotation, generation.Flip);
                    //}
                }
                else
                {
                    await ImageHelper.ScaleAsync(BitmapEncoder.JpegEncoderId, file, temp, 320, true, generation.TrimStartTime);
                }

                _clientService.Send(new FinishFileGeneration(update.GenerationId, null));
            }
            catch (Exception ex)
            {
                _clientService.Send(new FinishFileGeneration(update.GenerationId, new Error(500, "FILE_GENERATE_LOCATION_INVALID " + ex.ToString())));
            }

            //StorageApplicationPermissions.FutureAccessList.Remove(args[0]);
        }

        private async Task ThumbnailDocumentAsync(UpdateFileGenerationStart update, string[] args)
        {
            try
            {
                var file = await GenerationSources.GetFileAsync(args[0]);
                var temp = await StorageFile.GetFileFromPathAsync(update.DestinationPath);

                var mode = file.HasExtension(".mp3", ".wav", ".m4a", ".ogg", ".oga", ".opus", ".flac")
                    ? ThumbnailMode.MusicView
                    : ThumbnailMode.DocumentsView;

                using (var thumbnail = await file.GetThumbnailAsync(mode, 90))
                {
                    if (thumbnail != null && thumbnail.Type == ThumbnailType.Image)
                    {
                        using (var reader = new DataReader(thumbnail))
                        {
                            await reader.LoadAsync((uint)thumbnail.Size);
                            var buffer = new byte[(int)thumbnail.Size];
                            reader.ReadBytes(buffer);
                            await FileIO.WriteBytesAsync(temp, buffer);
                        }

                        _clientService.Send(new FinishFileGeneration(update.GenerationId, null));
                    }
                    else
                    {
                        _clientService.Send(new FinishFileGeneration(update.GenerationId, new Error(500, "FILE_GENERATE_LOCATION_INVALID No thumbnail found")));
                    }
                }
            }
            catch (Exception ex)
            {
                _clientService.Send(new FinishFileGeneration(update.GenerationId, new Error(500, "FILE_GENERATE_LOCATION_INVALID " + ex.ToString())));
            }

            //StorageApplicationPermissions.FutureAccessList.Remove(args[0]);
        }

        private async Task AlbumCoverAsync(UpdateFileGenerationStart update, string[] args)
        {
            try
            {
                var file = await GenerationSources.GetFileAsync(args[0]);
                var temp = await StorageFile.GetFileFromPathAsync(update.DestinationPath);

                using var stream = await file.OpenReadAsync();
                using var animation = await Task.Run(() => VideoAnimation.LoadFromFile(new VideoAnimationStreamSource(stream), true, false, true));

                if (animation.HasAlbumCover)
                {
                    using (var thumbnail = animation.GetAlbumCover())
                    {
                        await ImageHelper.ScaleAsync(BitmapEncoder.JpegEncoderId, thumbnail, temp, 320);
                    }

                    _clientService.Send(new FinishFileGeneration(update.GenerationId, null));
                }
                else
                {
                    _clientService.Send(new FinishFileGeneration(update.GenerationId, new Error(500, "FILE_GENERATE_LOCATION_INVALID No thumbnail found")));
                }
            }
            catch (Exception ex)
            {
                _clientService.Send(new FinishFileGeneration(update.GenerationId, new Error(500, "FILE_GENERATE_LOCATION_INVALID " + ex.ToString())));
            }

            //StorageApplicationPermissions.FutureAccessList.Remove(args[0]);
        }
    }

    [JsonSourceGenerationOptions(IgnoreReadOnlyProperties = true, NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals, Converters = new[] { typeof(Vector2Converter) })]
    [JsonSerializable(typeof(ImageGeneration))]
    [JsonSerializable(typeof(VideoGeneration))]
    public partial class GenerationJsonContext : JsonSerializerContext
    {
    }

    public class Vector2Converter : JsonConverter<Vector2>
    {
        public override Vector2 Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            float x = 0, y = 0;

            if (reader.TokenType != JsonTokenType.StartObject)
                throw new JsonException();

            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                    return new Vector2(x, y);

                if (reader.TokenType == JsonTokenType.PropertyName)
                {
                    string propName = reader.GetString()!;
                    reader.Read();

                    switch (propName)
                    {
                        case "X": x = reader.GetSingle(); break;
                        case "Y": y = reader.GetSingle(); break;
                        default: reader.Skip(); break;
                    }
                }
            }

            throw new JsonException();
        }

        public override void Write(Utf8JsonWriter writer, Vector2 value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            writer.WriteNumber("X", value.X);
            writer.WriteNumber("Y", value.Y);
            writer.WriteEndObject();
        }
    }
}
