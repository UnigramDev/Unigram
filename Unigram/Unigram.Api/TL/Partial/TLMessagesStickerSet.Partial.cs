using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Services.Cache;

namespace Telegram.Api.TL.Messages
{
    public partial class TLMessagesStickerSet
    {
        public object Cover
        {
            get
            {
                // TODO: maybe a bit dirty
                //if (Set.ShortName.Equals("tg/groupStickers"))
                //{
                //    return InMemoryCacheService.Current.GetChat((int)Set.Id);
                //}

                if (Documents != null && Documents.Count > 0)
                {
                    return Documents[0];
                }

                return null;
            }
        }
    }
}
