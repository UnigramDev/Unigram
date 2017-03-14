using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.TL;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace Unigram.Controls
{
    public class StickerView : ImageView
    {
        protected override void OnConstraintChanged(object newValue, object oldValue)
        {
            var document = newValue as TLDocument;
            if (document != null)
            {
                var stickerAttribute = document.Attributes.OfType<TLDocumentAttributeSticker>().FirstOrDefault();
                if (stickerAttribute != null)
                {
                    IsHitTestVisible = stickerAttribute.StickerSet.TypeId != TLType.InputStickerSetEmpty;
                }
            }
        }
    }
}
