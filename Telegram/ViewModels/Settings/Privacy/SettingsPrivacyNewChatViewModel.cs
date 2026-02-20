//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Telegram.Controls;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.Views.Settings.Privacy;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

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
            _allowUnpaidRules = Session.Resolve<SettingsPrivacyAllowUnpaidMessagesViewModel>();
            _allowUnpaidRules.PropertyChanged += OnPropertyChanged;

            Children.Add(_allowUnpaidRules);
        }

        private void OnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(HasChanged))
            {
                RaisePropertyChanged(nameof(HasChanged));
            }
        }

        public SettingsPrivacyAllowUnpaidMessagesViewModel AllowUnpaidRules => _allowUnpaidRules;

        protected override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, NavigationState state)
        {
            var response = await ClientService.SendAsync(new GetNewChatPrivacySettings());
            if (response is NewChatPrivacySettings rules)
            {
                UpdatePrivacy(rules);
            }
        }

        public override async void NavigatingFrom(NavigatingEventArgs args)
        {
            if (!_completed && HasChanged)
            {
                args.Cancel = true;

                var confirm = await ShowPopupAsync(Strings.PrivacySettingsChangedAlert, Strings.UnsavedChanges, Strings.ApplyTheme, Strings.PassportDiscard);
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

        public bool CanSetNewChatPrivacySettings => ClientService.Options.CanSetNewChatPrivacySettings;

        private void UpdatePrivacy(NewChatPrivacySettings rules)
        {
            BeginOnUIThread(() =>
            {
                _cached = rules;

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
            set => Invalidate(ref _selectedItem, value);
        }

        private int _incomingPaidMessageStarCount;
        public int IncomingPaidMessageStarCount
        {
            get => _incomingPaidMessageStarCount;
            set => Invalidate(ref _incomingPaidMessageStarCount, value);
        }

        public void Enable()
        {
            if (IsPremiumAvailable && !IsPremium)
            {
                ToastPopup.ShowOptionPromo(NavigationService);
            }
        }

        private NewChatPrivacySettings _cached;

        protected bool _completed;
        public virtual bool HasChanged => _cached != null && (_allowUnpaidRules.HasChanged || !_cached.AreTheSame(GetSettings()));

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
            if (IsPremium)
            {
                var response = await ClientService.SendAsync(new SetNewChatPrivacySettings(GetSettings()));
                if (response is Ok)
                {
                    _completed = true;

                    if (_selectedItem is PrivacyValue.DisallowAll)
                    {
                        _allowUnpaidRules.Continue();
                    }
                    else
                    {
                        NavigationService.GoBack(args);
                    }

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
                else if (response is Error error)
                {
                    ShowToast(error);
                }
            }
            else
            {
                _completed = true;
                NavigationService.GoBack(args);
            }
        }

        private NewChatPrivacySettings GetSettings()
        {
            return new NewChatPrivacySettings(_selectedItem is not PrivacyValue.AllowContacts, _selectedItem is PrivacyValue.DisallowAll ? _incomingPaidMessageStarCount : 0);
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
