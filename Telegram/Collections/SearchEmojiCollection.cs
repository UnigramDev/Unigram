//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Telegram.Common;
using Telegram.Native;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.ViewModels.Drawers;
using Windows.Foundation;
using Windows.UI.Xaml.Data;

namespace Telegram.Collections
{
    public partial class SearchEmojiCollection : ObservableCollection<object>, ISupportIncrementalLoading
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

        public IAsyncOperation<LoadMoreItemsResult> LoadMoreItemsAsync(uint count)
        {
            return AsyncInfo.Run(async token =>
            {
                var total = 0u;
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
                            total++;
                        }
                    }

                    if (_mode == EmojiDrawerMode.Chat)
                    {
                        foreach (var item in suggestions.EmojiKeywordsValue.DistinctBy(x => x.Emoji))
                        {
                            var emoji = item.Emoji;
                            if (Emoji.EmojiGroupInternal._skinEmojis.Contains(emoji) || Emoji.EmojiGroupInternal._skinEmojis.Contains(emoji.TrimEnd('\uFE0F')))
                            {
                                Add(SettingsService.Current.Emoji.GetEmojiSkinTone(emoji));
                            }
                            else
                            {
                                Add(new EmojiData(item.Emoji));
                            }

                            total++;
                        }
                    }
                }

                HasMoreItems = false;

                return new LoadMoreItemsResult
                {
                    Count = total
                };
            });
        }

        public bool HasMoreItems { get; private set; } = true;
    }
}
