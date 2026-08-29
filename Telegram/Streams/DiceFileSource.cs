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
    public partial class DiceFileSource : DelayedFileSource
    {
        private readonly DiceStickers _state;

        private long _part1Token;
        private long _part2Token;
        private long _part3Token;
        private long _part4Token;
        private long _part5Token;

        public DiceFileSource(IClientService clientService, DiceStickers state, int value, bool isContentUnread)
            : base(clientService, null as File)
        {
            _state = state;
            Value = value;
            IsContentUnread = isContentUnread;
            IsUnique = true;

            DownloadFile(null, DelayedFileDownload.Loaded, null);
        }

        public DiceStickers State => _state;

        public int Value { get; }

        public bool IsContentUnread { get; }

        public override long Id => GetHashCode();

        public override bool IsDownloadingCompleted => _state.IsDownloadingCompleted();

        public override async void DownloadFile(object sender, DelayedFileDownload download, UpdateHandler<File> handler)
        {
            if (_state.IsDownloadingCompleted() && download != DelayedFileDownload.Unloaded)
            {
                handler?.Invoke(_file);
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

            void UpdateFile(File update)
            {
                if (_state.IsDownloadingCompleted())
                {
                    handler(update);
                }
            }

            if (_state is DiceStickersRegular regular)
            {
                if (regular.Sticker.StickerValue.Local.CanBeDownloaded)
                {
                    // Unsubscribe all tokens
                    UpdateManager.Unsubscribe(this, ref _part2Token);
                    UpdateManager.Unsubscribe(this, ref _part3Token);
                    UpdateManager.Unsubscribe(this, ref _part4Token);
                    UpdateManager.Unsubscribe(this, ref _part5Token);

                    if (handler != null && download != DelayedFileDownload.Unloaded)
                    {
                        UpdateManager.Subscribe(this, _clientService, regular.Sticker.StickerValue, ref _part1Token, UpdateFile, true);
                    }

                    _clientService.DownloadFile(regular.Sticker.StickerValue.Id, download == DelayedFileDownload.Playing ? 16 : 15);
                }
            }
            else if (_state is DiceStickersSlotMachine slotMachine)
            {
                if (slotMachine.Background.StickerValue.Local.CanBeDownloaded)
                {
                    if (handler != null && download != DelayedFileDownload.Unloaded)
                    {
                        UpdateManager.Subscribe(this, _clientService, slotMachine.Background.StickerValue, ref _part1Token, UpdateFile, true);
                    }

                    _clientService.DownloadFile(slotMachine.Background.StickerValue.Id, download == DelayedFileDownload.Playing ? 16 : 15);
                }
                if (slotMachine.LeftReel.StickerValue.Local.CanBeDownloaded)
                {
                    if (handler != null && download != DelayedFileDownload.Unloaded)
                    {
                        UpdateManager.Subscribe(this, _clientService, slotMachine.LeftReel.StickerValue, ref _part2Token, UpdateFile, true);
                    }

                    _clientService.DownloadFile(slotMachine.LeftReel.StickerValue.Id, download == DelayedFileDownload.Playing ? 16 : 15);
                }
                if (slotMachine.CenterReel.StickerValue.Local.CanBeDownloaded && !slotMachine.CenterReel.StickerValue.Local.IsDownloadingActive)
                {
                    if (handler != null && download != DelayedFileDownload.Unloaded)
                    {
                        UpdateManager.Subscribe(this, _clientService, slotMachine.CenterReel.StickerValue, ref _part3Token, UpdateFile, true);
                    }

                    _clientService.DownloadFile(slotMachine.CenterReel.StickerValue.Id, download == DelayedFileDownload.Playing ? 16 : 15);
                }
                if (slotMachine.RightReel.StickerValue.Local.CanBeDownloaded)
                {
                    if (handler != null && download != DelayedFileDownload.Unloaded)
                    {
                        UpdateManager.Subscribe(this, _clientService, slotMachine.RightReel.StickerValue, ref _part4Token, UpdateFile, true);
                    }

                    _clientService.DownloadFile(slotMachine.RightReel.StickerValue.Id, download == DelayedFileDownload.Playing ? 16 : 15);
                }
                if (slotMachine.Lever.StickerValue.Local.CanBeDownloaded)
                {
                    if (handler != null && download != DelayedFileDownload.Unloaded)
                    {
                        UpdateManager.Subscribe(this, _clientService, slotMachine.Lever.StickerValue, ref _part5Token, UpdateFile, true);
                    }

                    _clientService.DownloadFile(slotMachine.Lever.StickerValue.Id, download == DelayedFileDownload.Playing ? 16 : 15);
                }
            }
        }
    }
}
