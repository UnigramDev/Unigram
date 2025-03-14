//
// Copyright Fela Ameghino 2015-2025
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Telegram.Common;
using Telegram.Services;
using Telegram.Td.Api;

namespace Telegram.Streams
{
    public partial class AnimatedEmojiFileSource : DelayedFileSource
    {
        private readonly IClientService _clientService;
        private readonly string _emoji;

        public AnimatedEmojiFileSource(IClientService clientService, string emoji)
            : base(clientService, null as File)
        {
            _clientService = clientService;
            _emoji = emoji;

            DownloadFile(null, null);
        }

        public override long Id => _emoji.GetHashCode();

        public override async void DownloadFile(object sender, UpdateHandler<File> handler)
        {
            if (_file != null && _file.Local.IsDownloadingCompleted)
            {
                handler?.Invoke(sender, _file);
            }
            else
            {
                if (_file == null)
                {
                    var response = await _clientService.SendAsync(new GetAnimatedEmoji(_emoji));
                    if (response is AnimatedEmoji emoji)
                    {
                        var sticker = emoji.Sticker;

                        _file = sticker.StickerValue;
                        Format = sticker.Format;
                        Width = sticker.Width;
                        Height = sticker.Height;
                        NeedsRepainting = sticker.FullType is StickerFullTypeCustomEmoji { NeedsRepainting: true };

                        OnOutlineChanged(sticker.StickerValue);
                    }
                }

                if (_file == null)
                {
                    return;
                }
                else if (_file.Local.IsDownloadingCompleted)
                {
                    handler?.Invoke(sender, _file);
                    return;
                }

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

        public override bool Equals(object obj)
        {
            if (obj is CustomEmojiFileSource y && !y.IsUnique && !IsUnique)
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

            return _emoji.GetHashCode();
        }
    }
}
