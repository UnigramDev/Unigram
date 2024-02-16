//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Collections.Generic;
using Telegram.Services;
using Telegram.Streams;
using Telegram.Td.Api;
using Windows.System;
using Windows.UI.Xaml.Controls;

namespace Telegram.Controls.Cells.Premium
{
    public sealed partial class PremiumFeatureUniqueStickersCell : UserControl
    {
        private readonly DispatcherQueue _dispatcher;

        private IClientService _clientService;

        private IList<Sticker> _stickers;
        private int _index;

        private bool _premiumCompleted;

        public PremiumFeatureUniqueStickersCell()
        {
            InitializeComponent();

            _dispatcher = DispatcherQueue.GetForCurrentThread();
        }

        public void UpdateFeature(IClientService clientService, IList<Sticker> stickers)
        {
            if (stickers == null)
            {
                return;
            }

            _clientService = clientService;

            _stickers = stickers;
            _index = 0;

            for (int i = stickers.Count - 1; i >= 0; i--)
            {
                var sticker = _stickers[i];
                if (sticker.FullType is StickerFullTypeRegular regular)
                {
                    _clientService.DownloadFile(sticker.StickerValue.Id, 32);
                    _clientService.DownloadFile(regular.PremiumAnimation.Id, 32);
                }
            }

            UpdateSticker();
        }

        public void UpdateSticker()
        {
            var index = _index + 1;
            if (index >= _stickers.Count)
            {
                index = 0;
            }

            if (index < _stickers.Count)
            {
                _index = index;
                _premiumCompleted = false;

                var sticker = _stickers[index];
                if (sticker.FullType is StickerFullTypeRegular regular)
                {
                    Animation1.Source = new DelayedFileSource(_clientService, sticker);
                    PremiumAnimation1.Source = new DelayedFileSource(_clientService, regular.PremiumAnimation);
                }
            }
        }

        private void Animation1_LoopCompleted(object sender, EventArgs e)
        {
            if (_premiumCompleted && _stickers.Count > 1)
            {
                _dispatcher.TryEnqueue(UpdateSticker);
            }
        }

        private void PremiumAnimation1_LoopCompleted(object sender, EventArgs e)
        {
            _premiumCompleted = true;
        }
    }
}
