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
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels.Channels
{
    public class ChannelAdminsViewModel : ChannelParticipantsViewModelBase
    {
        public ChannelAdminsViewModel(IMTProtoService protoService, ICacheService cacheService, ITelegramEventAggregator aggregator) 
            : base(protoService, cacheService, aggregator, new TLChannelParticipantsAdmins())
        {
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
