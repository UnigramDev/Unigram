using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Telegram.Api.TL
{
    public partial class TLMessagesStickerSet
    {
        public TLDocumentBase Cover
        {
            get
            {
                if (Documents != null && Documents.Count > 0)
                {
                    return Documents[0];
                }

                return null;
            }
        }
    }
}
