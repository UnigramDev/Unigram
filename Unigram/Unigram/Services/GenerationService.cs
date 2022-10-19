using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Effects;
using Newtonsoft.Json;
using RLottie;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Numerics;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Controls.Chats;
using Unigram.Entities;
using Unigram.Native.Opus;
using Windows.Foundation;
using Windows.Graphics.DirectX;
using Windows.Graphics.Imaging;
using Windows.Media.Editing;
using Windows.Media.Effects;
using Windows.Media.MediaProperties;
using Windows.Media.Transcoding;
using Windows.Storage;
using Windows.Storage.AccessCache;
using Windows.Storage.FileProperties;
using Windows.Storage.Streams;
using Windows.UI;

namespace Unigram.Services
{
    public interface IGenerationService
    {

    }

    public enum ConversionType
    {
        Copy,
        Compress,
        Opus,
        Transcode,
        TranscodeThumbnail,
        DocumentThumbnail,
        ChatPhoto,
        // TDLib
        Url
    }

    public class GenerationService : IGenerationService
        //, IHandle<UpdateFileGenerationStart>
        //, IHandle<UpdateFileGenerationStop>
    {
        private readonly IClientService _clientService;
        private readonly IEventAggregator _aggregator;

        public GenerationService(IClientService clientService, IEventAggregator aggregator)
        {
            _clientService = clientService;
            _aggregator = aggregator;

            _aggregator.Subscribe<UpdateFileGenerationStart>(this, Handle)
                .Subscribe<UpdateFileGenerationStop>(Handle);
        }

        public async void Handle(UpdateFileGenerationStart update)
        {
            var args = update.Conversion.Split('#');
            if (args.Length < 2)
            {
                _clientService.Send(new FinishFileGeneration(update.GenerationId, new Error(500, "Invalid generation arguments")));
                return;
            }

            if (Enum.TryParse(args[1], true, out ConversionType conversion))
            {
                if (conversion == ConversionType.Copy)
                {
                    await CopyAsync(update, args);
                }
                else if (conversion == ConversionType.Compress)
                {
                    await CompressAsync(update, args);
                }
                else if (conversion == ConversionType.Opus)
                {
                    await TranscodeOpusAsync(update, args);
                }
                else if (conversion == ConversionType.Transcode)
                {
                    await TranscodeAsync(update, args);
                }
                else if (conversion == ConversionType.TranscodeThumbnail)
                {
                    await ThumbnailTranscodeAsync(update, args);
                }
                else if (conversion == ConversionType.DocumentThumbnail)
                {
                    await ThumbnailDocumentAsync(update, args);
                }
                else if (conversion == ConversionType.ChatPhoto)
                {
                    await ChatPhotoAsync(update, args);
                }
                // TDLib
                else if (conversion == ConversionType.Url)
                {
                    await DownloadAsync(update);
                }
            }
            else
            {
                _clientService.Send(new FinishFileGeneration(update.GenerationId, new Error(500, "FILE_GENERATE_LOCATION_INVALID Unknown conversion type")));
            }
        }

        public void Handle(UpdateFileGenerationStop update)
        {
            //throw new NotImplementedException();
        }

        private bool IsTemporary(StorageFile source)
        {
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
                var temp = await _clientService.GetFileAsync(update.DestinationPath);
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
                var file = await StorageApplicationPermissions.FutureAccessList.GetFileAsync(args[0]);
                var temp = await _clientService.GetFileAsync(update.DestinationPath);

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
        }

        private async Task CompressAsync(UpdateFileGenerationStart update, string[] args)
        {
            try
            {
                var file = await StorageApplicationPermissions.FutureAccessList.GetFileAsync(args[0]);
                var temp = await _clientService.GetFileAsync(update.DestinationPath);

                if (args.Length > 3)
                {
                    var editState = JsonConvert.DeserializeObject<BitmapEditState>(args[2]);
                    var rectangle = editState.Rectangle;

                    await ImageHelper.CropAsync(file, temp, rectangle, rotation: editState.Rotation, flip: editState.Flip);

                    var drawing = editState.Strokes;
                    if (drawing != null && drawing.Count > 0)
                    {
                        await ImageHelper.DrawStrokesAsync(temp, drawing, rectangle, editState.Rotation, editState.Flip);
                    }
                }
                else
                {
                    await ImageHelper.ScaleJpegAsync(file, temp, 1280, 1);
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
        }

        private async Task ThumbnailAsync(UpdateFileGenerationStart update, string[] args)
        {
            try
            {
                var file = await StorageApplicationPermissions.FutureAccessList.GetFileAsync(args[0]);
                var temp = await _clientService.GetFileAsync(update.DestinationPath);

                if (args.Length > 3)
                {
                    var rect = JsonConvert.DeserializeObject<Rect>(args[2]);
                    await ImageHelper.CropAsync(file, temp, rect, 90);
                }
                else
                {
                    await ImageHelper.ScaleJpegAsync(file, temp, 90, 0.77);
                }

                _clientService.Send(new FinishFileGeneration(update.GenerationId, null));
            }
            catch (Exception ex)
            {
                _clientService.Send(new FinishFileGeneration(update.GenerationId, new Error(500, "FILE_GENERATE_LOCATION_INVALID " + ex.ToString())));
            }
        }

        private async Task TranscodeOpusAsync(UpdateFileGenerationStart update, string[] args)
        {
            try
            {
                using (var opus = new OpusOutput(update.DestinationPath))
                {
                    var file = await StorageApplicationPermissions.FutureAccessList.GetFileAsync(args[0]);
                    await Task.Run(() => opus.Transcode(file.Path));
                }

                _clientService.Send(new FinishFileGeneration(update.GenerationId, null));
            }
            catch (Exception ex)
            {
                _clientService.Send(new FinishFileGeneration(update.GenerationId, new Error(500, "FILE_GENERATE_LOCATION_INVALID " + ex.ToString())));
            }
        }

        private async Task TranscodeAsync(UpdateFileGenerationStart update, string[] args)
        {
            try
            {
                var conversion = JsonConvert.DeserializeObject<VideoConversion>(args[2]);
                if (conversion.Mute || conversion.Transcode)
                {
                    var file = await StorageApplicationPermissions.FutureAccessList.GetFileAsync(args[0]);
                    var temp = await _clientService.GetFileAsync(update.DestinationPath);

                    var profile = await MediaEncodingProfile.CreateFromFileAsync(file);
                    if (profile.Audio == null && conversion.Mute && conversion.TrimStartTime == null && conversion.TrimStopTime == null)
                    {
                        await CopyAsync(update, args);
                        return;
                    }
                    //profile.Video.Width = conversion.Width;
                    //profile.Video.Height = conversion.Height;
                    //profile.Video.Bitrate = conversion.Bitrate;

                    if (conversion.Mute)
                    {
                        profile.Audio = null;
                    }

                    var transcoder = new MediaTranscoder();

                    if (conversion.TrimStartTime is TimeSpan trimStart)
                    {
                        transcoder.TrimStartTime = trimStart;
                    }
                    if (conversion.TrimStopTime is TimeSpan trimStop)
                    {
                        transcoder.TrimStopTime = trimStop;
                    }

                    if (conversion.Transform)
                    {
                        var transform = new VideoTransformEffectDefinition();
                        transform.Rotation = conversion.Rotation;
                        transform.OutputSize = conversion.OutputSize;
                        transform.Mirror = conversion.Mirror;
                        transform.CropRectangle = conversion.CropRectangle.IsEmpty() ? Rect.Empty : conversion.CropRectangle;

                        if (conversion.VideoBitrate != 0)
                        {
                            profile.Video.Bitrate = conversion.VideoBitrate;
                        }

                        if (conversion.AudioBitrate != 0 && profile.Audio != null)
                        {
                            profile.Audio.Bitrate = conversion.AudioBitrate;
                        }

                        profile.Video.Width = (uint)conversion.OutputSize.Width;
                        profile.Video.Height = (uint)conversion.OutputSize.Height;

                        transcoder.AddVideoEffect(transform.ActivatableClassId, true, transform.Properties);
                    }

                    var prepare = await transcoder.PrepareFileTranscodeAsync(file, temp, profile);
                    if (prepare.CanTranscode)
                    {
                        var progress = prepare.TranscodeAsync();
                        progress.Progress = (result, delta) =>
                        {
                            _clientService.Send(new SetFileGenerationProgress(update.GenerationId, 100, (int)delta));
                        };
                        progress.Completed = (result, delta) =>
                        {
                            _clientService.Send(new FinishFileGeneration(update.GenerationId, prepare.FailureReason == TranscodeFailureReason.None ? null : new Error(500, prepare.FailureReason.ToString())));
                        };
                    }
                    else
                    {
                        _clientService.Send(new FinishFileGeneration(update.GenerationId, new Error(500, prepare.FailureReason.ToString())));
                    }
                }
                else
                {
                    await CopyAsync(update, args);
                }
            }
            catch (Exception ex)
            {
                _clientService.Send(new FinishFileGeneration(update.GenerationId, new Error(500, "FILE_GENERATE_LOCATION_INVALID " + ex.ToString())));
            }
        }

        private async Task ThumbnailTranscodeAsync(UpdateFileGenerationStart update, string[] args)
        {
            try
            {
                var conversion = JsonConvert.DeserializeObject<VideoConversion>(args[2]);
                //if (conversion.Transcode)
                {
                    var file = await StorageApplicationPermissions.FutureAccessList.GetFileAsync(args[0]);
                    var temp = await _clientService.GetFileAsync(update.DestinationPath);

                    var props = await file.Properties.GetVideoPropertiesAsync();

                    double originalWidth = props.GetWidth();
                    double originalHeight = props.GetHeight();

                    //if (!conversion.CropRectangle.IsEmpty())
                    //{
                    //    file = await ImageHelper.CropAsync(file, temp, conversion.CropRectangle);
                    //    originalWidth = conversion.CropRectangle.Width;
                    //    originalHeight = conversion.CropRectangle.Height;
                    //}

                    using (var fileStream = await ImageHelper.OpenReadAsync(file))
                    using (var outputStream = await temp.OpenAsync(FileAccessMode.ReadWrite))
                    {
                        var decoder = await BitmapDecoder.CreateAsync(fileStream);

                        double ratioX = 90d / originalWidth;
                        double ratioY = 90d / originalHeight;
                        double ratio = Math.Min(ratioX, ratioY);

                        uint width = (uint)(originalWidth * ratio);
                        uint height = (uint)(originalHeight * ratio);

                        var transform = new BitmapTransform();
                        transform.ScaledWidth = width;
                        transform.ScaledHeight = height;
                        transform.InterpolationMode = BitmapInterpolationMode.Linear;
                        transform.Flip = conversion.Mirror == MediaMirroringOptions.Horizontal ? BitmapFlip.Horizontal : BitmapFlip.None;

                        var pixelData = await decoder.GetSoftwareBitmapAsync(decoder.BitmapPixelFormat, decoder.BitmapAlphaMode, transform, ExifOrientationMode.RespectExifOrientation, ColorManagementMode.DoNotColorManage);

                        var propertySet = new BitmapPropertySet();
                        var qualityValue = new BitmapTypedValue(0.77, PropertyType.Single);
                        propertySet.Add("ImageQuality", qualityValue);

                        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.JpegEncoderId, outputStream);
                        encoder.SetSoftwareBitmap(pixelData);
                        await encoder.FlushAsync();

                        _clientService.Send(new FinishFileGeneration(update.GenerationId, null));
                    }
                }
            }
            catch (Exception ex)
            {
                _clientService.Send(new FinishFileGeneration(update.GenerationId, new Error(500, "FILE_GENERATE_LOCATION_INVALID " + ex.ToString())));
            }
        }

        private async Task ThumbnailDocumentAsync(UpdateFileGenerationStart update, string[] args)
        {
            try
            {
                var file = await StorageApplicationPermissions.FutureAccessList.GetFileAsync(args[0]);
                var temp = await _clientService.GetFileAsync(update.DestinationPath);

                var mode = ThumbnailMode.DocumentsView;
                if (file.ContentType.StartsWith("audio/"))
                {
                    mode = ThumbnailMode.MusicView;
                }

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
        }

        private async Task ChatPhotoAsync(UpdateFileGenerationStart update, string[] args)
        {
            try
            {
                var conversion = JsonConvert.DeserializeObject<ChatPhotoConversion>(args[2]);

                var sticker = await _clientService.SendAsync(new GetFile(conversion.StickerFileId)) as Telegram.Td.Api.File;
                if (sticker == null || !sticker.Local.IsDownloadingCompleted)
                {
                    _clientService.Send(new FinishFileGeneration(update.GenerationId, new Error(500, "FILE_GENERATE_LOCATION_INVALID No sticker found")));
                    return;
                }

                Background background = null;

                var backgroundLink = await _clientService.SendAsync(new GetInternalLinkType(conversion.BackgroundUrl ?? string.Empty)) as InternalLinkTypeBackground;
                if (backgroundLink != null)
                {
                    background = await _clientService.SendAsync(new SearchBackground(backgroundLink.BackgroundName)) as Background;
                }
                else
                {
                    var freeform = new[] { 0xDBDDBB, 0x6BA587, 0xD5D88D, 0x88B884 };
                    background = new Background(0, true, false, string.Empty,
                        new Document(string.Empty, "application/x-tgwallpattern", null, null, TdExtensions.GetLocalFile("Assets\\Background.tgv", "Background")),
                        new BackgroundTypePattern(new BackgroundFillFreeformGradient(freeform), 50, false, false));
                }

                if (background == null || (background.Document != null && !background.Document.DocumentValue.Local.IsDownloadingCompleted))
                {
                    _clientService.Send(new FinishFileGeneration(update.GenerationId, new Error(500, "FILE_GENERATE_LOCATION_INVALID No background found")));
                    return;
                }

                var device = CanvasDevice.GetSharedDevice();
                var bitmaps = new List<CanvasBitmap>();

                var sfondo = new CanvasRenderTarget(device, 640, 640, 96, DirectXPixelFormat.B8G8R8A8UIntNormalized, CanvasAlphaMode.Premultiplied);

                using (var session = sfondo.CreateDrawingSession())
                {
                    if (background.Type is BackgroundTypePattern pattern)
                    {
                        if (pattern.Fill is BackgroundFillFreeformGradient freeform)
                        {
                            var colors = freeform.GetColors();
                            var positions = new Vector2[]
                            {
                                new Vector2(0.80f, 0.10f),
                                new Vector2(0.35f, 0.25f),
                                new Vector2(0.20f, 0.90f),
                                new Vector2(0.65f, 0.75f),
                            };

                            using (var gradient = CanvasBitmap.CreateFromBytes(device, ChatBackgroundFreeform.GenerateGradientData(50, 50, colors, positions), 50, 50, DirectXPixelFormat.B8G8R8A8UIntNormalized))
                            using (var cache = await PlaceholderHelper.GetPatternBitmapAsync(device, null, background.Document.DocumentValue))
                            {
                                using (var scale = new ScaleEffect { Source = gradient, BorderMode = EffectBorderMode.Hard, Scale = new Vector2(640f / 50f, 640f / 50f) })
                                using (var colorize = new TintEffect { Source = cache, Color = Color.FromArgb(0x76, 00, 00, 00) })
                                using (var tile = new BorderEffect { Source = colorize, ExtendX = CanvasEdgeBehavior.Wrap, ExtendY = CanvasEdgeBehavior.Wrap })
                                using (var effect = new BlendEffect { Foreground = tile, Background = scale, Mode = BlendEffectMode.Overlay })
                                {
                                    session.DrawImage(effect, new Rect(0, 0, 640, 640), new Rect(0, 0, 640, 640));
                                }
                            }
                        }
                    }
                }

                bitmaps.Add(sfondo);

                var width = (int)(512d * conversion.Scale);
                var height = (int)(512d * conversion.Scale);

                var animation = await Task.Run(() => LottieAnimation.LoadFromFile(sticker.Local.Path, new Windows.Graphics.SizeInt32 { Width = width, Height = height }, false, null));
                if (animation == null)
                {
                    _clientService.Send(new FinishFileGeneration(update.GenerationId, new Error(500, "FILE_GENERATE_LOCATION_INVALID Can't load Lottie animation")));
                    return;
                }

                var composition = new MediaComposition();
                var layer = new MediaOverlayLayer();

                var buffer = ArrayPool<byte>.Shared.Rent(width * height * 4);

                var framesPerUpdate = animation.FrameRate < 60 ? 1 : 2;
                var duration = TimeSpan.Zero;

                for (int i = 0; i < animation.TotalFrame; i += framesPerUpdate)
                {
                    var bitmap = CanvasBitmap.CreateFromBytes(device, buffer, width, height, DirectXPixelFormat.B8G8R8A8UIntNormalized);
                    animation.RenderSync(bitmap, i);

                    var clip = MediaClip.CreateFromSurface(bitmap, TimeSpan.FromMilliseconds(1000d / 30d));
                    var overlay = new MediaOverlay(clip, new Rect(320 - (width / 2d), 320 - (height / 2d), width, height), 1);

                    overlay.Delay = duration;

                    layer.Overlays.Add(overlay);
                    duration += clip.OriginalDuration;

                    bitmaps.Add(bitmap);
                }

                composition.OverlayLayers.Add(layer);
                composition.Clips.Add(MediaClip.CreateFromSurface(sfondo, duration));

                var temp = await _clientService.GetFileAsync(update.DestinationPath);

                var profile = MediaEncodingProfile.CreateMp4(VideoEncodingQuality.Auto);
                profile.Audio = null;
                profile.Video.Bitrate = 1800000;
                profile.Video.Width = 640;
                profile.Video.Height = 640;
                profile.Video.FrameRate.Numerator = 30;
                profile.Video.FrameRate.Denominator = 1;

                var progress = composition.RenderToFileAsync(temp, MediaTrimmingPreference.Precise, profile);
                progress.Progress = (result, delta) =>
                {
                    _clientService.Send(new SetFileGenerationProgress(update.GenerationId, 100, (int)delta));
                };

                var result = await progress;
                if (result == TranscodeFailureReason.None)
                {
                    _clientService.Send(new FinishFileGeneration(update.GenerationId, null));
                }
                else
                {
                    _clientService.Send(new FinishFileGeneration(update.GenerationId, new Error(500, result.ToString())));
                }

                ArrayPool<byte>.Shared.Return(buffer);

                foreach (var bitmap in bitmaps)
                {
                    bitmap.Dispose();
                }
            }
            catch (Exception ex)
            {
                _clientService.Send(new FinishFileGeneration(update.GenerationId, new Error(500, "FILE_GENERATE_LOCATION_INVALID " + ex.ToString())));
            }
        }

        public class VideoConversion
        {
            public bool Transcode { get; set; }
            public bool Mute { get; set; }
            public uint Width { get; set; }
            public uint Height { get; set; }
            public uint VideoBitrate { get; set; }
            public uint AudioBitrate { get; set; }

            public TimeSpan? TrimStartTime { get; set; }
            public TimeSpan? TrimStopTime { get; set; }

            public bool Transform { get; set; }
            public MediaRotation Rotation { get; set; }
            public Size OutputSize { get; set; }
            public MediaMirroringOptions Mirror { get; set; }
            public Rect CropRectangle { get; set; }
        }

        public class ChatPhotoConversion
        {
            public int StickerFileId { get; set; }

            public string BackgroundUrl { get; set; }

            public double Scale { get; set; }
        }
    }
}
