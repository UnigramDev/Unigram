using System.Runtime.InteropServices.WindowsRuntime;
using Telegram.Td.Api;
using Unigram.Collections;
using Unigram.Controls;
using Unigram.Services;
using Unigram.ViewModels;
using Windows.Foundation;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Data;

namespace Unigram.Views.Popups
{
    public sealed partial class ChatJoinRequestsPopup : ContentPopup
    {
        public ChatJoinRequestsViewModel ViewModel => DataContext as ChatJoinRequestsViewModel;

        public ChatJoinRequestsPopup(IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator, Chat chat, string inviteLink)
        {
            InitializeComponent();
            DataContext = new ChatJoinRequestsViewModel(chat, inviteLink, protoService, cacheService, settingsService, aggregator);
        }

        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
        }

        private void ContentDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
        }
    }

    public class ChatJoinRequestsViewModel : TLViewModelBase
    {
        private readonly Chat _chat;
        private readonly string _inviteLink;

        public ChatJoinRequestsViewModel(Chat chat, string inviteLink, IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(protoService, cacheService, settingsService, aggregator)
        {
            _chat = chat;
            _inviteLink = inviteLink;

            Items = new ItemCollection(protoService, chat, inviteLink);
        }

        public ItemCollection Items { get; private set; }

        public class ItemCollection : MvxObservableCollection<ChatJoinRequest>, ISupportIncrementalLoading
        {
            private readonly IProtoService _protoService;
            private readonly Chat _chat;
            private readonly string _inviteLink;

            private ChatJoinRequest _offset;
            private bool _hasMoreItems = true;

            public ItemCollection(IProtoService protoService, Chat chat, string inviteLink)
            {
                _protoService = protoService;
                _chat = chat;
                _inviteLink = inviteLink;
            }

            public IAsyncOperation<LoadMoreItemsResult> LoadMoreItemsAsync(uint count)
            {
                return AsyncInfo.Run(async token =>
                {
                    var response = await _protoService.SendAsync(new GetChatJoinRequests(_chat.Id, _inviteLink, string.Empty, _offset, 10));
                    if (response is ChatJoinRequests requests)
                    {
                        foreach (var item in requests.Requests)
                        {
                            _offset = item;
                            Add(item);
                        }

                        _hasMoreItems = requests.Requests.Count > 0;
                    }

                    return new LoadMoreItemsResult { Count = 0 };
                });
            }

            public bool HasMoreItems => _hasMoreItems;
        }
    }

}
