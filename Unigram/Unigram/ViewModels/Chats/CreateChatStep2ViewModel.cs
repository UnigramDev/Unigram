using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Aggregator;
using Telegram.Api.Helpers;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Telegram.Api.TL;
using Template10.Common;
using Template10.Services.NavigationService;
using Unigram.Views;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels.Chats
{
    public class CreateChatStep2ViewModel : UsersSelectionViewModel
    {
        private string _title;

        public CreateChatStep2ViewModel(IMTProtoService protoService, ICacheService cacheService, ITelegramEventAggregator aggregator)
            : base(protoService, cacheService, aggregator)
        {
            var config = CacheService.GetConfig();
            if (config != null)
            {
                _maximum = config.MegaGroupSizeMax;
            }
        }

        public override string Title
        {
            get
            {
                return "Test!";
            }
        }

        private int _maximum;
        public override int Maximum { get { return _maximum; } }

        public override int Minimum { get { return 1; } }

        public override Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            _title = (string)parameter;
            return base.OnNavigatedToAsync(parameter, mode, state);
        }

        protected override async void SendExecute()
        {
            var config = CacheService.GetConfig();
            if (config == null) return;

            var peers = new TLVector<TLInputUserBase>(SelectedItems.Select(x => x.ToInputUser()));
            if (peers.Count <= config.ChatSizeMax)
            {
                // Classic chat
                var response = await ProtoService.CreateChatAsync(peers, _title);
                if (response.IsSucceeded)
                {
                    var updates = response.Result as TLUpdates;
                    if (updates != null)
                    {
                        CacheService.SyncUsersAndChats(updates.Users, updates.Chats, tuple => { });

                        var chat = updates.Chats.FirstOrDefault() as TLChat;
                        if (chat != null)
                        {
                            NavigationService.Navigate(typeof(DialogPage), new TLPeerChat { ChatId = chat.Id });
                        }
                    }
                }
                else
                {
                    Execute.ShowDebugMessage("messages.createChat error " + response.Error);
                }
            }
            else
            {
            }
        }
    }
}
