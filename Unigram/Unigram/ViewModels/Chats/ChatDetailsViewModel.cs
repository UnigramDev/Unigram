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
using Unigram.Collections;
using Unigram.Common;
using Unigram.Converters;
using Unigram.Views;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels.Chats
{
    public class ChatDetailsViewModel : UnigramViewModelBase
    {
        public ChatDetailsViewModel(IMTProtoService protoService, ICacheService cacheService, ITelegramEventAggregator aggregator) 
            : base(protoService, cacheService, aggregator)
        {
        }

        private TLChat _item;
        public TLChat Item
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

        private TLChatFull _full;
        public TLChatFull Full
        {
            get
            {
                return _full;
            }
            set
            {
                Set(ref _full, value);
            }
        }

        public override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            var chat = parameter as TLChat;
            var peer = parameter as TLPeerChat;
            if (peer != null)
            {
                chat = CacheService.GetChat(peer.ChatId) as TLChat;
            }

            if (chat != null)
            {
                Item = chat;

                var response = await ProtoService.GetFullChatAsync(chat.Id);
                if (response.IsSucceeded)
                {
                    var collection = new SortedObservableCollection<TLChatParticipantBase>(new TLChatParticipantBaseComparer(true));
                    Full = response.Result.FullChat as TLChatFull;
                    Participants = collection;

                    RaisePropertyChanged(() => Participants);

                    if (_full.Participants is TLChatParticipants participants)
                    {
                        collection.AddRange(participants.Participants, true);
                    }
                }
            }
        }

        public SortedObservableCollection<TLChatParticipantBase> Participants { get; private set; }

        public RelayCommand MediaCommand => new RelayCommand(MediaExecute);
        private void MediaExecute()
        {
            var chat = Item as TLChat;
            if (chat != null)
            {
                NavigationService.Navigate(typeof(DialogSharedMediaPage), new TLInputPeerChat { ChatId = chat.Id });
            }
        }
    }

    public class TLChatParticipantBaseComparer : IComparer<TLChatParticipantBase>
    {
        private bool _epoch;

        public TLChatParticipantBaseComparer(bool epoch)
        {
            _epoch = epoch;
        }

        public int Compare(TLChatParticipantBase x, TLChatParticipantBase y)
        {
            var xUser = x.User;
            var yUser = y.User;

            if (xUser == null || yUser == null)
            {
                return -1;
            }

            if (_epoch)
            {
                var epoch = LastSeenConverter.GetIndex(yUser).CompareTo(LastSeenConverter.GetIndex(xUser));
                if (epoch == 0)
                {
                    var fullName = xUser.FullName.CompareTo(yUser.FullName);
                    if (fullName == 0)
                    {
                        return yUser.Id.CompareTo(xUser.Id);
                    }

                    return fullName;
                }

                return epoch;
            }
            else
            {
                var fullName = xUser.FullName.CompareTo(yUser.FullName);
                if (fullName == 0)
                {
                    return yUser.Id.CompareTo(xUser.Id);
                }

                return fullName;
            }
        }
    }
}
