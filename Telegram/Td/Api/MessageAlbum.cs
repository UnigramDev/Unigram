//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Collections.Generic;
using Telegram.Common;
using Telegram.ViewModels;
using Windows.Foundation;

namespace Telegram.Td.Api
{
    public class MessageAlbum : MessageContent
    {
        public bool IsMedia { get; }

        public FormattedText Caption { get; set; }

        public UniqueList<long, MessageViewModel> Messages { get; } = new UniqueList<long, MessageViewModel>(x => x.Id);

        public MessageAlbum(bool media)
        {
            IsMedia = media;
        }

        public const double ITEM_MARGIN = 2;
        public const double MAX_WIDTH = 320 + ITEM_MARGIN;
        public const double MAX_HEIGHT = 420 + ITEM_MARGIN;

        private ((Rect, MosaicItemPosition)[], Size)? _positions;

        public void Invalidate()
        {
            _positions = null;
        }

        public (Rect[], Size) GetPositionsForWidth(double w)
        {
            var positions = _positions ??= MosaicAlbumLayout.chatMessageBubbleMosaicLayout(new Size(MAX_WIDTH, MAX_HEIGHT), GetSizes());

            var ratio = w / positions.Item2.Width;
            var rects = new Rect[positions.Item1.Length];

            for (int i = 0; i < rects.Length; i++)
            {
                var rect = positions.Item1[i].Item1;

                var width = Math.Max(0, rect.Width * ratio);
                var height = Math.Max(0, rect.Height * ratio);

                width = double.IsNaN(width) ? 0 : width;
                height = double.IsNaN(height) ? 0 : height;

                rects[i] = new Rect(rect.X * ratio, rect.Y * ratio, width, height);
            }

            var finalWidth = Math.Max(0, positions.Item2.Width * ratio);
            var finalHeight = Math.Max(0, positions.Item2.Height * ratio);

            finalWidth = double.IsNaN(finalWidth) ? 0 : finalWidth;
            finalHeight = double.IsNaN(finalHeight) ? 0 : finalHeight;

            return (rects, new Size(finalWidth, finalHeight));
        }

        private IEnumerable<Size> GetSizes()
        {
            foreach (var message in Messages)
            {
                if (message.Content is MessagePhoto photoMedia)
                {
                    yield return GetClosestPhotoSizeWithSize(photoMedia.Photo.Sizes, 1280, false);
                }
                else if (message.Content is MessageVideo videoMedia)
                {
                    if (videoMedia.Video.Width != 0 && videoMedia.Video.Height != 0)
                    {
                        yield return new Size(videoMedia.Video.Width, videoMedia.Video.Height);
                    }
                    else if (videoMedia.Video.Thumbnail != null)
                    {
                        yield return new Size(videoMedia.Video.Thumbnail.Width, videoMedia.Video.Thumbnail.Height);
                    }
                    else
                    {
                        // We are returning a random size, it's still better than NaN.
                        yield return new Size(1280, 1280);
                    }
                }
            }
        }

        public static Size GetClosestPhotoSizeWithSize(IList<PhotoSize> sizes, int side)
        {
            return GetClosestPhotoSizeWithSize(sizes, side, false);
        }

        public static Size GetClosestPhotoSizeWithSize(IList<PhotoSize> sizes, int side, bool byMinSide)
        {
            if (sizes == null || sizes.IsEmpty())
            {
                // We are returning a random size, it's still better than NaN.
                return new Size(1280, 1280);
            }

            int lastSide = 0;
            PhotoSize closestObject = null;
            for (int a = 0; a < sizes.Count; a++)
            {
                PhotoSize obj = sizes[a];
                if (obj == null)
                {
                    continue;
                }

                int w = obj.Width;
                int h = obj.Height;

                if (byMinSide)
                {
                    int currentSide = h >= w ? w : h;
                    if (closestObject == null || side > 100 && side > lastSide && lastSide < currentSide)
                    {
                        closestObject = obj;
                        lastSide = currentSide;
                    }
                }
                else
                {
                    int currentSide = w >= h ? w : h;
                    if (closestObject == null || side > 100 && currentSide <= side && lastSide < currentSide)
                    {
                        closestObject = obj;
                        lastSide = currentSide;
                    }
                }
            }

            return new Size(closestObject.Width, closestObject.Height);
        }

        public NativeObject ToUnmanaged()
        {
            throw new NotImplementedException();
        }
    }
}
