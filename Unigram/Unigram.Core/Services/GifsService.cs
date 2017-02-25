using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Helpers;
using Telegram.Api.Services;
using Telegram.Api.TL;
using Unigram.Common;
using Unigram.Core;
using Windows.UI.Popups;

namespace Unigram.Services
{
    public interface IGifsService
    {
        Task<KeyedList<int, TLDocument>> GetSavedGifs();
    }

    public class GifsService : IGifsService
    {
        private readonly IMTProtoService _protoService;

        public GifsService(IMTProtoService protoService)
        {
            _protoService = protoService;
        }

        public async Task<KeyedList<int, TLDocument>> GetSavedGifs()
        {
            var count = DatabaseContext.Current.Count("Gifs");
            var hash = count > 0 ? SettingsHelper.GifsHash : 0;

            var response = await _protoService.GetSavedGifsAsync(hash);
            if (response.IsSucceeded)
            {
                var result = response.Result as TLMessagesSavedGifs;
                if (result != null)
                {
                    var gifs = new KeyedList<int, TLDocument>(result.Hash, result.Gifs.OfType<TLDocument>());

                    SettingsHelper.GifsHash = result.Hash;
                    DatabaseContext.Current.InsertDocuments("Gifs", gifs, true);

                    return gifs;
                }
                else
                {
                    var cached = DatabaseContext.Current.SelectDocuments("Gifs");
                    if (cached.Count > 0)
                    {
                        return new KeyedList<int, TLDocument>(SettingsHelper.GifsHash, cached);
                    }
                }
            }
            else
            {
                var cached = DatabaseContext.Current.SelectDocuments("Gifs");
                if (cached.Count > 0)
                {
                    return new KeyedList<int, TLDocument>(SettingsHelper.GifsHash, cached);
                }
            }

            return new KeyedList<int, TLDocument>(0);
        }
    }
}
