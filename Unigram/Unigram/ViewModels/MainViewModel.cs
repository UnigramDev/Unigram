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
using Unigram.Core.Notifications;
using Unigram.Core.Services;
using Windows.ApplicationModel.Background;
using Windows.Globalization.DateTimeFormatting;
using Windows.Networking.PushNotifications;
using Windows.Security.Authentication.Web;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels
{
    public class MainViewModel : UnigramViewModelBase
    {
        private readonly IPushService _pushService;        
        public ObservableCollection<UsersPanelListItem> TempList = new ObservableCollection<UsersPanelListItem>();
        public ObservableCollection<UsersPanelListItem> UsersList = new ObservableCollection<UsersPanelListItem>();
        public MainViewModel(IMTProtoService protoService, ICacheService cacheService, ITelegramEventAggregator aggregator, IPushService pushService)
            : base(protoService, cacheService, aggregator)
        {
            _pushService = pushService;
           
            Dialogs = new DialogCollection(protoService, cacheService);
            SearchDialogs = new DialogCollection(protoService, cacheService);
            aggregator.Subscribe(Dialogs);
            aggregator.Subscribe(SearchDialogs);
        }

        public override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            await _pushService.RegisterAsync();
        }

        public async void getTLContacts()
        {
            var UserList = await ProtoService.GetContactsAsync("");
            var x = UserList.Value as TLContactsContacts;            
            foreach (var item in x.Users)
            {                
                string status = "";
                var User = item as TLUser;
                UsersPanelListItem TempX = new UsersPanelListItem(User as TLUser);
                var sOfflineCheck = User.Status as TLUserStatusOffline;
                if (sOfflineCheck != null)
                {
                    var seen = TLUtils.ToDateTime(sOfflineCheck.WasOnline);
                    var now = DateTime.Now;
                    string t;
                    //if date=date, show hours else show full string
                    //if hours=hours, show minutes else show "hours ago"
                    t = (now.Date == seen.Date) ? ((now - seen).Hours < 1 ? ((now - seen).Minutes < 1 ? " moments ago" : (now - seen).Minutes.ToString() + " minutes ago") : ((now - seen).Hours.ToString()) + " hours ago") : now.Date - seen.Date == new TimeSpan(24,0,0) ? "yesterday "+ new DateTimeFormatter("shorttime").Format(seen): seen.ToString();
                    status = "Last seen " + t;
                    TempX.lastSeenEpoch = sOfflineCheck.WasOnline;
                }
                var sOnlineCheck = User.Status as TLUserStatusOnline;
                if (sOnlineCheck != null)
                {
                    status = "Online";
                    TempX.lastSeenEpoch = int.MaxValue;
                }
                if (status == "")
                {
                    status = "Last seen recently";
                    TempX.lastSeenEpoch = 0;
                }                
                TempX.fullName = User.FullName;
                TempX.lastSeen = status;                
                TempX.Photo = TempX._parent.Photo;
                TempList.Add(TempX);
            }
            //Super Inefficient Method below to sort alphabetically, TODO: FIX IT
            TempList = new ObservableCollection<ViewModels.UsersPanelListItem>(TempList.OrderByDescending(person => person.lastSeenEpoch));
            foreach (var item in TempList)
            {
                UsersList.Add(item);
            }
        }

        public ObservableCollection<string> ContactsList = new ObservableCollection<string>();

        public DialogCollection Dialogs { get; private set; }

        public DialogCollection SearchDialogs { get; private set; }

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
        public int lastSeenEpoch { get; internal set; }
    }
}
