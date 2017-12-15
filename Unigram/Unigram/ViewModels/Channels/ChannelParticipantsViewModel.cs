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
using Unigram.Views.Channels;

namespace Unigram.ViewModels.Channels
{
    public class ChannelParticipantsViewModel : ChannelParticipantsViewModelBase
    {
        public ChannelParticipantsViewModel(IMTProtoService protoService, ICacheService cacheService, ITelegramEventAggregator aggregator)
            : base(protoService, cacheService, aggregator, new TLChannelParticipantsRecent(), query => new TLChannelParticipantsSearch { Q = query })
        {
            ParticipantPromoteCommand = new RelayCommand<TLChannelParticipantBase>(ParticipantPromoteExecute);
            ParticipantRemoveCommand = new RelayCommand<TLChannelParticipantBase>(ParticipantRemoveExecute);
        }

        #region Context menu

        public RelayCommand<TLChannelParticipantBase> ParticipantPromoteCommand { get; }
        private void ParticipantPromoteExecute(TLChannelParticipantBase participant)
        {
            if (_item == null)
            {
                return;
            }

            NavigationService.Navigate(typeof(ChannelAdminRightsPage), TLTuple.Create(_item.ToPeer(), participant));
        }

        public RelayCommand<TLChannelParticipantBase> ParticipantRemoveCommand { get; }
        private async void ParticipantRemoveExecute(TLChannelParticipantBase participant)
        {
            if (_item == null)
            {
                return;
            }

            if (participant.User == null)
            {
                return;
            }

            var rights = new TLChannelBannedRights { IsEmbedLinks = true, IsSendGames = true, IsSendGifs = true, IsSendInline = true, IsSendMedia = true, IsSendMessages = true, IsSendStickers = true, IsViewMessages = true };

            var response = await ProtoService.EditBannedAsync(_item, participant.User.ToInputUser(), rights);
            if (response.IsSucceeded)
            {
                Participants.Remove(participant);
            }
        }

        #endregion
    }
}
