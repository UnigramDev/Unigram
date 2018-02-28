using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TdWindows;
using Unigram.Common;
using Unigram.Services;
using Unigram.Views.Supergroups;

namespace Unigram.ViewModels.Supergroups
{
    public class SupergroupRestrictedViewModel : SupergroupMembersViewModelBase
    {
        public SupergroupRestrictedViewModel(IProtoService protoService, ICacheService cacheService, IEventAggregator aggregator) 
            : base(protoService, cacheService, aggregator, null, query => new SupergroupMembersFilterRestricted(query))
        {
            AddCommand = new RelayCommand(AddExecute);
            ParticipantDismissCommand = new RelayCommand<ChatMember>(ParticipantDismissExecute);
        }

        public RelayCommand AddCommand { get; }
        private void AddExecute()
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            NavigationService.Navigate(typeof(SupergroupAddRestrictedPage), chat.Id);
        }

        #region Context menu

        public RelayCommand<ChatMember> ParticipantDismissCommand { get; }
        private async void ParticipantDismissExecute(ChatMember participant)
        {
            //if (_item == null)
            //{
            //    return;
            //}

            //if (participant.User == null)
            //{
            //    return;
            //}

            //var rights = new TLChannelBannedRights();

            //var response = await LegacyService.EditBannedAsync(_item, participant.User.ToInputUser(), rights);
            //if (response.IsSucceeded)
            //{
            //    Participants.Remove(participant);
            //}
        }

        #endregion
    }
}
