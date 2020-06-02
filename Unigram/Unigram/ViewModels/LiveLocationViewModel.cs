using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Collections;
using Unigram.Services;
using Unigram.ViewModels.Delegates;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels
{
    public class LiveLocationViewModel : TLViewModelBase, IDelegable<ILiveLocationDelegate>, IHandle<UpdateMessageContent>, IHandle<UpdateNewMessage>
    {
        public ILiveLocationDelegate Delegate { get; set; }

        public LiveLocationViewModel(IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(protoService, cacheService, settingsService, aggregator)
        {
            Items = new MvxObservableCollection<Message>();
        }

        protected Chat _chat;
        public Chat Chat
        {
            get
            {
                return _chat;
            }
            set
            {
                Set(ref _chat, value);
            }
        }

        public MvxObservableCollection<Message> Items { get; private set; }

        public override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            var chatId = (long)parameter;

            Chat = ProtoService.GetChat(chatId);

            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            var response = await ProtoService.SendAsync(new SearchChatRecentLocationMessages(chat.Id, int.MaxValue));
            if (response is Messages messages)
            {
                Items.ReplaceWith(messages.MessagesValue);

                foreach (var message in messages.MessagesValue)
                {
                    Delegate?.UpdateNewMessage(message);
                }
            }
        }

        public void Handle(UpdateMessageContent update)
        {
            var chat = _chat;
            if (chat == null || chat.Id != update.ChatId)
            {
                return;
            }

            var message = Items.FirstOrDefault(x => x.Id == update.MessageId);
            if (message == null)
            {
                return;
            }

            message.Content = update.NewContent;
            Delegate?.UpdateMessageContent(message);
        }

        public void Handle(UpdateNewMessage update)
        {
            var chat = _chat;
            if (chat == null || chat.Id != update.Message.ChatId)
            {
                return;
            }

            if (update.Message.Content is MessageLocation location && location.LivePeriod > 0)
            {
                Items.Add(update.Message);
            }
        }
    }
}
