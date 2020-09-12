using Newtonsoft.Json;
using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Entities;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Media.Effects;
using Windows.Media.MediaProperties;
using Windows.Media.Transcoding;
using Windows.Storage;
using Windows.Storage.AccessCache;
using Windows.Storage.FileProperties;
using Windows.Storage.Streams;

namespace Unigram.Services
{
    public interface IGenerationService : IHandle<UpdateFileGenerationStart>, IHandle<UpdateFileGenerationStop>
    {

    }

    public enum ConversionType
    {
        Copy,
        Compress,
        Transcode,
        TranscodeThumbnail,
        DocumentThumbnail,
        // TDLib
        Url
    }

    public class GenerationService : IGenerationService
    {
        private readonly IProtoService _protoService;
        private readonly IEventAggregator _aggregator;

        public GenerationService(IProtoService protoService, IEventAggregator aggregator)
        {
            _protoService = protoService;
            _aggregator = aggregator;

            _aggregator.Subscribe(this);
        }

        public async void Handle(UpdateFileGenerationStart update)
        {
            var args = update.Conversion.Split('#');
            if (args.Length < 2)
            {
                _protoService.Send(new FinishFileGeneration(update.GenerationId, new Error(500, "Invalid generation arguments")));
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
                // TDLib
                else if (conversion == ConversionType.Url)
                {
                    await DownloadAsync(update);
                }
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
                var temp = await StorageFile.GetFileFromPathAsync(update.DestinationPath);
                var request = new HttpRequestMessage(HttpMethod.Get, result);
                var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
                var length = int.Parse(response.Content.Headers.GetValues("Content-Length").FirstOrDefault());

                _protoService.Send(new SetFileGenerationProgress(update.GenerationId, length, 0));

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
                            _protoService.Send(new SetFileGenerationProgress(update.GenerationId, length, totalBytesRead));

                            // Write to file.
                            await fs.WriteAsync(buffer);
                        }

                        inputStream.Dispose();
                        fs.Dispose();
                    }
                }

                _protoService.Send(new FinishFileGeneration(update.GenerationId, null));
            }
            catch (Exception ex)
            {
                _protoService.Send(new FinishFileGeneration(update.GenerationId, new Error(500, "FILE_GENERATE_LOCATION_INVALID " + ex.ToString())));
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

                _protoService.Send(new FinishFileGeneration(update.GenerationId, null));
            }
            catch (Exception ex)
            {
                _protoService.Send(new FinishFileGeneration(update.GenerationId, new Error(500, "FILE_GENERATE_LOCATION_INVALID " + ex.ToString())));
            }
        }

        private async Task CompressAsync(UpdateFileGenerationStart update, string[] args)
        {
            try
            {
                var file = await StorageApplicationPermissions.FutureAccessList.GetFileAsync(args[0]);
                var temp = await StorageFile.GetFileFromPathAsync(update.DestinationPath);

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
                    await ImageHelper.ScaleJpegAsync(file, temp, 1280, 0.77);
                }

                _protoService.Send(new FinishFileGeneration(update.GenerationId, null));

                if (IsTemporary(file))
                {
                    await file.DeleteAsync();
                }
            }
            catch (Exception ex)
            {
                _protoService.Send(new FinishFileGeneration(update.GenerationId, new Error(500, "FILE_GENERATE_LOCATION_INVALID " + ex.ToString())));
            }
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
                    await ImageHelper.ScaleJpegAsync(file, temp, 90, 0.77);
                }

                _protoService.Send(new FinishFileGeneration(update.GenerationId, null));
            }
            catch (Exception ex)
            {
                _protoService.Send(new FinishFileGeneration(update.GenerationId, new Error(500, "FILE_GENERATE_LOCATION_INVALID " + ex.ToString())));
            }
        }

        private async Task TranscodeAsync(UpdateFileGenerationStart update, string[] args)
        {
            try
            {
                var conversion = JsonConvert.DeserializeObject<VideoConversion>(args[2]);
                if (conversion.Mute)
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
                        var transform = new VideoTransformEffectDefinition();
                        transform.Rotation = conversion.Rotation;
                        transform.OutputSize = conversion.OutputSize;
                        transform.Mirror = conversion.Mirror;
                        transform.CropRectangle = conversion.CropRectangle.IsEmpty() ? Rect.Empty : conversion.CropRectangle;

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
                            _protoService.Send(new SetFileGenerationProgress(update.GenerationId, 100, (int)delta));
                        };
                        progress.Completed = (result, delta) =>
                        {
                            _protoService.Send(new FinishFileGeneration(update.GenerationId, prepare.FailureReason == TranscodeFailureReason.None ? null : new Error(500, prepare.FailureReason.ToString())));
                        };
                    }
                    else
                    {
                        _protoService.Send(new FinishFileGeneration(update.GenerationId, new Error(500, prepare.FailureReason.ToString())));
                    }
                }
                else
                {
                    await CopyAsync(update, args);
                }
            }
            catch (Exception ex)
            {
                _protoService.Send(new FinishFileGeneration(update.GenerationId, new Error(500, "FILE_GENERATE_LOCATION_INVALID " + ex.ToString())));
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
                    var temp = await StorageFile.GetFileFromPathAsync(update.DestinationPath);

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

                        double ratioX = (double)90 / originalWidth;
                        double ratioY = (double)90 / originalHeight;
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

                        _protoService.Send(new FinishFileGeneration(update.GenerationId, null));
                    }
                }
            }
            catch (Exception ex)
            {
                _protoService.Send(new FinishFileGeneration(update.GenerationId, new Error(500, "FILE_GENERATE_LOCATION_INVALID " + ex.ToString())));
            }
        }

        private async Task ThumbnailDocumentAsync(UpdateFileGenerationStart update, string[] args)
        {
            try
            {
                var file = await StorageApplicationPermissions.FutureAccessList.GetFileAsync(args[0]);
                var temp = await StorageFile.GetFileFromPathAsync(update.DestinationPath);

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

                        _protoService.Send(new FinishFileGeneration(update.GenerationId, null));
                    }
                    else
                    {
                        _protoService.Send(new FinishFileGeneration(update.GenerationId, new Error(500, "FILE_GENERATE_LOCATION_INVALID No thumbnail found")));
                    }
                }
            }
            catch (Exception ex)
            {
                _protoService.Send(new FinishFileGeneration(update.GenerationId, new Error(500, "FILE_GENERATE_LOCATION_INVALID " + ex.ToString())));
            }
        }

        public class VideoConversion
        {
            public bool Transcode { get; set; }
            public bool Mute { get; set; }
            public uint Width { get; set; }
            public uint Height { get; set; }
            public uint Bitrate { get; set; }

            public TimeSpan? TrimStartTime { get; set; }
            public TimeSpan? TrimStopTime { get; set; }

            public bool Transform { get; set; }
            public MediaRotation Rotation { get; set; }
            public Size OutputSize { get; set; }
            public MediaMirroringOptions Mirror { get; set; }
            public Rect CropRectangle { get; set; }
        }
    }
}
