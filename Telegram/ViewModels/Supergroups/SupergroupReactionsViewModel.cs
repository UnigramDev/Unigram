//
// Copyright Fela Ameghino 2015-2024
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
                RaisePropertyChanged(nameof(IsSomeSelectedInChannel));
                RaisePropertyChanged(nameof(IsNoneSelected));
            }
        }

        public bool IsAllSelected
        {
            get => _available == SupergroupAvailableReactions.All;
            set
            {
                if (value)
                {
                    SetAvailable(SupergroupAvailableReactions.All);
                }
            }
        }

        public bool IsSomeSelected
        {
            get => _available == SupergroupAvailableReactions.Some;
            set
            {
                if (value)
                {
                    SetAvailable(SupergroupAvailableReactions.Some);
                }
            }
        }

        public bool IsSomeSelectedInChannel => IsSomeSelected && Chat?.Type is ChatTypeSupergroup { IsChannel: true };

        public bool IsNoneSelected
        {
            get => _available == SupergroupAvailableReactions.None;
            set
            {
                if (value)
                {
                    SetAvailable(SupergroupAvailableReactions.None);
                }
            }
        }

        // TODO: ???
        public int MaximumMaxReactionCount => 11;

        private int _maxReactionCount;
        public int MaxReactionCount
        {
            get => _maxReactionCount;
            set => Set(ref _maxReactionCount, value);
        }

        private bool _canEnablePaidReaction;
        public bool CanEnablePaidReaction
        {
            get => _canEnablePaidReaction;
            set => Set(ref _canEnablePaidReaction, value);
        }

        private bool _enablePaidReactions;
        public bool EnablePaidReactions
        {
            get => _enablePaidReactions;
            set => Set(ref _enablePaidReactions, value);
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

            AllowCustomEmoji = chat.Type is ChatTypeSupergroup { IsChannel: true };

            MaxReactionCount = chat.AvailableReactions switch
            {
                ChatAvailableReactionsSome some => some.MaxReactionCount,
                ChatAvailableReactionsAll all => all.MaxReactionCount,
                _ => 11
            };

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
            EnablePaidReactions = items.Any(x => x is ReactionTypePaid);

            if (ClientService.TryGetSupergroupFull(chat, out SupergroupFullInfo cached))
            {
                CanEnablePaidReaction = cached.CanEnablePaidReaction;
            }
            else if (chat.Type is ChatTypeSupergroup supergroup)
            {
                var fullInfo = await ClientService.SendAsync(new GetSupergroupFullInfo(supergroup.SupergroupId)) as SupergroupFullInfo;
                if (fullInfo != null)
                {
                    CanEnablePaidReaction = cached.CanEnablePaidReaction;
                }
            }

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

            var maxReactionCount = supergroup.IsChannel
                ? MaxReactionCount
                : MaximumMaxReactionCount;

            var items = Items.ToList();
            if (CanEnablePaidReaction && EnablePaidReactions)
            {
                items.Insert(0, new ReactionTypePaid());
            }

            ChatAvailableReactions value = _available switch
            {
                SupergroupAvailableReactions.All => new ChatAvailableReactionsAll(),
                SupergroupAvailableReactions.None => new ChatAvailableReactionsSome(Array.Empty<ReactionType>(), maxReactionCount),
                SupergroupAvailableReactions.Some => new ChatAvailableReactionsSome(items, maxReactionCount),
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
