using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Services;
using Unigram.Views;
using Unigram.Views.Supergroups;

namespace Unigram.ViewModels.Supergroups
{
    public class SupergroupAdministratorsViewModel : SupergroupMembersViewModelBase, IHandle
    {
        public SupergroupAdministratorsViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator, new SupergroupMembersFilterAdministrators(), query => new SupergroupMembersFilterAdministrators())
        {
            EventLogCommand = new RelayCommand(EventLogExecute);
            AddCommand = new RelayCommand(AddExecute);
            ParticipantDismissCommand = new RelayCommand<ChatMember>(ParticipantDismissExecute);
        }

        public override void Subscribe()
        {
            Aggregator.Subscribe<UpdateSupergroupFullInfo>(this, Handle);
        }

        public async void ToggleAntiSpam()
        {
            if (Chat.Type is ChatTypeSupergroup supergroup && ClientService.TryGetSupergroupFull(Chat, out SupergroupFullInfo supergroupFull))
            {
                if (supergroupFull.MemberCount >= ClientService.Options.AggressiveAntiSpamSupergroupMemberCountMin || supergroupFull.IsAggressiveAntiSpamEnabled)
                {
                    ClientService.Send(new ToggleSupergroupIsAggressiveAntiSpamEnabled(supergroup.SupergroupId, !supergroupFull.IsAggressiveAntiSpamEnabled));
                }
                else
                {
                    var message = Locale.Declension("ChannelAntiSpamForbidden", ClientService.Options.AggressiveAntiSpamSupergroupMemberCountMin);
                    await MessagePopup.ShowAsync(message, Strings.Resources.AppName, Strings.Resources.OK);
                }
            }
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
