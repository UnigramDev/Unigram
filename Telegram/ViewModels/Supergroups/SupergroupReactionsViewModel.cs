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
            Items = new MvxObservableCollection<EmojiReaction>();
            SelectedItems = new MvxObservableCollection<EmojiReaction>();
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
            if (_available == SupergroupAvailableReactions.Some)
            {
                _selected = SelectedItems.Select(x => x.Emoji).ToList();
            }

            if (Set(ref _available, value, nameof(Available)))
            {
                if (value == SupergroupAvailableReactions.Some)
                {
                    Items.ReplaceWith(_items);
                    SelectedItems.ReplaceWith(_items.Where(x => _selected.Contains(x.Emoji)));
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

        private List<EmojiReaction> _items;
        private List<string> _selected;
        public MvxObservableCollection<EmojiReaction> Items { get; private set; }

        public MvxObservableCollection<EmojiReaction> SelectedItems { get; private set; }

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

            var items = reactions.Where(x => x.Value.IsActive).Select(x => x.Value).ToList();
            var selected = items.Where(x => chat.AvailableReactions.Contains(x.Emoji)).Select(x => x.Emoji);

            var available = chat.AvailableReactions is ChatAvailableReactionsAll
                ? SupergroupAvailableReactions.All
                : selected.Any()
                ? SupergroupAvailableReactions.Some
                : SupergroupAvailableReactions.None;

            _items = items;
            _selected = selected.ToList();
            Available = available;
        }

        public void Execute()
        {
            if (_chat is not Chat chat || chat.Type is not ChatTypeSupergroup supergroup)
            {
                return;
            }

            var items = SelectedItems.Select(x => new ReactionTypeEmoji(x.Emoji)).ToList<ReactionType>();
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
                return;
            }

            ClientService.Send(new SetChatAvailableReactions(chat.Id, value));
        }
    }
}
