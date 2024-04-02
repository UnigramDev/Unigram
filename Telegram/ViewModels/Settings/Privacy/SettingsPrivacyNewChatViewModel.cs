//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System.Threading.Tasks;
using Telegram.Controls;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Td.Api;
using Windows.UI.Xaml.Navigation;

namespace Telegram.ViewModels.Settings.Privacy
{
    public class SettingsPrivacyNewChatViewModel : ViewModelBase
    {
        public SettingsPrivacyNewChatViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
        }

        protected override Task OnNavigatedToAsync(object parameter, NavigationMode mode, NavigationState state)
        {
            UpdatePrivacy();
            return Task.CompletedTask;
        }

        public bool CanSetNewChatPrivacySettings => ClientService.Options.CanSetNewChatPrivacySettings;

        private void UpdatePrivacy()
        {
            ClientService.Send(new GetNewChatPrivacySettings(), result =>
            {
                if (result is NewChatPrivacySettings rules)
                {
                    UpdatePrivacyImpl(rules);
                }
            });
        }

        private void UpdatePrivacyImpl(NewChatPrivacySettings rules)
        {
            BeginOnUIThread(() =>
            {
                SelectedItem = rules.AllowNewChatsFromUnknownUsers
                    ? PrivacyValue.AllowAll
                    : PrivacyValue.AllowContacts;

                Badge = rules.AllowNewChatsFromUnknownUsers
                    ? Strings.LastSeenEverybody
                    : Strings.PrivacyMessagesContactsAndPremium;
            });
        }

        private string _badge;
        public string Badge
        {
            get => _badge;
            set => Set(ref _badge, value);
        }

        private PrivacyValue _selectedItem;
        public PrivacyValue SelectedItem
        {
            get => _selectedItem;
            set => Set(ref _selectedItem, value);
        }

        public void Enable()
        {
            if (IsPremiumAvailable && !IsPremium)
            {
                ToastPopup.ShowOption(NavigationService);
            }
        }

        public async void Save()
        {
            if (IsPremium)
            {
                var response = await ClientService.SendAsync(new SetNewChatPrivacySettings(new NewChatPrivacySettings(_selectedItem is PrivacyValue.AllowAll)));
                if (response is Ok)
                {
                    NavigationService.GoBack();
                }
                else
                {
                    // TODO: ...
                }
            }
            else
            {
                NavigationService.GoBack();
            }
        }
    }
}
