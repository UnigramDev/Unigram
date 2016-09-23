using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Telegram.Api.TL
{
    public partial class TLMessagesAllStickers : ITLStickers
    {
        #region ITLStickers implementation

        public TLVector<TLDocumentBase> Documents { get; set; }

        public TLVector<TLStickerPack> Packs { get; set; }

        #endregion

        public TLVector<TLMessagesStickerSet> MessagesStickerSets { get; set; }
    }
}
