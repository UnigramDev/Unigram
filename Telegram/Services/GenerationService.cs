//
// Copyright Fela Ameghino 2015-2025
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Newtonsoft.Json;
using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Entities;
using Telegram.Native;
using Telegram.Native.Opus;
using Telegram.Td.Api;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Media.Effects;
using Windows.Media.MediaProperties;
using Windows.Media.Transcoding;
using Windows.Storage;
using Windows.Storage.AccessCache;
using Windows.Storage.FileProperties;
using Windows.Storage.Streams;

namespace Telegram.Services
{
    public interface IGenerationService
    {

    }

    public enum ConversionType
    {
        Copy,
        Compress,
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

        public GenerationService(IClientService clientService, IEventAggregator aggregator)
        {
            _clientService = clientService;
            _aggregator = aggregator;

            _aggregator.Subscribe<UpdateFileGenerationStart>(this, Handle)
                .Subscribe<UpdateFileGenerationStop>(Handle);
        }

        public async void Handle(UpdateFileGenerationStart update)
        {
            var args = update.Conversion.Split('#', StringSplitOptions.RemoveEmptyEntries);
            if (args.Length < 2)
            {
                _clientService.Send(new FinishFileGeneration(update.GenerationId, new Error(500, "Invalid generation arguments")));
                return;
            }

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
                    else if (conversion == ConversionType.Compress)
                    {
                        await CompressAsync(update, args);
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
            //throw new NotImplementedException();
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
                var file = await StorageApplicationPermissions.FutureAccessList.GetFileAsync(args[0]);
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

        private async Task CompressAsync(UpdateFileGenerationStart update, string[] args)
        {
            try
            {
                var file = await StorageApplicationPermissions.FutureAccessList.GetFileAsync(args[0]);
                var temp = await StorageFile.GetFileFromPathAsync(update.DestinationPath);

                var maxSize = 1280;

                if (SettingsService.Current.Diagnostics.SendLargePhotos)
                {
                    maxSize = 2560;
                }

                if (args.Length > 3)
                {
                    var editState = JsonConvert.DeserializeObject<BitmapEditState>(args[2]);
                    var rectangle = editState.Rectangle;

                    await ImageHelper.CropAsync(file, temp, rectangle, maxSize, editState.MinimumSize, rotation: editState.Rotation, flip: editState.Flip);

                    var drawing = editState.Strokes;
                    if (drawing != null && drawing.Count > 0)
                    {
                        await ImageHelper.DrawStrokesAsync(temp, drawing, rectangle, editState.Rotation, editState.Flip);
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
                var file = await StorageApplicationPermissions.FutureAccessList.GetFileAsync(args[0]);
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
                var file = await StorageApplicationPermissions.FutureAccessList.GetFileAsync(args[0]);
                var temp = await StorageFile.GetFileFromPathAsync(update.DestinationPath);

                if (args.Length > 3)
                {
                    var rect = JsonConvert.DeserializeObject<Rect>(args[2]);
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
                var file = await StorageApplicationPermissions.FutureAccessList.GetFileAsync(args[0]);

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

        private async Task TranscodeAsync(UpdateFileGenerationStart update, string[] args)
        {
            try
            {
                var conversion = JsonConvert.DeserializeObject<VideoConversion>(args[2]);
                if (conversion.Mute || conversion.Transcode)
                {
                    var file = await StorageApplicationPermissions.FutureAccessList.GetFileAsync(args[0]);
                    var temp = await StorageFile.GetFileFromPathAsync(update.DestinationPath);

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
                        var crop = conversion.CropRectangle;
                        var empty = crop == default || (crop.Width == 0 && crop.Height == 0);

                        var transform = new VideoTransformEffectDefinition();
                        transform.Rotation = conversion.Rotation;
                        transform.OutputSize = conversion.OutputSize;
                        transform.Mirror = conversion.Mirror;
                        transform.CropRectangle = empty ? Rect.Empty : conversion.CropRectangle;

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

            //StorageApplicationPermissions.FutureAccessList.Remove(args[0]);
        }

        private async Task ThumbnailTranscodeAsync(UpdateFileGenerationStart update, string[] args)
        {
            try
            {
                var conversion = JsonConvert.DeserializeObject<VideoConversion>(args[2]);
                //if (conversion.Transcode)
                {
                    var file = await StorageApplicationPermissions.FutureAccessList.GetFileAsync(args[0]);
                    var temp = await StorageFile.GetFileFromPathAsync(update.DestinationPath);

                    var props = await file.Properties.GetVideoPropertiesAsync();

                    double originalWidth = props.GetWidth();
                    double originalHeight = props.GetHeight();

                    if (conversion.Transform && !conversion.CropRectangle.IsEmpty)
                    {
                        file = await ImageHelper.CropAsync(file, temp, conversion.CropRectangle, trimStart: conversion.TrimStartTime);
                        originalWidth = conversion.CropRectangle.Width;
                        originalHeight = conversion.CropRectangle.Height;
                    }

                    using (var fileStream = await ImageHelper.OpenReadAsync(file))
                    using (var outputStream = await temp.OpenAsync(FileAccessMode.ReadWrite))
                    {
                        var decoder = await BitmapDecoder.CreateAsync(fileStream);

                        double ratioX = 320d / originalWidth;
                        double ratioY = 320d / originalHeight;
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

            //StorageApplicationPermissions.FutureAccessList.Remove(args[0]);
        }

        private async Task ThumbnailDocumentAsync(UpdateFileGenerationStart update, string[] args)
        {
            try
            {
                var file = await StorageApplicationPermissions.FutureAccessList.GetFileAsync(args[0]);
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
                var file = await StorageApplicationPermissions.FutureAccessList.GetFileAsync(args[0]);
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

        public partial class VideoConversion
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

        public partial class ChatPhotoConversion
        {
            public int StickerFileId { get; set; }

            public int StickerFileType { get; set; }

            public string BackgroundUrl { get; set; }

            public float Scale { get; set; } = 1;

            public float OffsetX { get; set; } = 0;

            public float OffsetY { get; set; } = 0;
        }
    }
}
