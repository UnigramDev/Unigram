//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Collections;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Td.Api;
using Windows.UI.Xaml.Navigation;

namespace Telegram.ViewModels.Supergroups
{
    public enum SupergroupAvailableReactions
    {
        All,
        Some,
        None
    }

    public class SupergroupReactionsViewModel : ViewModelBase
    {
        public SupergroupReactionsViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
            Items = new MvxObservableCollection<SupergroupReactionOption>();
        }

        protected Chat _chat;
        public Chat Chat
        {
            get => _chat;
            set => Set(ref _chat, value);
        }

        private SupergroupAvailableReactions _available;
        public SupergroupAvailableReactions Available
        {
            get => _available;
            set => SetAvailable(value);
        }

        private void SetAvailable(SupergroupAvailableReactions value)
        {
            if (Set(ref _available, value, nameof(Available)))
            {
                if (value == SupergroupAvailableReactions.Some)
                {
                    Items.ReplaceWith(_items);
                }
                else
                {
                    Items.Clear();
                }

                RaisePropertyChanged(nameof(IsAllSelected));
                RaisePropertyChanged(nameof(IsSomeSelected));
                RaisePropertyChanged(nameof(IsNoneSelected));
            }
        }

        public bool IsAllSelected
        {
            get => _available == SupergroupAvailableReactions.All;
            set => SetAvailable(SupergroupAvailableReactions.All);
        }

        public bool IsSomeSelected
        {
            get => _available == SupergroupAvailableReactions.Some;
            set => SetAvailable(SupergroupAvailableReactions.Some);
        }

        public bool IsNoneSelected
        {
            get => _available == SupergroupAvailableReactions.None;
            set => SetAvailable(SupergroupAvailableReactions.None);
        }

        private List<SupergroupReactionOption> _items;
        public MvxObservableCollection<SupergroupReactionOption> Items { get; private set; }

        protected override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, NavigationState state)
        {
            var chatId = (long)parameter;

            Chat = ClientService.GetChat(chatId);

            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            var reactions = await ClientService.GetAllReactionsAsync();
            if (reactions == null)
            {
                return;
            }

            var items = reactions.Where(x => x.Value.IsActive).Select(x => new SupergroupReactionOption(x.Value, chat.AvailableReactions.Contains(x.Key))).ToList();
            var selected = items.Count(x => x.IsSelected);

            var available = chat.AvailableReactions is ChatAvailableReactionsAll
                ? SupergroupAvailableReactions.All
                : selected == 0
                ? SupergroupAvailableReactions.None
                : SupergroupAvailableReactions.Some;

            _items = items;
            Available = available;
        }

        public async void Execute()
        {
            if (_chat is not Chat chat || chat.Type is not ChatTypeSupergroup supergroup)
            {
                return;
            }

            var items = _items.Where(x => x.IsSelected).Select(x => new ReactionTypeEmoji(x.Reaction.Emoji)).ToList<ReactionType>();
            var available = _available;

            if (supergroup.IsChannel && items.Count == _items.Count)
            {
                available = SupergroupAvailableReactions.All;
            }

            ChatAvailableReactions value = _available switch
            {
                SupergroupAvailableReactions.All => new ChatAvailableReactionsAll(),
                SupergroupAvailableReactions.None => new ChatAvailableReactionsSome(Array.Empty<ReactionType>()),
                SupergroupAvailableReactions.Some => new ChatAvailableReactionsSome(items),
                _ => null
            };

            if (value == null || value.AreTheSame(chat.AvailableReactions))
            {
                NavigationService.GoBack();
                return;
            }

            var response = await ClientService.SendAsync(new SetChatAvailableReactions(chat.Id, value));
            NavigationService.GoBack();
        }
    }

    public class SupergroupReactionOption : BindableBase
    {
        public SupergroupReactionOption(EmojiReaction reaction, bool selected)
        {
            Reaction = reaction;
            IsSelected = selected;
        }

        public EmojiReaction Reaction { get; private set; }

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set => Set(ref _isSelected, value);
        }
    }
}
