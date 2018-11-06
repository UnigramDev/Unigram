using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Collections;
using Unigram.Common;
using Unigram.Services;
using Unigram.Services.Updates;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels.Settings
{
    public class SettingsAdvancedViewModel : TLViewModelBase, IHandle<UpdateChatIsPinned>
    {
        public SettingsAdvancedViewModel(IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(protoService, cacheService, settingsService, aggregator)
        {
            PinnedChats = new MvxObservableCollection<Chat>();
        }

        public override Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            PinnedChats.ReplaceWith(CacheService.GetPinnedChats());
            Aggregator.Subscribe(this);

            return base.OnNavigatedToAsync(parameter, mode, state);
        }

        public override Task OnNavigatedFromAsync(IDictionary<string, object> pageState, bool suspending)
        {
            Aggregator.Unsubscribe(this);
            return base.OnNavigatedFromAsync(pageState, suspending);
        }

        public MvxObservableCollection<Chat> PinnedChats { get; private set; }

        public void SetPinnedChats()
        {
            ProtoService.Send(new SetPinnedChats(PinnedChats.Select(x => x.Id).ToArray()));
        }

        public void Handle(UpdateChatIsPinned update)
        {
            PinnedChats.ReplaceWith(CacheService.GetPinnedChats());
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

        public bool IsAdaptiveWideEnabled
        {
            get
            {
                return Settings.IsAdaptiveWideEnabled;
            }
            set
            {
                Settings.IsAdaptiveWideEnabled = value;
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
