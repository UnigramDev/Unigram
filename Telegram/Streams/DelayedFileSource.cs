//
// Copyright Fela Ameghino 2015-2025
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using Telegram.Common;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.ViewModels.Drawers;

namespace Telegram.Streams
{
    public partial class DelayedFileSource : LocalFileSource
    {
        protected readonly IClientService _clientService;

        protected File _file;
        protected long _fileToken;

        public DelayedFileSource(IClientService clientService, File file)
            : base(file)
        {
            _clientService = clientService;
            _file = file;

            if (file != null)
            {
                DownloadFile(null, null);
            }
        }

        public static DelayedFileSource FromSticker(IClientService clientService, Sticker sticker)
        {
            if (sticker == null)
            {
                return null;
            }

            return new DelayedFileSource(clientService, sticker);
        }

        public static DelayedFileSource FromStickerSet(IClientService clientService, StickerSet stickerSet)
        {
            if (stickerSet.Thumbnail != null)
            {
                StickerFormat format = stickerSet.Thumbnail.Format switch
                {
                    ThumbnailFormatWebp => new StickerFormatWebp(),
                    ThumbnailFormatWebm => new StickerFormatWebm(),
                    ThumbnailFormatTgs => new StickerFormatTgs(),
                    _ => default
                };

                StickerFullType fullType = stickerSet.NeedsRepainting
                    ? new StickerFullTypeCustomEmoji(0, true)
                    : new StickerFullTypeRegular();

                if (stickerSet.Thumbnail.Format is ThumbnailFormatTgs)
                {
                    return new DelayedFileSource(clientService, stickerSet.Thumbnail.File)
                    {
                        Format = format,
                        Width = 512,
                        Height = 512,
                        NeedsRepainting = stickerSet.NeedsRepainting,
                        Outline = stickerSet.ThumbnailOutline?.Paths ?? Array.Empty<ClosedVectorPath>(),
                    };
                }

                return new DelayedFileSource(clientService, stickerSet.Thumbnail.File)
                {
                    Format = format,
                    Width = stickerSet.Thumbnail.Width,
                    Height = stickerSet.Thumbnail.Height,
                    NeedsRepainting = stickerSet.NeedsRepainting,
                    Outline = stickerSet.ThumbnailOutline?.Paths ?? Array.Empty<ClosedVectorPath>(),
                };
            }

            if (stickerSet.Stickers?.Count > 0)
            {
                return DelayedFileSource.FromSticker(clientService, stickerSet.Stickers[0]);
            }

            return null;
        }

        public static DelayedFileSource FromStickerSetInfo(IClientService clientService, StickerSetInfo stickerSet)
        {
            if (stickerSet?.Thumbnail != null)
            {
                StickerFormat format = stickerSet.Thumbnail.Format switch
                {
                    ThumbnailFormatWebp => new StickerFormatWebp(),
                    ThumbnailFormatWebm => new StickerFormatWebm(),
                    ThumbnailFormatTgs => new StickerFormatTgs(),
                    _ => default
                };

                StickerFullType fullType = stickerSet.NeedsRepainting
                    ? new StickerFullTypeCustomEmoji(0, true)
                    : new StickerFullTypeRegular();

                if (stickerSet.Thumbnail.Format is ThumbnailFormatTgs)
                {
                    return new DelayedFileSource(clientService, stickerSet.Thumbnail.File)
                    {
                        Format = format,
                        Width = 512,
                        Height = 512,
                        NeedsRepainting = stickerSet.NeedsRepainting,
                        Outline = stickerSet.ThumbnailOutline?.Paths ?? Array.Empty<ClosedVectorPath>(),
                    };
                }

                return new DelayedFileSource(clientService, stickerSet.Thumbnail.File)
                {
                    Format = format,
                    Width = stickerSet.Thumbnail.Width,
                    Height = stickerSet.Thumbnail.Height,
                    NeedsRepainting = stickerSet.NeedsRepainting,
                    Outline = stickerSet.ThumbnailOutline?.Paths ?? Array.Empty<ClosedVectorPath>(),
                };
            }

            if (stickerSet.Covers?.Count > 0)
            {
                return DelayedFileSource.FromSticker(clientService, stickerSet.Covers[0]);
            }

            return null;
        }

        public DelayedFileSource(IClientService clientService, Sticker sticker)
            : this(clientService, sticker.StickerValue)
        {
            Format = sticker.Format;
            Width = sticker.Width;
            Height = sticker.Height;
            NeedsRepainting = sticker.FullType is StickerFullTypeCustomEmoji { NeedsRepainting: true };

            OnOutlineChanged(sticker.StickerValue);
        }

        public DelayedFileSource(IClientService clientService, StickerViewModel sticker)
            : this(clientService, sticker.StickerValue)
        {
            Format = sticker.Format;
            Width = sticker.Width;
            Height = sticker.Height;
            NeedsRepainting = sticker.FullType is StickerFullTypeCustomEmoji { NeedsRepainting: true };

            OnOutlineChanged(sticker.StickerValue);
        }

        protected async void OnOutlineChanged(File file)
        {
            if (file == null || file.Id == 0)
            {
                return;
            }

            var response = await _clientService.SendAsync(new GetStickerOutline(file.Id, false, false));
            if (response is Outline outline)
            {
                Outline = outline.Paths;
                OnOutlineChanged();
            }
        }

        public override string FilePath => _file?.Local.Path;

        public override long Id => _file.Id;

        public bool IsDownloadingCompleted => _file?.Local.IsDownloadingCompleted ?? false;

        public virtual void DownloadFile(object sender, UpdateHandler<File> handler)
        {
            if (_file.Local.IsDownloadingCompleted)
            {
                handler?.Invoke(sender, _file);
            }
            else
            {
                if (handler != null)
                {
                    UpdateManager.Subscribe(sender, _clientService, _file, ref _fileToken, handler, true);
                }

                if (_file.Local.CanBeDownloaded /*&& !_file.Local.IsDownloadingActive*/)
                {
                    _clientService.DownloadFile(_file.Id, 16);
                }
            }
        }

        public void Complete()
        {
            UpdateManager.Unsubscribe(this, ref _fileToken, true);
        }

        public override bool Equals(object obj)
        {
            if (obj is DelayedFileSource y && !y.IsUnique && !IsUnique)
            {
                return y.Id == Id;
            }

            return base.Equals(obj);
        }

        public override int GetHashCode()
        {
            if (IsUnique)
            {
                return base.GetHashCode();
            }

            return Id.GetHashCode();
        }
    }
}
