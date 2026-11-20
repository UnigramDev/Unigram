//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Services.Factories;
using Telegram.Td.Api;
using Windows.UI.Xaml.Media;

namespace Telegram.Entities
{
    public enum PollMediaSlot
    {
        Poll,
        Option,
        Explanation
    }

    /// <summary>
    /// Media picked for a poll, one of its options or a quiz's explanation, as it will be sent.
    /// A file from this device goes in as a local file: TDLib uploads it with the poll, or with
    /// the option it is added with.
    /// </summary>
    public sealed partial class PollMediaDraft
    {
        // Twice the thumbnail's side, which covers up to 200% scale with a little to crop.
        private const int PreviewSide = 96;

        private PollMediaDraft(InputPollMedia input, ImageSource preview = null, Sticker sticker = null)
        {
            Input = input;
            Preview = preview;
            Sticker = sticker;
        }

        public InputPollMedia Input { get; }

        /// <summary>A photo or a video from this device, decoded small.</summary>
        public ImageSource Preview { get; }

        /// <summary>The sticker <see cref="Input"/> sends, to draw it from.</summary>
        public Sticker Sticker { get; }

        /// <summary>
        /// Whether <paramref name="slot"/> takes media of this kind. These are the server's rules:
        /// an option has no files or music, and only an option has stickers and links.
        /// </summary>
        public static bool IsAllowed(InputPollMedia input, PollMediaSlot slot)
        {
            return slot == PollMediaSlot.Option
                ? input is InputPollMediaAnimation or InputPollMediaLocation or InputPollMediaPhoto or InputPollMediaSticker or InputPollMediaLink or InputPollMediaVenue or InputPollMediaVideo
                : input is InputPollMediaAnimation or InputPollMediaAudio or InputPollMediaDocument or InputPollMediaLocation or InputPollMediaPhoto or InputPollMediaVenue or InputPollMediaVideo;
        }

        /// <summary>
        /// The media <paramref name="media"/> is sent as, compressed and converted as a message
        /// would be, or null if <paramref name="slot"/> cannot take it.
        /// </summary>
        public static async Task<PollMediaDraft> CreateAsync(StorageMedia media, PollMediaSlot slot, bool asDocument)
        {
            if (media is null or StorageInvalid)
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
                content = await MessageFactory.CreateVideoAsync(video, null, video.IsMuted, false, false, null, 0);
            }
            else
            {
                content = await MessageFactory.CreateDocumentAsync(media, null, asDocument);
            }

            InputPollMedia input = content switch
            {
                InputMessagePhoto x => new InputPollMediaPhoto(x.Photo),
                InputMessageVideo x => new InputPollMediaVideo(x.Video),
                InputMessageAnimation x => new InputPollMediaAnimation(x.Animation),
                InputMessageAudio x => new InputPollMediaAudio(x.Audio),
                InputMessageDocument x => new InputPollMediaDocument(x.Document),
                InputMessageSticker x when slot == PollMediaSlot.Option => new InputPollMediaSticker(x.Sticker),
                InputMessageSticker x => new InputPollMediaDocument(new InputDocument(x.Sticker.Sticker, null, true)),
                _ => null
            };

            if (input == null || !IsAllowed(input, slot))
            {
                return null;
            }

            var preview = input is InputPollMediaPhoto or InputPollMediaVideo or InputPollMediaAnimation
                ? await ImageHelper.GetPreviewBitmapAsync(media, PreviewSide)
                : null;

            return new PollMediaDraft(input, preview);
        }

        public static PollMediaDraft FromSticker(Sticker sticker)
        {
            // Sent by the id of the file it already is: nothing to upload.
            var input = new InputSticker(new InputFileId(sticker.StickerValue.Id), null, sticker.Width, sticker.Height);
            return new PollMediaDraft(new InputPollMediaSticker(input), sticker: sticker);
        }

        /// <summary>Music already on the server, as the music picker gives it.</summary>
        public static PollMediaDraft FromAudio(InputAudio audio)
        {
            return new PollMediaDraft(new InputPollMediaAudio(audio));
        }

        /// <summary>What a location picker gave: a point, or a venue.</summary>
        public static PollMediaDraft FromLocation(InputMessageContent content)
        {
            return content switch
            {
                InputMessageVenue venue => new PollMediaDraft(new InputPollMediaVenue(venue.Venue)),
                InputMessageLocation location => new PollMediaDraft(new InputPollMediaLocation(location.Location)),
                _ => null
            };
        }

        public static PollMediaDraft FromLink(string url)
        {
            return new PollMediaDraft(new InputPollMediaLink(url));
        }
    }
}
