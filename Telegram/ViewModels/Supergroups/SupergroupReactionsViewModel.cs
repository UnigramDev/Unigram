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
            Items = new MvxObservableCollection<ReactionType>();
        }

        protected Chat _chat;
        public Chat Chat
        {
            get => _chat;
            set => Set(ref _chat, value);
        }

        public bool AllowCustomEmoji { get; private set; }

        public int BoostLevel { get; private set; }

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

        public MvxObservableCollection<ReactionType> Items { get; private set; }

        protected override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, NavigationState state)
        {
            var chatId = (long)parameter;

            Chat = ClientService.GetChat(chatId);

            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            AllowCustomEmoji = chat.Type is ChatTypeSupergroup supergroup && supergroup.IsChannel;

            IList<ReactionType> items = chat.AvailableReactions switch
            {
                ChatAvailableReactionsSome some => some.Reactions,
                ChatAvailableReactionsAll all => ClientService.ActiveReactions.Select(x => new ReactionTypeEmoji(x)).ToList<ReactionType>(),
                _ => Array.Empty<ReactionType>()
            };

            var available = chat.AvailableReactions is ChatAvailableReactionsAll
                ? SupergroupAvailableReactions.All
                : items.Any()
                ? SupergroupAvailableReactions.Some
                : SupergroupAvailableReactions.None;

            Items.ReplaceWith(items);
            Available = available;

            if (AllowCustomEmoji)
            {
                var response = await ClientService.SendAsync(new GetChatBoostStatus(chat.Id));
                if (response is ChatBoostStatus status)
                {
                    BoostLevel = status.Level;
                }
            }
        }

        public void Execute()
        {
            if (_chat is not Chat chat || chat.Type is not ChatTypeSupergroup supergroup)
            {
                return;
            }

            var count = Items.Count(x => x is ReactionTypeCustomEmoji);
            if (count > BoostLevel)
            {
                // TODO: show boost popup
                return;
            }

            ChatAvailableReactions value = _available switch
            {
                SupergroupAvailableReactions.All => new ChatAvailableReactionsAll(),
                SupergroupAvailableReactions.None => new ChatAvailableReactionsSome(Array.Empty<ReactionType>()),
                SupergroupAvailableReactions.Some => new ChatAvailableReactionsSome(Items),
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
