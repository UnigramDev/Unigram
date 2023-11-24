//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Telegram.Common;
using Telegram.Services;
using Telegram.Td.Api;

namespace Telegram.Streams
{
    public class CustomEmojiFileSource : DelayedFileSource
    {
        private readonly IClientService _clientService;
        private readonly long _customEmojiId;

        public CustomEmojiFileSource(IClientService clientService, long customEmojiId)
            : base(clientService, null as File)
        {
            _clientService = clientService;
            _customEmojiId = customEmojiId;
        }

        public override long Id => _customEmojiId;

        public override async void DownloadFile(object sender, UpdateHandler<File> handler)
        {
            if (_file != null && _file.Local.IsDownloadingCompleted)
            {
                handler(sender, _file);
            }
            else
            {
                if (_file == null)
                {
                    var response = await _clientService.SendAsync(new GetCustomEmojiStickers(new[] { _customEmojiId }));
                    if (response is Stickers stickers && stickers.StickersValue.Count == 1)
                    {
                        _file = stickers.StickersValue[0].StickerValue;

                        NeedsRepainting = stickers.StickersValue[0].FullType is StickerFullTypeCustomEmoji customEmoji
                            && customEmoji.NeedsRepainting;
                    }
                }

                if (_file == null)
                {
                    return;
                }
                else if (_file.Local.IsDownloadingCompleted)
                {
                    handler(sender, _file);
                    return;
                }

                UpdateManager.Subscribe(sender, _clientService, _file, ref _fileToken, handler, true);

                if (_file.Local.CanBeDownloaded && !_file.Local.IsDownloadingActive)
                {
                    _clientService.DownloadFile(_file.Id, 16);
                }
            }
        }

        public override bool Equals(object obj)
        {
            if (obj is CustomEmojiFileSource y)
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
