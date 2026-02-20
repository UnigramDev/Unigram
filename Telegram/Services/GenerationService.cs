//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;
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

        private async Task CompressAsync(UpdateFileGenerationStart update, bool highQuality, string[] args)
        {
            try
            {
                var file = await StorageApplicationPermissions.FutureAccessList.GetFileAsync(args[0]);
                var temp = await StorageFile.GetFileFromPathAsync(update.DestinationPath);

                var maxSize = highQuality
                    ? Constants.ImageHighQuality
                    : Constants.ImageStandardQuality;

                if (args.Length > 3)
                {
                    var generation = JsonSerializer.Deserialize(args[2], GenerationJsonContext.Default.ImageGeneration);
                    var rectangle = generation.Rectangle;

                    await ImageHelper.CropAsync(file, temp, rectangle, maxSize, generation.MinimumSize, rotation: generation.Rotation, flip: generation.Flip, bestQuality: true);

                    var drawing = generation.Strokes;
                    if (drawing != null && drawing.Count > 0)
                    {
                        await ImageHelper.DrawStrokesAsync(temp, drawing, rectangle, generation.Rotation, generation.Flip);
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
                var generation = JsonSerializer.Deserialize(args[2], GenerationJsonContext.Default.VideoGeneration);
                if (generation.Mute || generation.Transcode)
                {
                    var file = await StorageApplicationPermissions.FutureAccessList.GetFileAsync(args[0]);
                    var temp = await StorageFile.GetFileFromPathAsync(update.DestinationPath);

                    var profile = await MediaEncodingProfile.CreateFromFileAsync(file);
                    if (profile.Audio == null && generation.Mute && generation.TrimStartTime == null && generation.TrimStopTime == null)
                    {
                        await CopyAsync(update, args);
                        return;
                    }
                    //profile.Video.Width = conversion.Width;
                    //profile.Video.Height = conversion.Height;
                    //profile.Video.Bitrate = conversion.Bitrate;

                    if (generation.Mute)
                    {
                        profile.Audio = null;
                    }

                    var transcoder = new MediaTranscoder();
                    //var clip = await MediaClip.CreateFromFileAsync(file);
                    //var composition = new MediaComposition();
                    //composition.Clips.Add(clip);

                    if (generation.TrimStartTime is TimeSpan trimStart)
                    {
                        transcoder.TrimStartTime = trimStart;
                        //clip.TrimTimeFromStart = trimStart;
                    }
                    if (generation.TrimStopTime is TimeSpan trimStop)
                    {
                        transcoder.TrimStopTime = trimStop;
                        //clip.TrimTimeFromEnd = trimStop;
                    }

                    if (generation.Transform)
                    {
                        var crop = generation.CropRectangle;
                        var empty = crop == default || (crop.Width == 0 && crop.Height == 0);

                        var transform = new VideoTransformEffectDefinition();
                        transform.Rotation = (MediaRotation)generation.Rotation;
                        transform.Mirror = (MediaMirroringOptions)generation.Flip;
                        transform.OutputSize = generation.OutputSize;
                        transform.CropRectangle = empty ? Rect.Empty : generation.CropRectangle;

                        if (generation.VideoBitrate != 0)
                        {
                            profile.Video.Bitrate = generation.VideoBitrate;
                        }

                        if (generation.AudioBitrate != 0 && profile.Audio != null)
                        {
                            profile.Audio.Bitrate = generation.AudioBitrate;
                        }

                        profile.Video.Width = (uint)generation.OutputSize.Width;
                        profile.Video.Height = (uint)generation.OutputSize.Height;

                        transcoder.AddVideoEffect(transform.ActivatableClassId, true, transform.Properties);
                        //clip.VideoEffectDefinitions.Add(transform);
                    }

                    var prepare = await transcoder.PrepareFileTranscodeAsync(file, temp, profile);
                    if (prepare.CanTranscode)
                    {
                        var progress = prepare.TranscodeAsync();
                        //var progress = composition.RenderToFileAsync(temp, MediaTrimmingPreference.Precise, profile);
                        progress.Progress = (info, delta) =>
                        {
                            _clientService.Send(new SetFileGenerationProgress(update.GenerationId, 100, (int)delta));
                        };
                        progress.Completed = (info, status) =>
                        {
                            //var results = info.GetResults();
                            //if (results != TranscodeFailureReason.None || status != AsyncStatus.Completed)
                            //{
                            //    _clientService.Send(new FinishFileGeneration(update.GenerationId, new Error(500, results.ToString())));
                            //}
                            //else
                            {
                                _clientService.Send(new FinishFileGeneration(update.GenerationId, null));
                            }
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
                var file = await StorageApplicationPermissions.FutureAccessList.GetFileAsync(args[0]);
                var temp = await StorageFile.GetFileFromPathAsync(update.DestinationPath);

                var generation = JsonSerializer.Deserialize(args[2], GenerationJsonContext.Default.VideoGeneration);

                if (args.Length > 3)
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
