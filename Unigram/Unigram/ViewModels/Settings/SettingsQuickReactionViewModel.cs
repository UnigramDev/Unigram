using System.Linq;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Collections;
using Unigram.Common;
using Unigram.Navigation;
using Unigram.Navigation.Services;
using Unigram.Services;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels.Settings
{
    public class SettingsQuickReactionViewModel : TLViewModelBase
    {
        public SettingsQuickReactionViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
            Items = new MvxObservableCollection<SettingsReactionOption>();

            SendCommand = new RelayCommand(SendExecute);
        }

        public bool IsQuickReplySelected
        {
            get => Settings.Appearance.IsQuickReplySelected;
            set
            {
                Settings.Appearance.IsQuickReplySelected = value;
                RaisePropertyChanged();
            }
        }

        public MvxObservableCollection<SettingsReactionOption> Items { get; private set; }

        protected override Task OnNavigatedToAsync(object parameter, NavigationMode mode, NavigationState state)
        {
            //if (ClientService.IsPremium)
            //{
            //    Items.ReplaceWith(ClientService.Reactions.Where(x => x.Value.IsActive).Select(x => new SettingsReactionOption(this, x.Value, x.Key == ClientService.Options.DefaultReaction && !IsQuickReplySelected)));
            //}
            //else
            //{
            //    Items.ReplaceWith(ClientService.Reactions.Where(x => x.Value.IsActive && !x.Value.IsPremium).Select(x => new SettingsReactionOption(this, x.Value, x.Key == ClientService.Options.DefaultReaction && !IsQuickReplySelected)));
            //}

            return Task.CompletedTask;
        }

        public RelayCommand SendCommand { get; }
        private void SendExecute()
        {
            NavigationService.GoBack();
        }
    }

    public class SettingsReactionOption : BindableBase
    {
        private readonly SettingsQuickReactionViewModel _parent;

        public SettingsReactionOption(SettingsQuickReactionViewModel parent, Reaction reaction, bool selected)
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
                if (Set(ref _isSelected, value) && value)
                {
                    //_parent.ClientService.Options.DefaultReaction = Reaction.ReactionValue;
                }
            }
        }
    }
}
