using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Aggregator;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Telegram.Api.TL;
using Unigram.Common;
using Unigram.Converters;
using Unigram.Views;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels.Chats
{
    public class ChatDetailsViewModel : UnigramViewModelBase
    {
        public ObservableCollection<TLUser> UsersList = new ObservableCollection<TLUser>();
        public ObservableCollection<TLUser> TempList = new ObservableCollection<TLUser>();
        public object photo;
        public string Status { get; internal set; }
        public ChatDetailsViewModel(IMTProtoService protoService, ICacheService cacheService, ITelegramEventAggregator aggregator) : base(protoService, cacheService, aggregator)
        {
        }

        public override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            var channel = parameter as TLPeerChannel;
            var chat = parameter as TLPeerChat;

            Item = CacheService.GetChat(chat?.ChatId ?? channel?.ChannelId);

            //if (channel != null)
            //{
            //    TLInputChannel x = new TLInputChannel();                
            //    x.ChannelId = channel.ChannelId;
            //    x.AccessHash = channel.AccessHash;
            //    var channelDetails = await ProtoService.GetFullChannelAsync(x);
            //    Status = ((TLChannelFull)channelDetails.Result.FullChat).About;
            //    // TODO: photo = channelDetails.Value.Chats[0].Photo;
            //}
            //if (chat != null)
            //{
            //    var chatDetails = await ProtoService.GetFullChatAsync(chat.ChatId);
            //}
            //TempList.Clear();
            //UsersList.Clear();
            //getMembers(channel, chat);
        }

        private TLChatBase _item;
        public TLChatBase Item
        {
            get
            {
                return _item;
            }
            set
            {
                Set(ref _item, value);
            }
        }

        public async Task getMembers(TLInputPeerChannel channel, TLInputPeerChat chat)
        {

            if (channel != null)
            {
                //set visibility
                TLInputChannel x = new TLInputChannel();
                x.ChannelId = channel.ChannelId;
                x.AccessHash = channel.AccessHash;
                var participants = await ProtoService.GetParticipantsAsync(x, null, 0, int.MaxValue);
                foreach (var item in participants.Result.Users)
                {
                    var User = item as TLUser;
                    //var TempX = new UsersPanelListItem(User);
                    //var Status = LastSeenHelper.GetLastSeen(User);
                    //TempX.fullName = User.FullName;
                    //TempX.lastSeen = Status.Item1;
                    //TempX.Photo = TempX._parent.Photo;
                    TempList.Add(User);
                }
            }

            if (chat != null)
            {
                //set visibility
                var chatDetails = await ProtoService.GetFullChatAsync(chat.ChatId);
                foreach (var item in chatDetails.Result.Users)
                {
                    var User = item as TLUser;
                    //var TempX = new UsersPanelListItem(User);
                    //var Status = LastSeenHelper.GetLastSeen(User);
                    //TempX.fullName = User.FullName;
                    //TempX.lastSeen = Status.Item1;
                    //TempX.Photo = TempX._parent.Photo;
                    TempList.Add(User);
                }
            }

            //foreach (var item in TempList.OrderByDescending(person => person.lastSeen))
            //{
            //    UsersList.Add(item);
            //}
        }

        public RelayCommand MediaCommand => new RelayCommand(MediaExecute);
        private void MediaExecute()
        {
            var channel = Item as TLChannel;
            if (channel != null)
            {
                NavigationService.Navigate(typeof(DialogSharedMediaPage), new TLInputPeerChannel { ChannelId = channel.Id, AccessHash = channel.AccessHash.Value });
            }

            var chat = Item as TLChat;
            if (chat != null)
            {
                NavigationService.Navigate(typeof(DialogSharedMediaPage), new TLInputPeerChat { ChatId = chat.Id });
            }
        }
    }
}
