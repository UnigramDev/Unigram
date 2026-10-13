//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System.Collections.Generic;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Entities;
using Telegram.Services.Factories;
using Telegram.Td.Api;
using Windows.Storage;

namespace Telegram.Controls.RichEditor
{
    /// <summary>
    /// Files from a picker, the clipboard or a drop, as media the editor can hold. Each goes
    /// through <see cref="MessageFactory"/>, which is what the chat composer sends through, so a
    /// photo in a rich message is compressed, converted and thumbnailed exactly like one sent
    /// on its own; TDLib uploads it with the message.
    /// </summary>
    public static class RichMediaFactory
    {
        private const int PreviewSide = 720;

        public static async Task<IList<RichMediaSource>> CreateAsync(IEnumerable<IStorageItem> items, bool asDocuments = false)
        {
            var result = new List<RichMediaSource>();
            var media = await StorageMedia.CreateAsync(items);

            foreach (var item in media)
            {
                if (await CreateAsync(item, asDocuments) is RichMediaSource source)
                {
                    result.Add(source);
                }
            }

            return result;
        }

        public static async Task<RichMediaSource> CreateAsync(StorageMedia media, bool asDocument = false)
        {
            if (media == null)
            {
                return null;
            }

            Object content;

            if (!asDocument && media is StoragePhoto photo)
            {
                content = await MessageFactory.CreatePhotoAsync(photo, null, false, false, false, null, 0);
            }
            else if (!asDocument && media is StorageVideo video)
            {
                content = await MessageFactory.CreateVideoAsync(video, null, false, false, false, null, 0);
            }
            else
            {
                content = await MessageFactory.CreateDocumentAsync(media, null, asDocument);
            }

            // A file shows as a file, so a picture added as one needs no preview decoded.
            var preview = !asDocument && media is StoragePhoto or StorageVideo
                ? await ImageHelper.GetPreviewBitmapAsync(media, PreviewSide)
                : null;

            return content switch
            {
                InputMessagePhoto x => RichMediaSource.FromLocal(RichMediaKind.Photo, x.Photo, media, preview, x.Photo.Width, x.Photo.Height, 0),
                InputMessageVideo x => RichMediaSource.FromLocal(RichMediaKind.Video, x.Video, media, preview, x.Video.Width, x.Video.Height, x.Video.Duration),
                InputMessageAnimation x => RichMediaSource.FromLocal(RichMediaKind.Animation, x.Animation, media, preview, x.Animation.Width, x.Animation.Height, x.Animation.Duration),
                InputMessageAudio x => RichMediaSource.FromLocal(RichMediaKind.Audio, x.Audio, media, null, 0, 0, x.Audio.Duration),
                InputMessageVoiceNote x => RichMediaSource.FromLocal(RichMediaKind.VoiceNote, x.VoiceNote, media, null, 0, 0, x.VoiceNote.Duration),
                InputMessageDocument x => RichMediaSource.FromLocal(RichMediaKind.Document, x.Document, media, preview, media.Width, media.Height, 0),

                // A sticker has no rich message block; it goes in as the file it is.
                InputMessageSticker x => RichMediaSource.FromLocal(RichMediaKind.Document, new InputDocument(x.Sticker.Sticker, null, true), media, preview, 0, 0, 0),
                _ => null
            };
        }
    }
}
