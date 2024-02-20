//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Telegram.Common;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.ViewModels.Drawers;

namespace Telegram.Streams
{
    public class DelayedFileSource : LocalFileSource
    {
        private readonly IClientService _clientService;

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

        public DelayedFileSource(IClientService clientService, Sticker sticker)
            : this(clientService, sticker.StickerValue)
        {
            Format = sticker.Format;
            Width = sticker.Width;
            Height = sticker.Height;
            Outline = sticker.Outline;
            NeedsRepainting = sticker.FullType is StickerFullTypeCustomEmoji { NeedsRepainting: true };
        }

        public DelayedFileSource(IClientService clientService, StickerViewModel sticker)
            : this(clientService, sticker.StickerValue)
        {
            Format = sticker.Format;
            Width = sticker.Width;
            Height = sticker.Height;
            Outline = sticker.Outline;
            NeedsRepainting = sticker.FullType is StickerFullTypeCustomEmoji { NeedsRepainting: true };
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
            if (obj is DelayedFileSource y)
            {
                return y.Id == Id;
            }

            return base.Equals(obj);
        }

        public override int GetHashCode()
        {
            return Id.GetHashCode();
        }
    }
}
