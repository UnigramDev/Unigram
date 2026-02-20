//
// Copyright (c) Fela Ameghino 2015-2026
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
    public enum DelayedFileDownload
    {
        Loaded,
        Playing,
        Unloaded
    }

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
                DownloadFile(null, DelayedFileDownload.Loaded, null);
            }
        }

        public static DelayedFileSource FromSticker(IClientService clientService, Sticker sticker)
        {
            if (clientService == null || sticker == null)
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

            if (stickerSet?.Covers?.Count > 0)
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
        }

        public DelayedFileSource(IClientService clientService, StickerViewModel sticker)
            : this(clientService, sticker.StickerValue)
        {
            Format = sticker.Format;
            Width = sticker.Width;
            Height = sticker.Height;
            NeedsRepainting = sticker.FullType is StickerFullTypeCustomEmoji { NeedsRepainting: true };
        }

        public override void RequestOutline()
        {
            if (_file != null && _file.Id != 0)
            {
                _clientService.Send(new GetStickerOutline(_file.Id, false, false), OutlineRequested);
            }
        }

        private void OutlineRequested(Object response)
        {
            if (response is Outline outline)
            {
                Outline = outline.Paths;
                OnOutlineChanged();
            }
        }

        public override string FilePath => _file?.Local.Path;

        public override long Id => _file.Id;

        public bool IsDownloadingCompleted => _file?.Local.IsDownloadingCompleted ?? false;

        public virtual void DownloadFile(object sender, DelayedFileDownload download, UpdateHandler<File> handler)
        {
            if (_file.Local.IsDownloadingCompleted && download != DelayedFileDownload.Unloaded)
            {
                handler?.Invoke(sender, _file);
            }
            else
            {
                if (handler != null && download != DelayedFileDownload.Unloaded)
                {
                    UpdateManager.Subscribe(sender, _clientService, _file, ref _fileToken, handler, true);
                }

                if (_file.Local.CanBeDownloaded /*&& !_file.Local.IsDownloadingActive*/)
                {
                    _clientService.DownloadFile(_file.Id, download == DelayedFileDownload.Playing ? 16 : 15);
                }
            }
        }

        public void Complete()
        {
            DownloadFile(null, DelayedFileDownload.Unloaded, null);
            UpdateManager.Unsubscribe(this, ref _fileToken);
        }

        public override bool Equals(object obj)
        {
            if (obj is DelayedFileSource y && !y.IsUnique && !IsUnique)
            {
                return y.Id == Id && y.IsAnimated == IsAnimated;
            }

            return base.Equals(obj);
        }

        public override int GetHashCode()
        {
            if (IsUnique)
            {
                return base.GetHashCode();
            }

            return HashCode.Combine(Id, IsAnimated);
        }
    }
}
