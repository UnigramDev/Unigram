using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Helpers;

namespace Telegram.Api.TL
{
    public abstract partial class TLMessagesArchivedStickers : ITLStickers
    {
        #region ITLStickers implementation

        public TLVector<TLDocumentBase> Documents { get; set; }

        public TLVector<TLStickerPack> Packs { get; set; }

        TLVector<TLStickerSet> ITLStickers.Sets
        {
            get
            {
                var sets = new TLVector<TLStickerSet>();
                foreach (var setCovered in Sets)
                {
                    sets.Add(setCovered.Set);
                }
                return sets;
            }
            set
            {
                Execute.ShowDebugMessage("TLMessagesArchivedStickers.Sets set");
            }
        }

        #endregion

        public TLVector<TLMessagesStickerSet> MessagesStickerSets { get; set; }
    }
}
