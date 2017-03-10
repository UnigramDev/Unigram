using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Services;
using Telegram.Api.TL;

namespace Unigram.Core.Services
{
    public class StickersService
    {
        private readonly IMTProtoService _protoService;

        public Task GetStickerSet(long id, long accessHash)
        {
            return Task.Run(async () =>
            {
                var update = false;
                var cached = DatabaseContext.Current.SelectStickerSet(id);
                if (cached == null)
                {
                    update = true;
                }

                var response = await _protoService.GetStickerSetAsync(new TLInputStickerSetID { Id = id, AccessHash = accessHash });
                if (response.IsSucceeded)
                {
                    if (update || cached?.Hash != response.Result.Set.Hash)
                    {

                    }
                }

                return cached;
            });
        }
    }
}
