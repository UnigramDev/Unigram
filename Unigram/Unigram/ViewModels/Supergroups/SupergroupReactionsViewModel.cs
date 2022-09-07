using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Collections;
using Unigram.Common;
using Unigram.Navigation;
using Unigram.Navigation.Services;
using Unigram.Services;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels.Supergroups
{
    public enum SupergroupAvailableReactions
    {
        All,
        Some,
        None
    }

    public class SupergroupReactionsViewModel : TLViewModelBase
    {
        public SupergroupReactionsViewModel(IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(protoService, cacheService, settingsService, aggregator)
        {
            Items = new MvxObservableCollection<SupergroupReactionOption>();

            SendCommand = new RelayCommand(SendExecute);
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

            Chat = ProtoService.GetChat(chatId);

            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            var reactions = await ProtoService.GetAllReactionsAsync();
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

        public RelayCommand SendCommand { get; }
        private async void SendExecute()
        {
            if (_chat is not Chat chat || chat.Type is not ChatTypeSupergroup supergroup)
            {
                return;
            }

            var items = _items.Where(x => x.IsSelected).Select(x => new ReactionTypeEmoji(x.Reaction.ReactionValue)).ToList<ReactionType>();
            var available = _available;

            if (supergroup.IsChannel && items.Count == _items.Count)
            {
                available = SupergroupAvailableReactions.All;
            }

            ChatAvailableReactions value = _available switch
            {
                SupergroupAvailableReactions.All => new ChatAvailableReactionsAll(),
                SupergroupAvailableReactions.None => new ChatAvailableReactionsSome(),
                SupergroupAvailableReactions.Some => new ChatAvailableReactionsSome(items),
                _ => null
            };

            if (value == null || value.AreTheSame(chat.AvailableReactions))
            {
                NavigationService.GoBack();
                return;
            }

            var response = await ProtoService.SendAsync(new SetChatAvailableReactions(chat.Id, value));
            NavigationService.GoBack();
        }
    }

    public class SupergroupReactionOption : BindableBase
    {
        public SupergroupReactionOption(Reaction reaction, bool selected)
        {
            Reaction = reaction;
            IsSelected = selected;
        }

        public Reaction Reaction { get; private set; }

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set => Set(ref _isSelected, value);
        }
    }
}
