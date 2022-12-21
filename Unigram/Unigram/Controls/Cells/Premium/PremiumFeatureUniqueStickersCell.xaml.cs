using System.Collections.Generic;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Services;
using Windows.System;
using Windows.UI.Xaml.Controls;

namespace Unigram.Controls.Cells.Premium
{
    public sealed partial class PremiumFeatureUniqueStickersCell : UserControl
    {
        private readonly DispatcherQueue _dispatcher;

        private IClientService _clientService;

        private IList<Sticker> _stickers;
        private int _index;

        public PremiumFeatureUniqueStickersCell()
        {
            InitializeComponent();

            _dispatcher = DispatcherQueue.GetForCurrentThread();
        }

        public void UpdateFature(IClientService clientService, IList<Sticker> stickers)
        {
            if (stickers == null)
            {
                return;
            }

            _clientService = clientService;

            _stickers = stickers;
            _index = 0;

            UpdateSticker();
        }

        public void UpdateSticker()
        {
            var index = _index + 1;
            if (index >= _stickers.Count)
            {
                index = 0;
            }

            if (index < _stickers.Count )
            {
                var sticker = _stickers[index];
                if (sticker.FullType is StickerTypeFullInfoRegular regular)
                {
                    Animation1.Source = UriEx.ToLocal(sticker.StickerValue.Local.Path);
                    PremiumAnimation1.Source = UriEx.ToLocal(regular.PremiumAnimation.Local.Path);
                }

                _index = index;
                PreloadSticker();
            }
        }

        private void PreloadSticker()
        {
            var index = _index;
            if (index >= _stickers.Count)
            {
                index = 0;
            }

            var sticker = _stickers[index];
            if (sticker.FullType is StickerTypeFullInfoRegular regular)
            {
                _clientService.DownloadFile(sticker.StickerValue.Id, 32);
                _clientService.DownloadFile(regular.PremiumAnimation.Id, 32);
            }
        }

        private void OnPositionChanged(object sender, double e)
        {
            if (e == 1 && _stickers.Count > 1)
            {
                _dispatcher.TryEnqueue(UpdateSticker);
            }
        }
    }
}
