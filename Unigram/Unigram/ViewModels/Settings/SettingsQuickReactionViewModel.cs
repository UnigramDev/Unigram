using Unigram.Services;

namespace Unigram.ViewModels.Settings
{
    public class SettingsQuickReactionViewModel : TLViewModelBase
    {
        public SettingsQuickReactionViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
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
    }
}
