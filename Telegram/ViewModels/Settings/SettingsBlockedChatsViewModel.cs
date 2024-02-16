//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Telegram.Collections;
using Telegram.Navigation;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.Views.Popups;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Data;

namespace Telegram.ViewModels.Settings
{
    public class SettingsBlockedChatsViewModel : ViewModelBase, IIncrementalCollectionOwner
    {
        public SettingsBlockedChatsViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
            Items = new IncrementalCollection<MessageSender>(this);
        }

        public ObservableCollection<MessageSender> Items { get; private set; }

        public async void Block()
        {
            var selected = await ChooseChatsPopup.PickChatAsync(Strings.BlockUser, ChooseChatsOptions.Users);
            if (selected == null)
            {
                return;
            }

            if (selected.Type is ChatTypePrivate privata)
            {
                Items.Insert(0, new MessageSenderUser(privata.UserId));
                ClientService.Send(new SetMessageSenderBlockList(new MessageSenderUser(privata.UserId), new BlockListMain()));
            }
            else
            {
                Items.Insert(0, new MessageSenderChat(selected.Id));
                ClientService.Send(new SetMessageSenderBlockList(new MessageSenderChat(selected.Id), new BlockListMain()));
            }
        }

        public async void Unblock(MessageSender sender)
        {
            var confirm = await ShowPopupAsync(Strings.AreYouSureUnblockContact, Strings.AppName, Strings.OK, Strings.Cancel);
            if (confirm == ContentDialogResult.Primary)
            {
                Items.Remove(sender);
                ClientService.Send(new SetMessageSenderBlockList(sender, null));
            }
        }

        public async Task<LoadMoreItemsResult> LoadMoreItemsAsync(uint count)
        {
            var response = await ClientService.SendAsync(new GetBlockedMessageSenders(new BlockListMain(), Items.Count, 20));
            if (response is MessageSenders chats)
            {
                foreach (var sender in chats.Senders)
                {
                    Items.Add(sender);
                }

                HasMoreItems = chats.Senders.Count > 0;
                return new LoadMoreItemsResult { Count = (uint)chats.Senders.Count };
            }

            HasMoreItems = false;
            return new LoadMoreItemsResult();
        }

        public bool HasMoreItems { get; private set; } = true;
    }
}
