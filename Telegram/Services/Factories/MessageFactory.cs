//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Newtonsoft.Json;
using System;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Entities;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Telegram.ViewModels.Delegates;
using Windows.Media.Effects;
using Windows.Media.MediaProperties;
using Windows.Storage;
using Windows.Storage.FileProperties;
using static Telegram.Services.GenerationService;

namespace Telegram.Services.Factories
{
    public interface IMessageFactory
    {
        MessageViewModel Create(IMessageDelegate delegato, Chat chat, Message message, bool processText);
    }

    public class MessageFactory : IMessageFactory
    {
        private readonly IClientService _clientService;
        private readonly IPlaybackService _playbackService;

        public MessageFactory(IClientService clientService, IPlaybackService playbackService)
        {
            _clientService = clientService;
            _playbackService = playbackService;
        }

        public MessageViewModel Create(IMessageDelegate delegato, Chat chat, Message message, bool processText)
        {
            if (message == null)
            {
                return null;
            }

            return new MessageViewModel(_clientService, _playbackService, delegato, chat, message, processText);
        }



        public static async Task<InputMessageFactory> CreatePhotoAsync(StoragePhoto photo, bool spoiler = false, MessageSelfDestructType ttl = null, BitmapEditState editState = null)
        {
            var conversionType = ConversionType.Compress;
            var file = photo.File;

            var size = await ImageHelper.GetScaleAsync(file, editState: editState);
            if (size.Width == 0 || size.Height == 0)
            {
                // This may happen if the image is a GIF with multiple frames.
                conversionType = ConversionType.Copy;
                editState = null;
            }

            var generated = await file.ToGeneratedAsync(conversionType, editState != null ? JsonConvert.SerializeObject(editState) : null);
            var thumbnail = default(InputThumbnail);

            if (conversionType == ConversionType.Copy)
            {
                return new InputMessageFactory
                {
                    InputFile = generated,
                    Delegate = (inputFile, caption) => new InputMessageDocument(inputFile, thumbnail, false, caption)
                };
            }

            return new InputMessageFactory
            {
                InputFile = generated,
                Delegate = (inputFile, caption) => new InputMessagePhoto(generated, thumbnail, Array.Empty<int>(), size.Width, size.Height, caption, photo.ShowCaptionAboveMedia, ttl, spoiler),
                PaidDelegate = (inputFile) => new InputPaidMedia(new InputPaidMediaTypePhoto(), generated, thumbnail, Array.Empty<int>(), size.Width, size.Height)
            };
        }

        public static async Task<InputMessageFactory> CreateVideoAsync(StorageVideo video, bool animated, bool spoiler = false, MessageSelfDestructType ttl = null, VideoTransformEffectDefinition transform = null)
        {
            var duration = video.TotalSeconds;
            var videoWidth = video.Width;
            var videoHeight = video.Height;

            var conversion = new VideoConversion
            {
                Mute = animated
            };

            //var profile = await video.GetEncodingAsync();
            //if (profile != null)
            //{
            //    conversion.Transcode = true;
            //    conversion.Mute = animated;
            //    conversion.Width = profile.Video.Width;
            //    conversion.Height = profile.Video.Height;
            //    conversion.Bitrate = profile.Video.Bitrate;
            //}

            if (transform != null)
            {
                //conversion.Transcode = true;
                conversion.Transform = true;
                conversion.Rotation = transform.Rotation;
                conversion.OutputSize = transform.OutputSize;
                conversion.Mirror = transform.Mirror;
                conversion.CropRectangle = transform.CropRectangle;
            }

            var generated = await video.File.ToGeneratedAsync(ConversionType.Transcode, JsonConvert.SerializeObject(conversion));
            var thumbnail = await video.File.ToVideoThumbnailAsync(conversion, ConversionType.TranscodeThumbnail, JsonConvert.SerializeObject(conversion));

            if (animated && ttl == null)
            {
                return new InputMessageFactory
                {
                    InputFile = generated,
                    Delegate = (inputFile, caption) => new InputMessageAnimation(inputFile, thumbnail, Array.Empty<int>(), duration, videoWidth, videoHeight, caption, video.ShowCaptionAboveMedia, spoiler)
                };
            }

            return new InputMessageFactory
            {
                InputFile = generated,
                Delegate = (inputFile, caption) => new InputMessageVideo(inputFile, thumbnail, Array.Empty<int>(), duration, videoWidth, videoHeight, true, caption, video.ShowCaptionAboveMedia, ttl, spoiler),
                PaidDelegate = (inputFile) => new InputPaidMedia(new InputPaidMediaTypeVideo(duration, true), inputFile, thumbnail, Array.Empty<int>(), videoWidth, videoHeight)
            };
        }

        public static async Task<InputMessageFactory> CreateVideoNoteAsync(StorageFile file, MediaEncodingProfile profile = null, VideoTransformEffectDefinition transform = null)
        {
            var basicProps = await file.GetBasicPropertiesAsync();
            var videoProps = await file.Properties.GetVideoPropertiesAsync();

            //var thumbnail = await ImageHelper.GetVideoThumbnailAsync(file, videoProps, transform);

            var duration = (int)videoProps.Duration.TotalSeconds;
            var videoWidth = (int)videoProps.GetWidth();
            var videoHeight = (int)videoProps.GetHeight();

            if (profile != null)
            {
                videoWidth = videoProps.Orientation is VideoOrientation.Rotate180 or VideoOrientation.Normal ? (int)profile.Video.Width : (int)profile.Video.Height;
                videoHeight = videoProps.Orientation is VideoOrientation.Rotate180 or VideoOrientation.Normal ? (int)profile.Video.Height : (int)profile.Video.Width;
            }

            var conversion = new VideoConversion();
            if (profile != null)
            {
                conversion.Transcode = true;
                conversion.Width = profile.Video.Width;
                conversion.Height = profile.Video.Height;
                conversion.VideoBitrate = profile.Video.Bitrate;

                if (profile.Audio != null)
                {
                    conversion.AudioBitrate = profile.Audio.Bitrate;
                }

                if (transform != null)
                {
                    conversion.Transform = true;
                    conversion.Rotation = transform.Rotation;
                    conversion.OutputSize = transform.OutputSize;
                    conversion.Mirror = transform.Mirror;
                    conversion.CropRectangle = transform.CropRectangle;
                }
            }

            var generated = await file.ToGeneratedAsync(ConversionType.Transcode, JsonConvert.SerializeObject(conversion));
            var thumbnail = await file.ToVideoThumbnailAsync(conversion, ConversionType.TranscodeThumbnail, JsonConvert.SerializeObject(conversion));

            // TODO: 172 selfDestructType
            return new InputMessageFactory
            {
                InputFile = generated,
                Delegate = (inputFile, caption) => new InputMessageVideoNote(inputFile, thumbnail, duration, Math.Min(videoWidth, videoHeight), null)
            };
        }

        public static async Task<InputMessageFactory> CreateDocumentAsync(StorageMedia media, bool asFile, bool asScreenshot)
        {
            var file = media.File;

            var generated = await file.ToGeneratedAsync(asScreenshot ? ConversionType.Screenshot : ConversionType.Copy);
            var thumbnail = new InputThumbnail(await file.ToGeneratedAsync(ConversionType.DocumentThumbnail), 0, 0);

            if (!asFile && file.FileType.Equals(".webp", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    //var buffer = await FileIO.ReadBufferAsync(file);
                    //var webp = WebPImage.DecodeFromBuffer(buffer);

                    // This isn't supposed to work.
                    var webp = PlaceholderHelper.GetWebPFrame(file.Path) as Windows.UI.Xaml.Media.Imaging.BitmapImage;

                    var width = webp.PixelWidth;
                    var height = webp.PixelHeight;

                    if ((width == 512 && height <= width) || (height == 512 && width <= height))
                    {
                        return new InputMessageFactory
                        {
                            InputFile = generated,
                            Delegate = (inputFile, caption) => new InputMessageSticker(inputFile, null, width, height, string.Empty)
                        };
                    }
                }
                catch
                {
                    // Not really a sticker, go on sending as a file
                }
            }
            else if (!asFile && file.FileType.Equals(".tgs", StringComparison.OrdinalIgnoreCase))
            {
                // TODO
            }
            else if (!asFile && media is StorageAudio audio)
            {
                var duration = audio.TotalSeconds;

                var title = audio.Title;
                var performer = audio.Performer;

                return new InputMessageFactory
                {
                    InputFile = generated,
                    Delegate = (inputFile, caption) => new InputMessageAudio(inputFile, thumbnail, duration, title, performer, caption)
                };
            }

            return new InputMessageFactory
            {
                InputFile = generated,
                Delegate = (inputFile, caption) => new InputMessageDocument(inputFile, thumbnail, true, caption)
            };
        }
    }

    public class InputMessageFactory
    {
        public InputFile InputFile { get; set; }
        public Func<InputFile, FormattedText, InputMessageContent> Delegate { get; set; }
        public Func<InputFile, InputPaidMedia> PaidDelegate { get; set; }
    }
}
