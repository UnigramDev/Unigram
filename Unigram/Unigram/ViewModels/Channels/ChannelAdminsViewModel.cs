using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Aggregator;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Telegram.Api.TL;
using Unigram.Common;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels.Channels
{
    public class ChannelAdminsViewModel : ChannelParticipantsViewModelBase
    {
        public ChannelAdminsViewModel(IMTProtoService protoService, ICacheService cacheService, ITelegramEventAggregator aggregator) 
            : base(protoService, cacheService, aggregator, new TLChannelParticipantsAdmins(), null)
        {
            ParticipantDismissCommand = new RelayCommand<TLChannelParticipantBase>(ParticipantDismissExecute);
        }

        public bool CanEditDemocracy
        {
            get
            {
                return _item != null && _item.IsCreator && _item.IsMegaGroup;
            }
        }

        private bool _isDemocracy;
        public bool IsDemocracy
        {
            get
            {
                return _isDemocracy;
            }
            set
            {
                Set(ref _isDemocracy, value);
            }
        }

        #region Context menu

        public RelayCommand<TLChannelParticipantBase> ParticipantDismissCommand { get; }
        private async void ParticipantDismissExecute(TLChannelParticipantBase participant)
        {
            if (_item == null)
            {
                return;
            }

            if (participant.User == null)
            {
                return;
            }

            var rights = new TLChannelAdminRights();

            var response = await ProtoService.EditAdminAsync(_item, participant.User.ToInputUser(), rights);
            if (response.IsSucceeded)
            {
                Participants.Remove(participant);
            }
        }

        #endregion

        public override void RaisePropertyChanged([CallerMemberName] string propertyName = null)
        {
            base.RaisePropertyChanged(propertyName);

            if (propertyName.Equals(nameof(Item)) && _item != null)
            {
                IsDemocracy = _item.IsDemocracy;
                RaisePropertyChanged(() => CanEditDemocracy);
            }
            else if (propertyName.Equals(nameof(IsDemocracy)))
            {
                ProtoService.ToggleInvitesAsync(_item.ToInputChannel(), _isDemocracy, null);
            }
        }
    }
}
