using System.Linq;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Collections;
using Unigram.Common;
using Unigram.Navigation;
using Unigram.Navigation.Services;
using Unigram.Services;
using Unigram.ViewModels.Delegates;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels.Supergroups
{
    public class SupergroupReactionsViewModel : TLViewModelBase, IDelegable<IChatDelegate>
    {
        public IChatDelegate Delegate { get; set; }

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

        public bool? AreEnabled
        {
            get
            {
                var count = Items.Count(x => x.IsSelected);
                if (count == Items.Count)
                {
                    return true;
                }

                return count == 0 ? false : null;
            }
            set
            {
                if (value != AreEnabled)
                {
                    Items.ForEach(x => x.IsSelected = value is true);
                    RaisePropertyChanged();
                }
            }
        }

        public MvxObservableCollection<SupergroupReactionOption> Items { get; private set; }

        public override Task OnNavigatedToAsync(object parameter, NavigationMode mode, NavigationState state)
        {
            var chatId = (long)parameter;

            Chat = ProtoService.GetChat(chatId);

            var chat = _chat;
            if (chat == null)
            {
                return Task.CompletedTask;
            }

            Delegate?.UpdateChat(chat);

            Items.ReplaceWith(CacheService.Reactions.Where(x => x.Value.IsActive).Select(x => new SupergroupReactionOption(this, x.Value, chat.AvailableReactions.Contains(x.Key))));
            return Task.CompletedTask;
        }

        public RelayCommand SendCommand { get; }
        private async void SendExecute()
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            var available = Items.Where(x => x.IsSelected).Select(x => x.Reaction.ReactionValue).ToArray();
            if (available.OrderBy(x => x).SequenceEqual(chat.AvailableReactions.OrderBy(x => x)))
            {
                NavigationService.GoBack();
                return;
            }

            var response = await ProtoService.SendAsync(new SetChatAvailableReactions(chat.Id, available));
            NavigationService.GoBack();
        }
    }

    public class SupergroupReactionOption : BindableBase
    {
        private readonly SupergroupReactionsViewModel _parent;

        public SupergroupReactionOption(SupergroupReactionsViewModel parent, Reaction reaction, bool selected)
        {
            _parent = parent;

            Reaction = reaction;
            IsSelected = selected;
        }

        public Reaction Reaction { get; private set; }

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (Set(ref _isSelected, value))
                {
                    _parent.RaisePropertyChanged(nameof(_parent.AreEnabled));
                }
            }
        }
    }
}
