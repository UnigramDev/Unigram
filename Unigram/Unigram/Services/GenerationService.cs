using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Common;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Media.Effects;
using Windows.Media.MediaProperties;
using Windows.Media.Transcoding;
using Windows.Storage;
using Windows.Storage.Streams;

namespace Unigram.Services
{
    public interface IGenerationService
    {

    }

    public class GenerationService : IGenerationService, IHandle<UpdateFileGenerationStart>, IHandle<UpdateFileGenerationStop>
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
            if (update.Conversion.StartsWith("copy"))
            {
                await CopyAsync(update);
            }
            else if (update.Conversion.StartsWith("compress"))
            {
                await CompressAsync(update);
            }
            else if (update.Conversion.StartsWith("transcode"))
            {
                await TranscodeAsync(update);
            }
            else if (update.Conversion.StartsWith("thumbnail_transcode"))
            {
                await ThumbnailTranscodeAsync(update);
            }
            else if (update.Conversion.Equals("#url#"))
            {
                await DownloadAsync(update);
            }
        }

        public void Handle(UpdateFileGenerationStop update)
        {
            //throw new NotImplementedException();
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
                _protoService.Send(new FinishFileGeneration(update.GenerationId, new Error(500, ex.ToString())));
            }
        }

        private async Task CopyAsync(UpdateFileGenerationStart update)
        {
            try
            {
                var file = await StorageFile.GetFileFromPathAsync(update.OriginalPath);
                var temp = await StorageFile.GetFileFromPathAsync(update.DestinationPath);

                await file.CopyAndReplaceAsync(temp);

                _protoService.Send(new FinishFileGeneration(update.GenerationId, null));
            }
            catch (Exception ex)
            {
                _protoService.Send(new FinishFileGeneration(update.GenerationId, new Error(500, ex.ToString())));
            }
        }

        private async Task CompressAsync(UpdateFileGenerationStart update)
        {
            try
            {
                var file = await StorageFile.GetFileFromPathAsync(update.OriginalPath);
                var temp = await StorageFile.GetFileFromPathAsync(update.DestinationPath);

                var args = update.Conversion.Split('#');
                if (args.Length > 2)
                {
                    var rect = JsonConvert.DeserializeObject<Rect>(args[1]);
                    await ImageHelper.CropAsync(file, temp, rect);
                }
                else
                {
                    await ImageHelper.ScaleJpegAsync(file, temp, 1280, 0.77);
                }

                _protoService.Send(new FinishFileGeneration(update.GenerationId, null));
            }
            catch (Exception ex)
            {
                _protoService.Send(new FinishFileGeneration(update.GenerationId, new Error(500, ex.ToString())));
            }
        }

        private async Task ThumbnailAsync(UpdateFileGenerationStart update)
        {
            try
            {
                var file = await StorageFile.GetFileFromPathAsync(update.OriginalPath);
                var temp = await StorageFile.GetFileFromPathAsync(update.DestinationPath);

                var args = update.Conversion.Split('#');
                if (args.Length > 2)
                {
                    var rect = JsonConvert.DeserializeObject<Rect>(args[1]);
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
                _protoService.Send(new FinishFileGeneration(update.GenerationId, new Error(500, ex.ToString())));
            }
        }

        private async Task TranscodeAsync(UpdateFileGenerationStart update)
        {
            try
            {
                var args = update.Conversion.Substring("transcode#".Length);
                args = args.Substring(0, args.LastIndexOf('#'));

                var conversion = JsonConvert.DeserializeObject<VideoConversion>(args);
                if (conversion.Transcode)
                {
                    var file = await StorageFile.GetFileFromPathAsync(update.OriginalPath);
                    var temp = await StorageFile.GetFileFromPathAsync(update.DestinationPath);

                    var transcoder = new MediaTranscoder();

                    var profile = await MediaEncodingProfile.CreateFromFileAsync(file);
                    profile.Video.Width = conversion.Width;
                    profile.Video.Height = conversion.Height;
                    profile.Video.Bitrate = conversion.Bitrate;

                    if (conversion.Mute)
                    {
                        profile.Audio = null;
                    }

                    if (conversion.Transform)
                    {
                        var transform = new VideoTransformEffectDefinition();
                        transform.Rotation = conversion.Rotation;
                        transform.OutputSize = conversion.OutputSize;
                        transform.Mirror = conversion.Mirror;
                        transform.CropRectangle = conversion.CropRectangle.IsEmpty() ? Rect.Empty : conversion.CropRectangle;

                        transcoder.AddVideoEffect(transform.ActivatableClassId, true, transform.Properties);
                    }

                    var prepare = await transcoder.PrepareFileTranscodeAsync(file, temp, profile);
                    if (prepare.CanTranscode)
                    {
                        var progress = prepare.TranscodeAsync();
                        progress.Progress = (result, delta) =>
                        {
                            _protoService.Send(new SetFileGenerationProgress(update.GenerationId, (int)delta, 100));
                        };
                        progress.Completed = (result, delta) =>
                        {
                            _protoService.Send(new FinishFileGeneration(update.GenerationId, prepare.FailureReason == TranscodeFailureReason.None ? null : new Error(406, prepare.FailureReason.ToString())));
                        };
                    }
                    else
                    {
                        _protoService.Send(new FinishFileGeneration(update.GenerationId, new Error(500, prepare.FailureReason.ToString())));
                    }
                }
                else
                {
                    await CopyAsync(update);
                }
            }
            catch (Exception ex)
            {
                _protoService.Send(new FinishFileGeneration(update.GenerationId, new Error(500, ex.ToString())));
            }
        }

        private async Task ThumbnailTranscodeAsync(UpdateFileGenerationStart update)
        {
            try
            {
                var args = update.Conversion.Substring("thumbnail_transcode#".Length);
                args = args.Substring(0, args.LastIndexOf('#'));

                var conversion = JsonConvert.DeserializeObject<VideoConversion>(args);
                if (conversion.Transcode)
                {
                    var file = await StorageFile.GetFileFromPathAsync(update.OriginalPath);
                    var temp = await StorageFile.GetFileFromPathAsync(update.DestinationPath);

                    var props = await file.Properties.GetVideoPropertiesAsync();

                    double originalWidth = props.GetWidth();
                    double originalHeight = props.GetHeight();

                    if (!conversion.CropRectangle.IsEmpty())
                    {
                        file = await ImageHelper.CropAsync(file, temp, conversion.CropRectangle);
                        originalWidth = conversion.CropRectangle.Width;
                        originalHeight = conversion.CropRectangle.Height;
                    }

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
                _protoService.Send(new FinishFileGeneration(update.GenerationId, new Error(500, ex.ToString())));
            }
        }

        public class VideoConversion
        {
            public bool Transcode { get; set; }
            public bool Mute { get; set; }
            public uint Width { get; set; }
            public uint Height { get; set; }
            public uint Bitrate { get; set; }

            public bool Transform { get; set; }
            public MediaRotation Rotation { get; set; }
            public Size OutputSize { get; set; }
            public MediaMirroringOptions Mirror { get; set; }
            public Rect CropRectangle { get; set; }
        }
    }
}
