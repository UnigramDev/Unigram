using System;
using System.Collections.Generic;
using System.Linq;
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
    public class ChannelKickedViewModel : ChannelParticipantsViewModelBase
    {
        public ChannelKickedViewModel(IMTProtoService protoService, ICacheService cacheService, ITelegramEventAggregator aggregator) 
            : base(protoService, cacheService, aggregator, null, query => new TLChannelParticipantsKicked { Q = query })
        {
            ParticipantDismissCommand = new RelayCommand<TLChannelParticipantBase>(ParticipantDismissExecute);
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

            var rights = new TLChannelBannedRights();

            var response = await ProtoService.EditBannedAsync(_item, participant.User.ToInputUser(), rights);
            if (response.IsSucceeded)
            {
                Participants.Remove(participant);
            }
        }

        #endregion
    }
}
