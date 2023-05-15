//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System.Collections.ObjectModel;
using System.Runtime.InteropServices.WindowsRuntime;
using Telegram.Collections;
using Telegram.Common;
using Telegram.Navigation;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.Views.Popups;
using Windows.Foundation;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Data;

namespace Telegram.ViewModels.Settings
{
    public class SettingsBlockedChatsViewModel : ViewModelBase
    {
        public SettingsBlockedChatsViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
            Items = new ItemsCollection(clientService);

            UnblockCommand = new RelayCommand<MessageSender>(UnblockExecute);
        }

        public ObservableCollection<MessageSender> Items { get; private set; }

        public async void Block()
        {
            var selected = await SharePopup.PickChatAsync(Strings.BlockUser);
            if (selected == null)
            {
                return;
            }

            if (selected.Type is ChatTypePrivate privata)
            {
                Items.Insert(0, new MessageSenderUser(privata.UserId));
                ClientService.Send(new ToggleMessageSenderIsBlocked(new MessageSenderUser(privata.UserId), true));
            }
            else
            {
                Items.Insert(0, new MessageSenderChat(selected.Id));
                ClientService.Send(new ToggleMessageSenderIsBlocked(new MessageSenderChat(selected.Id), true));
            }
        }

        public RelayCommand<MessageSender> UnblockCommand { get; }
        private async void UnblockExecute(MessageSender sender)
        {
            var confirm = await ShowPopupAsync(Strings.AreYouSureUnblockContact, Strings.AppName, Strings.OK, Strings.Cancel);
            if (confirm == ContentDialogResult.Primary)
            {
                Items.Remove(sender);
                ClientService.Send(new ToggleMessageSenderIsBlocked(sender, false));
            }
        }

        public class ItemsCollection : MvxObservableCollection<MessageSender>, ISupportIncrementalLoading
        {
            private readonly IClientService _clientService;

            public ItemsCollection(IClientService clientService)
            {
                _clientService = clientService;
            }

            public IAsyncOperation<LoadMoreItemsResult> LoadMoreItemsAsync(uint count)
            {
                return AsyncInfo.Run(async task =>
                {
                    var response = await _clientService.SendAsync(new GetBlockedMessageSenders(Count, 20));
                    if (response is MessageSenders chats)
                    {
                        foreach (var sender in chats.Senders)
                        {
                            Add(sender);
                        }

                        return new LoadMoreItemsResult { Count = (uint)chats.Senders.Count };
                    }

                    return new LoadMoreItemsResult();
                });
            }

            public bool HasMoreItems => true;
        }
    }
}
