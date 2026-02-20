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

namespace Telegram.Streams
{
    public partial class CustomEmojiFileSource : DelayedFileSource
    {
        private readonly long _customEmojiId;

        public CustomEmojiFileSource(IClientService clientService, long customEmojiId)
            : base(clientService, null as File)
        {
            _customEmojiId = customEmojiId;

            DownloadFile(null, DelayedFileDownload.Loaded, null);
        }

        public CustomEmojiFileSource(IClientService clientService, EmojiStatusType type)
            : base(clientService, null as File)
        {
            if (type is EmojiStatusTypeCustomEmoji customEmoji)
            {
                _customEmojiId = customEmoji.CustomEmojiId;
            }
            else if (type is EmojiStatusTypeUpgradedGift upgradedGift)
            {
                _customEmojiId = upgradedGift.ModelCustomEmojiId;
            }

            DownloadFile(null, DelayedFileDownload.Loaded, null);
        }

        public override long Id => _customEmojiId;

        public override async void DownloadFile(object sender, DelayedFileDownload download, UpdateHandler<File> handler)
        {
            if (_file != null && _file.Local.IsDownloadingCompleted && download != DelayedFileDownload.Unloaded)
            {
                handler?.Invoke(sender, _file);
            }
            else
            {
                if (_file == null && download != DelayedFileDownload.Unloaded)
                {
                    var response = await _clientService.SendAsync(new GetCustomEmojiStickers([_customEmojiId]));
                    if (response is Stickers stickers && stickers.StickersValue.Count == 1)
                    {
                        var sticker = stickers.StickersValue[0];

                        _file = sticker.StickerValue;
                        Format = sticker.Format;
                        Width = sticker.Width;
                        Height = sticker.Height;
                        NeedsRepainting = sticker.FullType is StickerFullTypeCustomEmoji { NeedsRepainting: true };
                    }
                }

                if (_file == null)
                {
                    return;
                }
                else if (_file.Local.IsDownloadingCompleted && download != DelayedFileDownload.Unloaded)
                {
                    handler?.Invoke(sender, _file);
                    return;
                }

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

        public override bool Equals(object obj)
        {
            if (obj is CustomEmojiFileSource y && !y.IsUnique && !IsUnique)
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
