using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Services;
using Unigram.Views;
using Unigram.Views.Supergroups;

namespace Unigram.ViewModels.Supergroups
{
    public class SupergroupAdministratorsViewModel : SupergroupMembersViewModelBase
    {
        public SupergroupAdministratorsViewModel(IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(protoService, cacheService, settingsService, aggregator, new SupergroupMembersFilterAdministrators(), null)
        {
            EventLogCommand = new RelayCommand(EventLogExecute);
            AddCommand = new RelayCommand(AddExecute);
            ParticipantDismissCommand = new RelayCommand<ChatMember>(ParticipantDismissExecute);
        }

        public RelayCommand EventLogCommand { get; }
        private void EventLogExecute()
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            NavigationService.Navigate(typeof(ChatEventLogPage), chat.Id);
        }

        public RelayCommand AddCommand { get; }
        private void AddExecute()
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            NavigationService.Navigate(typeof(SupergroupAddAdministratorPage), chat.Id);
        }

        #region Context menu

        public RelayCommand<ChatMember> ParticipantDismissCommand { get; }
        private async void ParticipantDismissExecute(ChatMember participant)
        {
        }

        #endregion
    }
}
