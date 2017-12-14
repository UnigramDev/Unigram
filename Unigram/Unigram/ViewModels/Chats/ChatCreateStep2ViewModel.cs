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
using Unigram.Common;
using Unigram.Views;
using Windows.UI.Xaml.Navigation;
using Windows.Storage;
using ChatCreateStep2Tuple = Telegram.Api.TL.TLTuple<string, Telegram.Api.TL.TLInputFileBase>;
using Telegram.Api.Native.TL;
using System.Runtime.InteropServices.WindowsRuntime;

namespace Unigram.ViewModels.Chats
{
    public class ChatCreateStep2ViewModel : UsersSelectionViewModel
    {
        private string _title;
        private TLInputFileBase _photo;

        public ChatCreateStep2ViewModel(IMTProtoService protoService, ICacheService cacheService, ITelegramEventAggregator aggregator)
            : base(protoService, cacheService, aggregator)
        {
            var config = CacheService.GetConfig();
            if (config != null)
            {
                _maximum = config.MegaGroupSizeMax;
            }
        }

        public override string Title => _title;

        private int _maximum;
        public override int Maximum { get { return _maximum; } }

        public override int Minimum { get { return 1; } }

        public override Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            var buffer = parameter as byte[];
            if (buffer == null)
            {
                return Task.CompletedTask;
            }

            using (var from = TLObjectSerializer.CreateReader(buffer.AsBuffer()))
            {
                var tuple = new ChatCreateStep2Tuple(from);

                _title = tuple.Item1;
                _photo = tuple.Item2;
            }

            RaisePropertyChanged(() => Title);
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
                            if (_photo != null)
                            {
                                var edit = await ProtoService.EditChatPhotoAsync(chat.Id, new TLInputChatUploadedPhoto { File = _photo });
                                if (edit.IsSucceeded)
                                {

                                }
                                else
                                {

                                }
                            }

                            NavigationService.NavigateToDialog(chat);
                            NavigationService.RemoveLast();
                            NavigationService.RemoveLast();
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
