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
            SavedReactions = new DiffObservableCollection<MessageEffect>();
            SavedStickers = new DiffObservableCollection<MessageEffect>();
        }

        public static EffectDrawerViewModel Create(ISession session)
        {
            var context = session.Resolve<EffectDrawerViewModel>();
            context.Dispatcher = DispatcherContext.Current;
            return context;
        }

        public DiffObservableCollection<MessageEffect> SavedReactions { get; private set; }
        public DiffObservableCollection<MessageEffect> SavedStickers { get; private set; }

        private DiffObservableCollection<MessageEffect> _searchReactions;
        public DiffObservableCollection<MessageEffect> SearchReactions
        {
            get => _searchReactions;
            set
            {
                Set(ref _searchReactions, value);
                RaisePropertyChanged(nameof(Reactions));
            }
        }

        private DiffObservableCollection<MessageEffect> _searchStickers;
        public DiffObservableCollection<MessageEffect> SearchStickers
        {
            get => _searchStickers;
            set
            {
                Set(ref _searchStickers, value);
                RaisePropertyChanged(nameof(Stickers));
            }
        }

        public DiffObservableCollection<MessageEffect> Reactions => SearchReactions ?? SavedReactions;
        public DiffObservableCollection<MessageEffect> Stickers => SearchStickers ?? SavedStickers;

        public async void Search(string query, bool emojiOnly)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                SearchReactions = null;
                SearchStickers = null;
            }
            else
            {
                var reactions = new DiffObservableCollection<MessageEffect>();
                var stickers = new DiffObservableCollection<MessageEffect>();
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
                var reactions = new DiffObservableCollection<MessageEffect>();
                var stickers = new DiffObservableCollection<MessageEffect>();
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
