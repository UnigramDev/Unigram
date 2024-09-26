//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Threading.Tasks;
using Telegram.Collections;
using Telegram.Controls;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Streams;
using Telegram.Td.Api;
using Telegram.Views.Stars.Popups;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Navigation;

namespace Telegram.ViewModels.Profile
{
    public partial class ProfileGiftsTabViewModel : ViewModelBase, IIncrementalCollectionOwner
    {
        private long _userId;
        private string _nextOffsetId = string.Empty;

        public ProfileGiftsTabViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
            Items = new IncrementalCollection<UserGift>(this);
        }

        protected override Task OnNavigatedToAsync(object parameter, NavigationMode mode, NavigationState state)
        {
            if (parameter is long chatId)
            {
                var chat = ClientService.GetChat(chatId);
                if (chat == null)
                {
                    return Task.CompletedTask;
                }

                var user = ClientService.GetUser(chat);
                if (user == null)
                {
                    return Task.CompletedTask;
                }

                _userId = user.Id;
            }

            return Task.CompletedTask;
        }

        public IncrementalCollection<UserGift> Items { get; private set; }

        public async Task<LoadMoreItemsResult> LoadMoreItemsAsync(uint count)
        {
            var total = 0u;

            var response = await ClientService.SendAsync(new GetUserGifts(_userId, _nextOffsetId, 50));
            if (response is UserGifts gifts)
            {
                _nextOffsetId = gifts.NextOffset;

                foreach (var gift in gifts.Gifts)
                {
                    Items.Add(gift);
                    total++;
                }
            }

            HasMoreItems = !string.IsNullOrEmpty(_nextOffsetId);

            return new LoadMoreItemsResult
            {
                Count = total
            };
        }

        public bool HasMoreItems { get; private set; } = true;

        public async void OpenGift(UserGift userGift)
        {
            if (userGift == null)
            {
                return;
            }

            var confirm = await ShowPopupAsync(new ReceiptPopup(ClientService, NavigationService, userGift, _userId));
            if (confirm == ContentDialogResult.Primary)
            {
                var response = await ClientService.SendAsync(new ToggleGiftIsSaved(userGift.SenderUserId, userGift.MessageId, !userGift.IsSaved));
                if (response is Ok)
                {
                    userGift.IsSaved = !userGift.IsSaved;

                    var index = Items.IndexOf(userGift);
                    Items.Remove(userGift);
                    Items.Insert(index, userGift);

                    if (userGift.IsSaved)
                    {
                        ToastPopup.Show(XamlRoot, string.Format("**{0}**\n{1}", Strings.Gift2MadePublicTitle, Strings.Gift2MadePublic), new DelayedFileSource(ClientService, userGift.Gift.Sticker));
                    }
                    else
                    {
                        ToastPopup.Show(XamlRoot, string.Format("**{0}**\n{1}", Strings.Gift2MadePrivateTitle, Strings.Gift2MadePrivate), new DelayedFileSource(ClientService, userGift.Gift.Sticker));
                    }
                }
            }
            else if (confirm == ContentDialogResult.Secondary)
            {
                Items.Remove(userGift);
            }
        }
    }
}
