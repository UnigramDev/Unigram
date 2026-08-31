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

            if (clientService == null)
            {
                WatchDog.TrackError("clientService == null");
            }

            DownloadFile(DelayedFileDownload.Loaded);
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

            DownloadFile(DelayedFileDownload.Loaded);
        }

        public override long Id => _customEmojiId;

        public override async void DownloadFile(DelayedFileDownload download)
        {
            if (_file != null && _file.Local.IsDownloadingCompleted)
            {
                OnDownloaded();
            }
            else if (download != DelayedFileDownload.Unloaded)
            {
                if (_file == null)
                {
                    var response = await _clientService.SendAsync(new GetCustomEmojiStickers([_customEmojiId]));
                    if (response is Stickers stickers && stickers.StickersValue.Count == 1)
                    {
                        SetSticker(stickers.StickersValue[0]);
                    }
                }

                if (_file == null)
                {
                    return;
                }

                if (_file.Local.IsDownloadingCompleted)
                {
                    OnDownloaded();
                    return;
                }

                UpdateManager.Subscribe(this, _clientService, _file, ref _fileToken, OnFileUpdated, true);

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
