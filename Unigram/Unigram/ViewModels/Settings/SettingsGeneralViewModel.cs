using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Aggregator;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Telegram.Api.TL;
using Unigram.Common;
using Unigram.Core.Services;

namespace Unigram.ViewModels.Settings
{
    public class SettingsGeneralViewModel : UnigramViewModelBase
    {
        private IContactsService _contactsService;

        public SettingsGeneralViewModel(IMTProtoService protoService, ICacheService cacheService, ITelegramEventAggregator aggregator, IContactsService contactsService)
            : base(protoService, cacheService, aggregator)
        {
            _contactsService = contactsService;
        }

        public bool IsSendByEnterEnabled
        {
            get
            {
                return ApplicationSettings.Current.IsSendByEnterEnabled;
            }
            set
            {
                ApplicationSettings.Current.IsSendByEnterEnabled = value;
                RaisePropertyChanged();
            }
        }

        public bool IsReplaceEmojiEnabled
        {
            get
            {
                return ApplicationSettings.Current.IsReplaceEmojiEnabled;
            }
            set
            {
                ApplicationSettings.Current.IsReplaceEmojiEnabled = value;
                RaisePropertyChanged();
            }
        }

        public bool IsContactsSyncEnabled
        {
            get
            {
                return ApplicationSettings.Current.IsContactsSyncEnabled;
            }
            set
            {
                ApplicationSettings.Current.IsContactsSyncEnabled = value;
                RaisePropertyChanged();
            }
        }

        public override async void RaisePropertyChanged([CallerMemberName] string propertyName = null)
        {
            base.RaisePropertyChanged(propertyName);

            if (propertyName.Equals(nameof(IsContactsSyncEnabled)))
            {
                if (IsContactsSyncEnabled)
                {
                    var contacts = CacheService.GetContacts();
                    var response = new TLContactsContacts { Users = new TLVector<TLUserBase>(contacts) };

                    await _contactsService.SyncContactsAsync(response);
                }
                else
                {
                    await _contactsService.UnsyncContactsAsync();
                }
            }
        }
    }
}
