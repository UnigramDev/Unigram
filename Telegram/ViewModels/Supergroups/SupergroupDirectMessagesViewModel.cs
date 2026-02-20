//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Td.Api;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Telegram.ViewModels.Supergroups
{
    public partial class SupergroupDirectMessagesViewModel : SupergroupViewModelBase
    {
        public SupergroupDirectMessagesViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
        }

        private Chat _chat;
        public Chat Chat
        {
            get => _chat;
            set => Set(ref _chat, value);
        }

        private bool _isEnabled = true;
        public bool IsEnabled
        {
            get => _isEnabled;
            set => Invalidate(ref _isEnabled, value);
        }

        private int _paidMessageStarCount = 0;
        public int PaidMessageStarCount
        {
            get => _paidMessageStarCount;
            set => Invalidate(ref _paidMessageStarCount, value);
        }

        protected override Task OnNavigatedToAsync(object parameter, NavigationMode mode, NavigationState state)
        {
            var chatId = (long)parameter;

            Chat = ClientService.GetChat(chatId);

            var chat = _chat;
            if (chat == null)
            {
                return Task.CompletedTask;
            }

            if (ClientService.TryGetSupergroup(chat, out Supergroup supergroup) && ClientService.TryGetSupergroupFull(chat, out SupergroupFullInfo fullInfo))
            {
                if (ClientService.TryGetChat(fullInfo.DirectMessagesChatId, out Chat directMessagesChat))
                {
                    _cached = new SetChatDirectMessagesGroup(chat.Id, true, ClientService.PaidMessageStarCount(directMessagesChat));
                }
                else
                {
                    _cached = new SetChatDirectMessagesGroup(chat.Id, false, ClientService.Options.DirectChannelMessageStarCountDefault);
                }
            }
            else
            {
                _cached = new SetChatDirectMessagesGroup(chat.Id, false, ClientService.Options.DirectChannelMessageStarCountDefault);
            }

            IsEnabled = _cached.IsEnabled;
            PaidMessageStarCount = (int)_cached.PaidMessageStarCount;

            return Task.CompletedTask;
        }

        public override async void NavigatingFrom(NavigatingEventArgs args)
        {
            if (!_completed && HasChanged)
            {
                args.Cancel = true;

                var confirm = await ShowPopupAsync(Strings.MessageSuggestionsUnsavedChanges, Strings.UnsavedChanges, Strings.ChatThemeSaveDialogApply, Strings.ChatThemeSaveDialogDiscard);
                if (confirm == ContentDialogResult.Primary)
                {
                    ContinueImpl(args);
                }
                else if (confirm == ContentDialogResult.Secondary)
                {
                    _completed = true;
                    NavigationService.GoBack(args);
                }
            }
        }

        protected bool _completed;
        public bool HasChanged => !_cached.AreTheSame(GetSettings());

        protected bool Invalidate<T>(ref T storage, T value, [CallerMemberName] string propertyName = null)
        {
            if (Set(ref storage, value, propertyName))
            {
                RaisePropertyChanged(nameof(HasChanged));
                return true;
            }

            return false;
        }

        public void Continue()
        {
            ContinueImpl(null);
        }

        private async void ContinueImpl(NavigatingEventArgs args)
        {
            var settings = GetSettings();
            if (settings.AreTheSame(_cached))
            {
                _completed = true;
                NavigationService.GoBack(args);
                return;
            }

            var response = await ClientService.SendAsync(settings);
            if (response is Ok)
            {
                _completed = true;
                NavigationService.GoBack(args);
            }
            else if (response is Error error)
            {
                ShowToast(error);
            }
        }

        private SetChatDirectMessagesGroup _cached;
        private SetChatDirectMessagesGroup GetSettings()
        {
            return new SetChatDirectMessagesGroup(Chat.Id, IsEnabled, PaidMessageStarCount);
        }
    }
}
