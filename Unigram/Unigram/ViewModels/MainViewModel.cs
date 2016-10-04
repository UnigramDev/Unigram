using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Aggregator;
using Telegram.Api.Helpers;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Telegram.Api.TL;
using Telegram.Api.TL.Methods.Contacts;
using Unigram.Collections;
using Unigram.Common;
using Unigram.Converters;
using Unigram.Core.Notifications;
using Unigram.Core.Services;
using Windows.ApplicationModel.Background;
using Windows.Globalization.DateTimeFormatting;
using Windows.Networking.PushNotifications;
using Windows.Security.Authentication.Web;
using Windows.Security.Cryptography;
using Windows.Security.Cryptography.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels
{
    public class MainViewModel : UnigramViewModelBase,
        IHandle<TLUpdateNewAuthorization>,
        IHandle
    {
        private readonly IPushService _pushService;

        public MainViewModel(IMTProtoService protoService, ICacheService cacheService, ITelegramEventAggregator aggregator, IPushService pushService)
            : base(protoService, cacheService, aggregator)
        {
            _pushService = pushService;
           
            Dialogs = new DialogCollection(protoService, cacheService);
            SearchDialogs = new ObservableCollection<TLDialog>();
            Contacts = new ContactsViewModel(ProtoService, cacheService, aggregator);

            aggregator.Subscribe(Dialogs);
            aggregator.Subscribe(SearchDialogs);

            aggregator.Subscribe(this);
        }

        public override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            await _pushService.RegisterAsync();
        }

        public ObservableCollection<string> ContactsList = new ObservableCollection<string>();

        public DialogCollection Dialogs { get; private set; }

        public ObservableCollection<TLDialog> SearchDialogs { get; private set; }

        public ContactsViewModel Contacts { get; private set; }

        public void GetSearchDialogs(string query)
        {
            try
            {
                SearchDialogs.Clear();
            }
            catch { }

            foreach (var dialog in this.Dialogs)
            {
                try
                {
                    if (dialog.FullName.ToLower().Contains(query.ToLower()))
                    {
                        SearchDialogs.Add(dialog);
                    }
                }
                catch { }
            }
        }

        #region Handles

        public void Handle(TLUpdateNewAuthorization update)
        {
            var user = CacheService.GetUser(SettingsHelper.UserId) as TLUser;
            var service = CacheService.GetUser(777000);
            if (user == null) return;
            if (service == null) return;

            var messageFormat = "{0},\r\nWe detected a login into your account from a new device on {1}, {2} at {3}\r\n\r\nDevice: {4}\r\nLocation: {5}\r\n\r\nIf this wasn't you, you can go to Settings — Privacy and Security — Terminate all sessions.\r\n\r\nThanks, The Telegram Team";

            var firstName = user.FirstName;
            var dateTime = TLUtils.ToDateTime(update.Date);
            var message = string.Format(messageFormat, firstName, dateTime.ToString("dddd"), dateTime.ToString("M"), dateTime.ToString("t"), update.Device, update.Location);
            var serviceMessage = TLUtils.GetMessage(777000, new TLPeerUser { UserId = SettingsHelper.UserId }, TLMessageState.Confirmed, false, true, update.Date, message, new TLMessageMediaEmpty(), TLLong.Random(), null);
            serviceMessage.Id = 0;

            CacheService.SyncMessage(serviceMessage, x => { });
        }

        #endregion
    }

    public class UsersPanelListItem : TLUser
    {
        public TLUser _parent;
        public UsersPanelListItem(TLUser parent)
        {
            _parent = parent;
        }
        public string fullName { get; internal set; }
        public string lastSeen { get; internal set; }
        public DateTime lastSeenEpoch { get; internal set; }
        public Brush PlaceHolderColor { get; internal set; }
    }
}
