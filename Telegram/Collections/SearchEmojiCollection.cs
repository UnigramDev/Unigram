//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System.Linq;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Native;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.ViewModels.Drawers;

namespace Telegram.Collections
{
    public partial class SearchEmojiCollection : IncrementalCollection<object>
    {
        private readonly IClientService _clientService;
        private readonly string _query;
        private readonly EmojiDrawerMode _mode;

        public SearchEmojiCollection(IClientService clientService, string query, EmojiDrawerMode mode)
        {
            _clientService = clientService;
            _query = query;
            _mode = mode;
        }

        // One response covers the query, so there is never a second page.
        protected override async Task<IncrementalLoadResult> OnLoadMoreItemsAsync(uint count)
        {
            var totalCount = 0u;
            var inputLanguage = NativeUtils.GetKeyboardCulture();

            var response = await _clientService.SendAsync(new SearchEmojis(_query, new[] { inputLanguage }));
            if (response is EmojiKeywords suggestions)
            {
                if (_clientService.IsPremium)
                {
                    var stickers = await Emoji.SearchAsync(_clientService, suggestions.EmojiKeywordsValue.DistinctBy(x => x.Emoji).Select(x => x.Emoji));

                    foreach (var item in stickers)
                    {
                        Add(item);
                        totalCount++;
                    }
                }

                if (_mode == EmojiDrawerMode.Chat)
                {
                    foreach (var item in suggestions.EmojiKeywordsValue.DistinctBy(x => x.Emoji))
                    {
                        var emoji = item.Emoji;
                        if (Emoji.EmojiGroupInternal._skinEmojis.Contains(emoji) || Emoji.EmojiGroupInternal._skinEmojis.Contains(emoji.TrimEnd('\uFE0F')))
                        {
                            Add(AppSettings.Emoji.GetEmojiSkinTone(emoji));
                        }
                        else
                        {
                            Add(new EmojiData(item.Emoji));
                        }

                        totalCount++;
                    }
                }
            }

            return new IncrementalLoadResult(totalCount, false);
        }
    }
}
