//
// Copyright Fela Ameghino 2015-2025
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Threading.Tasks;
using Telegram.Controls;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.Views;
using Telegram.Views.Settings.Privacy;

namespace Telegram.ViewModels.Settings.Privacy
{
    public partial class SettingsPrivacyAllowUnpaidMessagesViewModel : SettingsPrivacyViewModelBase
    {
        public SettingsPrivacyAllowUnpaidMessagesViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator, new UserPrivacySettingAllowUnpaidMessages())
        {
        }
    }

    public partial class SettingsPrivacyNewChatViewModel : MultiViewModelBase
    {
        private readonly SettingsPrivacyAllowUnpaidMessagesViewModel _allowUnpaidRules;

        public SettingsPrivacyNewChatViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
            _allowUnpaidRules = TypeResolver.Current.Resolve<SettingsPrivacyAllowUnpaidMessagesViewModel>(SessionId);

            Children.Add(_allowUnpaidRules);
        }

        public SettingsPrivacyAllowUnpaidMessagesViewModel AllowUnpaidRules => _allowUnpaidRules;

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
                SelectedItem = rules.IncomingPaidMessageStarCount > 0
                    ? PrivacyValue.DisallowAll
                    : rules.AllowNewChatsFromUnknownUsers
                    ? PrivacyValue.AllowAll
                    : PrivacyValue.AllowContacts;

                Badge = rules.IncomingPaidMessageStarCount > 0
                    ? Strings.ContactsAndFee
                    : rules.AllowNewChatsFromUnknownUsers
                    ? Strings.LastSeenEverybody
                    : Strings.ContactsAndPremium;

                IncomingPaidMessageStarCount = Math.Clamp((int)rules.IncomingPaidMessageStarCount, 1, 9000);
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

        private int _incomingPaidMessageStarCount;
        public int IncomingPaidMessageStarCount
        {
            get => _incomingPaidMessageStarCount;
            set => Set(ref _incomingPaidMessageStarCount, value);
        }

        public void Enable()
        {
            if (IsPremiumAvailable && !IsPremium)
            {
                ToastPopup.ShowOptionPromo(NavigationService);
            }
        }

        public async void Save()
        {
            if (IsPremium)
            {
                var response = await ClientService.SendAsync(new SetNewChatPrivacySettings(new NewChatPrivacySettings(_selectedItem is not PrivacyValue.AllowContacts, _selectedItem is PrivacyValue.DisallowAll ? _incomingPaidMessageStarCount : 0)));
                if (response is Ok)
                {
                    if (_selectedItem is PrivacyValue.DisallowAll)
                    {
                        _allowUnpaidRules.Save();
                    }

                    NavigationService.GoBack();

                    if (await CheckAllowAllAsync(new UserPrivacySettingAllowCalls()))
                    {
                        var confirm = await ShowPopupAsync(Strings.CheckPrivacyCallsText, Strings.CheckPrivacyCallsTitle, Strings.CheckPrivacyReview, Strings.Cancel);
                        if (confirm == ContentDialogResult.Primary)
                        {
                            NavigationService.Navigate(typeof(SettingsPrivacyAllowCallsPage));
                        }
                    }
                    else if (await CheckAllowAllAsync(new UserPrivacySettingAllowChatInvites()))
                    {
                        var confirm = await ShowPopupAsync(Strings.CheckPrivacyInviteText, Strings.CheckPrivacyInviteTitle, Strings.CheckPrivacyReview, Strings.Cancel);
                        if (confirm == ContentDialogResult.Primary)
                        {
                            NavigationService.Navigate(typeof(SettingsPrivacyAllowChatInvitesPage));
                        }
                    }
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

        private async Task<bool> CheckAllowAllAsync(UserPrivacySetting setting)
        {
            var response = await ClientService.SendAsync(new GetUserPrivacySettingRules(setting));
            if (response is UserPrivacySettingRules rules)
            {
                foreach (var rule in rules.Rules)
                {
                    if (rule is UserPrivacySettingRuleAllowAll)
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
