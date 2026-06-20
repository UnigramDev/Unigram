//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Text.Json;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Entities;
using Telegram.Native;
using Telegram.Td.Api;

namespace Telegram.Services.Factories
{
    public static class MessageFactory
    {
        public static async Task<Object> CreatePhotoAsync(StoragePhoto photo, FormattedText caption, bool highQuality, bool captionAboveMedia, bool spoiler, MessageSelfDestructType ttl, long starCount)
        {
            var conversionType = ConversionType.Compress;
            var file = photo.File;

            var generation = photo.IsEdited ? photo.EditState : null;

            var size = await ImageHelper.GetScaleAsync(file, allowMultipleFrames: ttl != null || starCount > 0, requestedMinSide: highQuality ? 2560 : 1280, generation: generation);
            if (size.Width == 0 || size.Height == 0)
            {
                // This may happen if the image is a GIF with multiple frames.
                conversionType = ConversionType.Copy;
                generation = null;
            }
            else if (highQuality)
            {
                conversionType = ConversionType.HighQuality;
            }

            var serialized = generation != null ? JsonSerializer.Serialize(generation, GenerationJsonContext.Default.ImageGeneration) : null;
            var generated = await file.ToGeneratedAsync(conversionType, serialized);
            var thumbnail = default(InputThumbnail);

            if (starCount > 0)
            {
                return new InputPaidMedia(new InputPaidMediaTypePhoto(), generated, thumbnail, null, size.Width, size.Height);
            }
            else if (conversionType == ConversionType.Copy)
            {
                return new InputMessageDocument(new InputDocument(generated, thumbnail, false), caption);
            }

            return new InputMessagePhoto(new InputPhoto(generated, thumbnail, null, null, size.Width, size.Height), caption, captionAboveMedia, ttl, spoiler);
        }

        public static async Task<Object> CreateVideoAsync(StorageVideo video, FormattedText caption, bool animated, bool captionAboveMedia, bool spoiler, MessageSelfDestructType ttl, long starCount)
        {
            var duration = video.TotalSeconds;
            var videoWidth = video.Width;
            var videoHeight = video.Height;
            var generation = video.GetGeneration();

            generation ??= new VideoGeneration
            {
                Mute = animated
            };

            if (generation.TrimStartTime is TimeSpan trimStart && generation.TrimStopTime is TimeSpan trimStop)
            {
                duration = (int)(trimStop.TotalSeconds - trimStart.TotalSeconds);
            }

            if (generation.Transform && !generation.CropRectangle.IsEmpty)
            {
                videoWidth = (int)generation.CropRectangle.Width;
                videoHeight = (int)generation.CropRectangle.Height;
            }

            var serialized = JsonSerializer.Serialize(generation, GenerationJsonContext.Default.VideoGeneration);
            var generated = await video.File.ToGeneratedAsync(ConversionType.Transcode, serialized);
            var thumbnail = await video.ToVideoThumbnailAsync(generation, ConversionType.TranscodeThumbnail, serialized);

            if (starCount > 0)
            {
                return new InputPaidMedia(new InputPaidMediaTypeVideo(null, 0, duration, true), generated, thumbnail, null, videoWidth, videoHeight);
            }
            else if (animated && ttl == null)
            {
                return new InputMessageAnimation(new InputAnimation(generated, thumbnail, null, duration, videoWidth, videoHeight), caption, captionAboveMedia, spoiler);
            }

            return new InputMessageVideo(new InputVideo(generated, thumbnail, null, 0, null, duration, videoWidth, videoHeight, true), caption, captionAboveMedia, ttl, spoiler);
        }

        public static async Task<InputMessageContent> CreateVideoNoteAsync(StorageVideo video, VideoGeneration generation, MessageSelfDestructType selfDestructType)
        {
            var duration = video.TotalSeconds;
            var videoWidth = video.Width;
            var videoHeight = video.Height;

            var serialized = JsonSerializer.Serialize(generation, GenerationJsonContext.Default.VideoGeneration);
            var generated = await video.File.ToGeneratedAsync(ConversionType.Transcode, serialized);
            var thumbnail = await video.ToVideoThumbnailAsync(generation, ConversionType.TranscodeThumbnail, serialized);

            // TODO: 172 selfDestructType
            return new InputMessageVideoNote(generated, thumbnail, duration, (int)generation.Width, selfDestructType);
        }

        public static async Task<Object> CreateDocumentAsync(StorageMedia media, FormattedText caption, bool forceDocument)
        {
            var file = media.File;
            var generated = await file.ToGeneratedAsync(media.IsScreenshot ? ConversionType.Screenshot : ConversionType.Copy);

            if (!forceDocument && media is StorageAudio audio)
            {
                var duration = audio.TotalSeconds;

                var title = audio.Title;
                var performer = audio.Performer;

                var albumCover = new InputThumbnail(await file.ToGeneratedAsync(ConversionType.AlbumCover), 0, 0);

                return new InputMessageAudio(new InputAudio(generated, albumCover, duration, title, performer), caption);
            }

            var thumbnail = new InputThumbnail(await file.ToGeneratedAsync(ConversionType.DocumentThumbnail), 0, 0);

            if (!forceDocument && file.FileType.Equals(".webp", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    if (PlaceholderImageHelper.IsWebP(file.Path, out int width, out int height))
                    {
                        if ((width == 512 && height <= width) || (height == 512 && width <= height))
                        {
                            return new InputMessageSticker(generated, null, width, height, string.Empty);
                        }
                    }
                }
                catch
                {
                    // Not really a sticker, go on sending as a file
                }
            }
            else if (!forceDocument && file.FileType.Equals(".tgs", StringComparison.OrdinalIgnoreCase))
            {
                // TODO
            }

            return new InputMessageDocument(new InputDocument(generated, thumbnail, true), caption);
        }
    }

    public partial class InputMessageFactory
    {
        public InputFile InputFile { get; set; }
        public Func<InputFile, FormattedText, InputMessageContent> Delegate { get; set; }
        public Func<InputFile, InputPaidMedia> PaidDelegate { get; set; }
    }
}
