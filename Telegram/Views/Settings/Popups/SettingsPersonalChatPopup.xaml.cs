//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System.Linq;
using System.Threading.Tasks;
using Telegram.Collections;
using Telegram.Controls;
using Telegram.Controls.Cells;
using Telegram.Services;
using Telegram.Td.Api;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Data;

namespace Telegram.Views.Settings.Popups
{
    public sealed partial class SettingsPersonalChatPopup : ContentPopup, IIncrementalCollectionOwner
    {
        private readonly IClientService _clientService;
        private readonly long _selectedChatId;

        private readonly IncrementalCollection<Chat> _items;

        public SettingsPersonalChatPopup(IClientService clientService)
        {
            InitializeComponent();

            _clientService = clientService;
            _items = new IncrementalCollection<Chat>(this);

            if (clientService.TryGetUserFull(clientService.Options.MyId, out UserFullInfo fullInfo))
            {
                _selectedChatId = fullInfo.PersonalChatId;
            }

            Title = Strings.EditProfileChannelSelect;

            PrimaryButtonText = Strings.OK;
            SecondaryButtonText = Strings.Cancel;

            ScrollingHost.ItemsSource = _items;
            SelectedChatId = _selectedChatId;

            if (_selectedChatId == 0)
            {
                CurrentLocation.UpdateState(true, false, true);
            }
        }
        private void CurrentLocation_Click(object sender, RoutedEventArgs e)
        {
            ScrollingHost.SelectedItem = null;

            SelectedChatId = 0;
            CurrentLocation.UpdateState(true, true, false);
        }

        #region Recycle

        private void OnChoosingItemContainer(ListViewBase sender, ChoosingItemContainerEventArgs args)
        {
            if (args.ItemContainer == null)
            {
                args.ItemContainer = new MultipleListViewItem(sender, false);
                args.ItemContainer.Style = sender.ItemContainerStyle;
                args.ItemContainer.ContentTemplate = sender.ItemTemplate;
            }

            args.IsContainerPrepared = true;
        }

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }
            else if (args.ItemContainer.ContentTemplateRoot is ChatShareCell content && args.Item is Chat chat)
            {
                content.UpdateChat(_clientService, args, OnContainerContentChanging);
                content.UpdateState(args.ItemContainer.IsSelected, false, false);

                args.Handled = true;
            }
        }

        #endregion

        private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count > 0 && e.AddedItems[0] is Chat chat)
            {
                SelectedChatId = chat.Id;
                CurrentLocation.UpdateState(false, true, false);
            }
        }

        public async Task<LoadMoreItemsResult> LoadMoreItemsAsync(uint count)
        {
            var totalCount = 0u;

            var response = await _clientService.SendAsync(new GetSuitablePersonalChats());
            if (response is Td.Api.Chats chats)
            {
                foreach (var chat in _clientService.GetChats(chats.ChatIds))
                {
                    _items.Add(chat);
                    totalCount++;
                }
            }

            ScrollingHost.SelectedItem = _items.FirstOrDefault(x => x.Id == _selectedChatId);
            HasMoreItems = false;

            return new LoadMoreItemsResult
            {
                Count = totalCount
            };
        }

        public bool HasMoreItems { get; private set; } = true;

        public long SelectedChatId { get; private set; }
    }
}
