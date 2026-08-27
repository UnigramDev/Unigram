//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System.Linq;
using Telegram.Collections;
using Telegram.Native;
using Telegram.Navigation;
using Telegram.Services;
using Telegram.Td.Api;

namespace Telegram.ViewModels.Drawers
{
    public partial class EffectDrawerViewModel : ViewModelBase
    {
        private bool _updated;

        public EffectDrawerViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
            SavedReactions = new RangeObservableCollection<MessageEffect>();
            SavedStickers = new RangeObservableCollection<MessageEffect>();
        }

        public static EffectDrawerViewModel Create(ISession session)
        {
            var context = session.Resolve<EffectDrawerViewModel>();
            context.Dispatcher = DispatcherContext.Current;
            return context;
        }

        public RangeObservableCollection<MessageEffect> SavedReactions { get; private set; }
        public RangeObservableCollection<MessageEffect> SavedStickers { get; private set; }

        private RangeObservableCollection<MessageEffect> _searchReactions;
        public RangeObservableCollection<MessageEffect> SearchReactions
        {
            get => _searchReactions;
            set
            {
                Set(ref _searchReactions, value);
                RaisePropertyChanged(nameof(Reactions));
            }
        }

        private RangeObservableCollection<MessageEffect> _searchStickers;
        public RangeObservableCollection<MessageEffect> SearchStickers
        {
            get => _searchStickers;
            set
            {
                Set(ref _searchStickers, value);
                RaisePropertyChanged(nameof(Stickers));
            }
        }

        public RangeObservableCollection<MessageEffect> Reactions => SearchReactions ?? SavedReactions;
        public RangeObservableCollection<MessageEffect> Stickers => SearchStickers ?? SavedStickers;

        public async void Search(string query, bool emojiOnly)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                SearchReactions = null;
                SearchStickers = null;
            }
            else
            {
                var reactions = new RangeObservableCollection<MessageEffect>();
                var stickers = new RangeObservableCollection<MessageEffect>();
                SearchReactions = reactions;
                SearchStickers = stickers;

                var response = await ClientService.SendAsync(new SearchEmojis(query, new[] { NativeUtils.GetKeyboardCulture() }));
                if (response is EmojiKeywords keywords)
                {
                    foreach (var keyword in keywords.EmojiKeywordsValue)
                    {
                        reactions.AddRange(SavedReactions.Where(x => x.Emoji.Equals(keyword.Emoji)));
                        stickers.AddRange(SavedStickers.Where(x => x.Emoji.Equals(keyword.Emoji)));
                    }
                }
            }
        }

        public void Search(EmojiCategorySource source)
        {
            if (source is EmojiCategorySourceSearch search)
            {
                var reactions = new RangeObservableCollection<MessageEffect>();
                var stickers = new RangeObservableCollection<MessageEffect>();
                SearchReactions = reactions;
                SearchStickers = stickers;

                foreach (var keyword in search.Emojis)
                {
                    reactions.AddRange(SavedReactions.Where(x => x.Emoji.Equals(keyword)));
                    stickers.AddRange(SavedStickers.Where(x => x.Emoji.Equals(keyword)));
                }
            }
        }

        public async void Update()
        {
            if (_updated || ClientService.AvailableMessageEffects == null)
            {
                return;
            }

            _updated = true;

            SavedReactions.ReplaceWith(await ClientService.GetMessageEffectsAsync(ClientService.AvailableMessageEffects.ReactionEffectIds));
            SavedStickers.ReplaceWith(await ClientService.GetMessageEffectsAsync(ClientService.AvailableMessageEffects.StickerEffectIds));
        }
    }
}
