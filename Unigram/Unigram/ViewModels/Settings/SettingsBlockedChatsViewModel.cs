using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Collections;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Services;
using Unigram.ViewModels.Delegates;
using Unigram.Views.Popups;
using Windows.Foundation;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels.Settings
{
    public class SettingsBlockedChatsViewModel : TLViewModelBase, IDelegable<IFileDelegate>, IHandle<UpdateChatIsBlocked>, IHandle<UpdateFile>
    {
        public IFileDelegate Delegate { get; set; }

        public SettingsBlockedChatsViewModel(IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(protoService, cacheService, settingsService, aggregator)
        {
            Items = new ItemsCollection(protoService, cacheService);

            BlockCommand = new RelayCommand(BlockExecute);
            UnblockCommand = new RelayCommand<Chat>(UnblockExecute);
        }

        public override Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            Aggregator.Subscribe(this);
            return Task.CompletedTask;
        }

        public override Task OnNavigatedFromAsync(IDictionary<string, object> pageState, bool suspending)
        {
            Aggregator.Unsubscribe(this);
            return Task.CompletedTask;
        }

        public void Handle(UpdateChatIsBlocked update)
        {
            BeginOnUIThread(() =>
            {
                var already = Items.FirstOrDefault(x => x.Id == update.ChatId);
                if (already != null && !update.IsBlocked)
                {
                    Items.Remove(already);
                }
            });
        }

        public void Handle(UpdateFile update)
        {
            BeginOnUIThread(() => Delegate?.UpdateFile(update.File));
        }

        public ObservableCollection<Chat> Items { get; private set; }

        //public async void Handle(TLUpdateUserBlocked message)
        //{
        //    var user = CacheService.GetUser(message.UserId) as TLUser;
        //    if (user != null)
        //    {
        //        BeginOnUIThread(() =>
        //        {
        //            if (message.Blocked)
        //            {
        //                Items.Insert(0, user);
        //            }
        //            else
        //            {
        //                Items.Remove(user);
        //            }
        //        });
        //    }
        //    else
        //    {
        //        var response = await LegacyService.GetFullUserAsync(new TLInputUser { UserId = message.UserId, AccessHash = 0 });
        //        if (response.IsSucceeded)
        //        {
        //            BeginOnUIThread(() =>
        //            {
        //                if (message.Blocked)
        //                {
        //                    Items.Insert(0, response.Result.User as TLUser);
        //                }
        //                else
        //                {
        //                    Items.Remove(response.Result.User as TLUser);
        //                }
        //            });
        //        }
        //    }
        //}

        public RelayCommand BlockCommand { get; }
        private async void BlockExecute()
        {
            var selected = await SharePopup.PickChatAsync(Strings.Resources.BlockUser);
            if (selected == null)
            {
                return;
            }

            ProtoService.Send(new ToggleChatIsBlocked(selected.Id, true));
        }

        public RelayCommand<Chat> UnblockCommand { get; }
        private async void UnblockExecute(Chat chat)
        {
            var confirm = await MessagePopup.ShowAsync(Strings.Resources.AreYouSureUnblockContact, Strings.Resources.AppName, Strings.Resources.OK, Strings.Resources.Cancel);
            if (confirm == ContentDialogResult.Primary)
            {
                ProtoService.Send(new ToggleChatIsBlocked(chat.Id, false));
            }
        }

        public class ItemsCollection : MvxObservableCollection<Chat>, ISupportIncrementalLoading
        {
            private readonly IProtoService _protoService;
            private readonly ICacheService _cacheService;

            public ItemsCollection(IProtoService protoService, ICacheService cacheService)
            {
                _protoService = protoService;
                _cacheService = cacheService;
            }

            public IAsyncOperation<LoadMoreItemsResult> LoadMoreItemsAsync(uint count)
            {
                return AsyncInfo.Run(async task =>
                {
                    var response = await _protoService.SendAsync(new GetBlockedChats(Count, 20));
                    if (response is Telegram.Td.Api.Chats chats)
                    {
                        foreach (var id in chats.ChatIds)
                        {
                            var chat = _protoService.GetChat(id);
                            if (chat != null)
                            {
                                Add(chat);
                            }
                        }

                        return new LoadMoreItemsResult { Count = (uint)chats.ChatIds.Count };
                    }

                    return new LoadMoreItemsResult();
                });
            }

            public bool HasMoreItems => true;
        }
    }
}
