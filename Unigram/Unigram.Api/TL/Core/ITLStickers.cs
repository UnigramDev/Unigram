using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.TL;

namespace Telegram.Api.TL
{
    public interface ITLStickers
    {
        //Int32 Hash { get; set; }

        TLVector<TLStickerSet> Sets { get; set; }

        TLVector<TLStickerPack> Packs { get; set; }

        TLVector<TLDocumentBase> Documents { get; set; }
    }
}
