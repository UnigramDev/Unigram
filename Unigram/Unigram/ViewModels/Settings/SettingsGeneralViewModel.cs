using Unigram.Common;
using Unigram.Services;
using Unigram.Services.Updates;

namespace Unigram.ViewModels.Settings
{
    public class SettingsGeneralViewModel : TLViewModelBase
    {
        private readonly IContactsService _contactsService;

        public SettingsGeneralViewModel(IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator, IContactsService contactsService)
            : base(protoService, cacheService, settingsService, aggregator)
        {
            _contactsService = contactsService;
        }

        public bool IsSendByEnterEnabled
        {
            get
            {
                return Settings.IsSendByEnterEnabled;
            }
            set
            {
                Settings.IsSendByEnterEnabled = value;
                RaisePropertyChanged();
            }
        }

        public bool IsReplaceEmojiEnabled
        {
            get
            {
                return Settings.IsReplaceEmojiEnabled;
            }
            set
            {
                Settings.IsReplaceEmojiEnabled = value;
                RaisePropertyChanged();
            }
        }

        public bool IsAutoPlayEnabled
        {
            get
            {
                return Settings.IsAutoPlayEnabled;
            }
            set
            {
                Settings.IsAutoPlayEnabled = value;
                RaisePropertyChanged();
            }
        }

        public bool IsWorkModeVisible
        {
            get
            {
                return Settings.IsWorkModeVisible;
            }
            set
            {
                Settings.IsWorkModeVisible = value;
                RaisePropertyChanged();

                if (!value)
                {
                    Settings.IsWorkModeEnabled = false;
                }

                Aggregator.Publish(new UpdateWorkMode(value, Settings.IsWorkModeEnabled));
            }
        }
    }
}
