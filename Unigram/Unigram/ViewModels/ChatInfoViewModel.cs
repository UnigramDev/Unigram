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
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels
{
    public class ChatInfoViewModel : UnigramViewModelBase
    {
        public ObservableCollection<UsersPanelListItem> UsersList = new ObservableCollection<UsersPanelListItem>();
        public ObservableCollection<UsersPanelListItem> TempList = new ObservableCollection<UsersPanelListItem>();
        public object photo;
        public string FullNameField { get; internal set; }
        public string Status { get; internal set; }
        public ChatInfoViewModel(IMTProtoService protoService, ICacheService cacheService, ITelegramEventAggregator aggregator) : base(protoService, cacheService, aggregator)
        {
        }
        public override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            FullNameField = "Hello!";
            var channel = parameter as TLInputPeerChannel;
            var chat = parameter as TLInputPeerChat;
            if (channel != null)
            {
                TLInputChannel x = new TLInputChannel();                
                x.ChannelId = channel.ChannelId;
                x.AccessHash = channel.AccessHash;
                var channelDetails = await ProtoService.GetFullChannelAsync(x);
                FullNameField = channelDetails.Value.Chats[0].FullName;
                Status = ((TLChannelFull)channelDetails.Value.FullChat).About;
                photo = channelDetails.Value.Chats[0].Photo;
            }
            if (chat != null)
            {
                var chatDetails = await ProtoService.GetFullChatAsync(chat.ChatId);
                FullNameField = chatDetails.Value.Chats[0].FullName;
            }
            TempList.Clear();
            UsersList.Clear();
            getMembers(channel, chat);
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
                foreach (var item in participants.Value.Users)
                {
                    var User = item as TLUser;
                    var TempX = new UsersPanelListItem(User);
                    var Status = LastSeenHelper.GetLastSeen(User);
                    TempX.fullName = User.FullName;
                    TempX.lastSeen = Status.Item1;
                    TempX.Photo = TempX._parent.Photo;
                    TempList.Add(TempX);
                }
            }

            if (chat != null)
            {
                //set visibility
                var chatDetails = await ProtoService.GetFullChatAsync(chat.ChatId);
                foreach (var item in chatDetails.Value.Users)
                {
                    var User = item as TLUser;
                    var TempX = new UsersPanelListItem(User);
                    var Status = LastSeenHelper.GetLastSeen(User);
                    TempX.fullName = User.FullName;
                    TempX.lastSeen = Status.Item1;
                    TempX.Photo = TempX._parent.Photo;
                    TempList.Add(TempX);
                }
            }

            foreach (var item in TempList.OrderByDescending(person => person.lastSeen))
            {
                UsersList.Add(item);
            }
        }
    }
}
