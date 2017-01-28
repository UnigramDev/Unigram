using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.TL;
using Windows.Foundation;
using Windows.Storage.Streams;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;

namespace Unigram.Common
{
    public class StickerBitmapSource : BitmapSource
    {
        public bool IsSet { get; private set; }

        public void SetStream(IRandomAccessStream streamSource)
        {
            IsSet = true;
            SetSource(streamSource);
        }

        public IAsyncAction SetStreamAsync(IRandomAccessStream streamSource)
        {
            IsSet = true;
            return SetSourceAsync(streamSource);
        }
    }
}
