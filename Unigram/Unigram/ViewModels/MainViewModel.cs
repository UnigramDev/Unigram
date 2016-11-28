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
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels
{
    public class MainViewModel : UnigramViewModelBase
    {
        private readonly IPushService _pushService;

        public MainViewModel(IMTProtoService protoService, ICacheService cacheService, ITelegramEventAggregator aggregator, IPushService pushService)
            : base(protoService, cacheService, aggregator)
        {
            _pushService = pushService;

            //Dialogs = new DialogCollection(protoService, cacheService);
            SearchResults = new ObservableCollection<SearchResult>();
            SearchDialogs = new ObservableCollection<TLDialog>();
            Dialogs = new DialogsViewModel(ProtoService, cacheService, aggregator);
            Contacts = new ContactsViewModel(ProtoService, cacheService, aggregator);
            aggregator.Subscribe(Dialogs);
            aggregator.Subscribe(SearchDialogs);
        }

        public override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            await _pushService.RegisterAsync();
        }

        public ObservableCollection<string> ContactsList = new ObservableCollection<string>();

        //EXPERIMENTAL
        public ObservableCollection<SearchResult> SearchResults { get; set; }

        //END OF EXPERIMENTS
        //public DialogCollection Dialogs { get; private set; }

        public ObservableCollection<TLDialog> SearchDialogs { get; private set; }

        public DialogsViewModel Dialogs { get; private set; }

        public ContactsViewModel Contacts { get; private set; }

        public void GetSearchDialogs(string query)
        {
            try
            {
               // SearchDialogs.Clear();
                SearchResults.Clear();
                searchUsers(query);
                searchMessages(query);
            }
            catch { }

            foreach (var dialog in this.Dialogs.Items)
            {
                try
                {
                    if (dialog.FullName.ToLower().Contains(query.ToLower()))
                    {
                      //  SearchDialogs.Add(dialog);
                        SearchResults.Add(new SearchResult(dialog, SearchResult.ResultType.Local));
                    }
                    
                }
                catch { }
            }
        }

        public async void searchMessages (string query)
        {
            if (query.Length > 1)
            {
                var result = await ProtoService.SearchGlobalAsync(query, 0, null, 0, 20);
                var messages = result.Value.Messages;
                foreach (var item in messages)
                {
                    TLUser userX = new TLUser();
                    bool isOut;
                    var message = item as TLMessage;
                    if(message!=null)
                    {
                        var peerUser = item.ToId as TLPeerUser;
                        if (peerUser != null)
                        {
                            isOut = message.IsOut;
                            if (isOut == false)
                                userX = message.From;
                            else
                            {
                                userX = CacheService.GetUser(message.ToId.Id) as TLUser; 
                                //userX =result.Value.Users.FirstOrDefault(s => s.Id == message.ToId.Id) as TLUser;
                            }
                            SearchResults.Add(new SearchResult(null, SearchResult.ResultType.Message, userX, null, item, isOut));
                            continue;
                        }
                        var peerChat = item.ToId as TLPeerChat;                        
                        if(peerChat!=null)
                        {
                            isOut = message.IsOut;
                            if (isOut == false)
                                userX = CacheService.GetUser(message.FromId) as TLUser; //FromId because most of the time From.Id is null.
                            else
                                userX = CacheService.GetUser(SettingsHelper.UserId) as TLUser;
                            // userX = result.Value.Users.FirstOrDefault(s => s.Id == SettingsHelper.UserId) as TLUser; 
                            //var chatItem = result.Value.Chats.FirstOrDefault(s => s.Id == item.ToId.Id);
                            var chatItem = CacheService.GetChats().FirstOrDefault(s => s.Id == item.ToId.Id);
                            SearchResults.Add(new SearchResult(null, SearchResult.ResultType.Message, userX, chatItem, item, isOut));
                        }
                        //CHANNEL RESULTS BELOW
                        var peerChannel = item.ToId as TLPeerChannel;
                        if (peerChannel != null)
                        {
                            isOut = message.IsOut;
                            if (isOut == false)
                            {
                                userX = CacheService.GetUser(message.FromId) as TLUser;
                                //if(userX==null)
                                //userX = (await ProtoService.GetUsersAsync(new TLVector<TLInputUserBase> { new TLInputUser { UserId = message.FromId.GetValueOrDefault() } })).Value[0] as TLUser;
                            }
                            else
                                userX = CacheService.GetUser(SettingsHelper.UserId) as TLUser;
                            var channelItem = CacheService.GetChats().FirstOrDefault(s => s.Id == item.ToId.Id);
                            SearchResults.Add(new SearchResult(null, SearchResult.ResultType.Message, userX, channelItem, item, isOut));
                        }
                    }
                }
            }
        }

        public async void searchUsers(string query)
        {
            if (query.Length > 4)
            {
                var result = await ProtoService.SearchAsync(query, 20);
                var users = result.Value.Users;
                foreach (var item in users)
                {
                    SearchResults.Add(new SearchResult(null, SearchResult.ResultType.GlobalUsers, item));
                }
                var chats = result.Value.Chats;
                foreach(var item in chats)
                {
                    SearchResults.Add(new SearchResult(null, SearchResult.ResultType.GlobalChats, null, item));
                }
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
        public DateTime lastSeenEpoch { get; internal set; }
        public Brush PlaceHolderColor { get; internal set; }
    }

    public class SearchResult
    {
        public TLDialog Dialog;
        public string Header { get; internal set; }
        public string SubHeader { get; internal set; }
        public object Photo { get; internal set; }
        public ResultType Type { get; internal set; }
        public enum ResultType
        {
            Local,
            Message,
            GlobalUsers,
            GlobalChats
        }
        public SearchResult(TLDialog dialog, ResultType type, TLUserBase userBase = null, TLChatBase chatBase = null,TLMessageBase messageBase=null, bool isOut=false)
        {
            Type = type;
            switch (type)
            {
                case ResultType.Local:                    
                    Dialog = dialog;
                    Header = dialog.FullName;                        
                    //Photo =((TLUser)dialog.With).Photo;
                    Photo =((TLUser)dialog.With).Photo;
                    SubHeader = ((TLMessage)dialog.TopMessageItem).Message;
                break;
                case ResultType.GlobalUsers:
                    Header = userBase.FullName;
                    //Photo = (BitmapImage)DefaultPhotoConverter.Convert(chatBase.Photo);
                    Photo = ((TLUser)userBase).Photo;
                    break;
                case ResultType.GlobalChats:
                    Header = chatBase.FullName;
                    Photo =chatBase.Photo;
                    break;
                case ResultType.Message:
                    if (chatBase != null)
                    {
                        Header = chatBase.FullName;
                        //Photo=(BitmapImage)DefaultPhotoConverter.Convert()
                        Photo =chatBase.Photo;
                        SubHeader = userBase==null?"null" : userBase.FullName + ": " + ((TLMessage)messageBase).Message;
                    }                
                    else
                    {
                        Header = userBase.FullName;
                        Photo = ((TLUser)userBase).Photo;
                        SubHeader = ((TLMessage)messageBase).From.FirstName + ": " + ((TLMessage)messageBase).Message;
                    }
                    break;
        }
        }
    }

}
