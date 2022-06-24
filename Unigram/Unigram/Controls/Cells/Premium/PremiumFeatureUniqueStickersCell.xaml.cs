using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Services;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace Unigram.Controls.Cells.Premium
{
    public sealed partial class PremiumFeatureUniqueStickersCell : UserControl
    {
        private readonly DispatcherQueue _dispatcher;

        private IProtoService _protoService;

        private IList<Sticker> _stickers;
        private int _index;

        public PremiumFeatureUniqueStickersCell()
        {
            InitializeComponent();

            _dispatcher = DispatcherQueue.GetForCurrentThread();
        }

        public void UpdateFature(IProtoService protoService, IList<Sticker> stickers)
        {
            if (stickers == null)
            {
                return;
            }

            _protoService = protoService;

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

            var sticker = _stickers[index];

            Animation1.Source = UriEx.ToLocal(sticker.StickerValue.Local.Path);
            PremiumAnimation1.Source = UriEx.ToLocal(sticker.PremiumAnimation.Local.Path);

            _index = index;
            PreloadSticker();
        }

        private void PreloadSticker()
        {
            var index = _index;
            if (index >= _stickers.Count)
            {
                index = 0;
            }

            var sticker = _stickers[index];

            _protoService.DownloadFile(sticker.StickerValue.Id, 32);
            _protoService.DownloadFile(sticker.PremiumAnimation.Id, 32);
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
