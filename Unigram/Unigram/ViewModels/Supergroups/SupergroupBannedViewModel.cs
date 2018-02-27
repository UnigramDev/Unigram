using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TdWindows;
using Unigram.Common;
using Unigram.Services;
using Unigram.Views.Supergroups;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels.Supergroups
{
    public class SupergroupBannedViewModel : SupergroupMembersViewModelBase
    {
        public SupergroupBannedViewModel(IProtoService protoService, ICacheService cacheService, IEventAggregator aggregator) 
            : base(protoService, cacheService, aggregator, null, query => new SupergroupMembersFilterBanned(query))
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
        }

        #endregion
    }
}
