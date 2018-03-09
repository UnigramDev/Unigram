using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unigram.Common;
using Unigram.Strings;
using Unigram.Services;
using Telegram.Td.Api;
using Unigram.Views.Settings.Privacy;
using Unigram.Controls;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels.Settings
{
    public class SettingsPrivacyViewModelBase : UnigramViewModelBase, IHandle<UpdateUserPrivacySettingRules>
    {
        private readonly UserPrivacySetting _inputKey;

        private UserPrivacySettingRules _rules;

        public SettingsPrivacyViewModelBase(IProtoService protoService, ICacheService cacheService, IEventAggregator aggregator, UserPrivacySetting inputKey)
            : base(protoService, cacheService, aggregator)
        {
            _inputKey = inputKey;

            AlwaysCommand = new RelayCommand(AlwaysExecute);
            NeverCommand = new RelayCommand(NeverExecute);
            SendCommand = new RelayCommand(SendExecute);

            Aggregator.Subscribe(this);
        }

        public override Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            if (mode != NavigationMode.Back)
            {
                UpdatePrivacyAsync();
            }

            return Task.CompletedTask;
        }

        //public void Handle(TLUpdatePrivacy update)
        //{
        //    if (update.Key.TypeId == _key)
        //    {
        //        UpdatePrivacy(new TLAccountPrivacyRules { Rules = update.Rules });
        //    }
        //}

        public void Handle(UpdateUserPrivacySettingRules update)
        {
            if (update.Setting.TypeEquals(_inputKey))
            {
                UpdatePrivacy(update.Rules);
            }
        }

        private void UpdatePrivacyAsync()
        {
            ProtoService.Send(new GetUserPrivacySettingRules(_inputKey), result =>
            {
                if (result is UserPrivacySettingRules rules)
                {
                    UpdatePrivacy(rules);
                }
            });
        }

        private void UpdatePrivacy(UserPrivacySettingRules rules)
        {
            _rules = rules;
            var badge = string.Empty;
            PrivacyValue? primary = null;
            UserPrivacySettingRuleRestrictUsers disallowed = null;
            UserPrivacySettingRuleAllowUsers allowed = null;
            foreach (var current in rules.Rules)
            {
                if (current is UserPrivacySettingRuleAllowAll)
                {
                    primary = PrivacyValue.AllowAll;
                    badge = Strings.Resources.LastSeenEverybody;
                }
                else if (current is UserPrivacySettingRuleAllowContacts)
                {
                    primary = PrivacyValue.AllowContacts;
                    badge = Strings.Resources.LastSeenContacts;
                }
                else if (current is UserPrivacySettingRuleRestrictAll)
                {
                    primary = PrivacyValue.DisallowAll;
                    badge = Strings.Resources.LastSeenNobody;
                }
                else if (current is UserPrivacySettingRuleRestrictUsers disallowUsers)
                {
                    disallowed = disallowUsers;
                }
                else if (current is UserPrivacySettingRuleAllowUsers allowUsers)
                {
                    allowed = allowUsers;
                }
            }

            if (primary == null)
            {
                primary = PrivacyValue.DisallowAll;
                badge = Strings.Resources.LastSeenNobody;
            }

            var list = new List<string>();
            if (disallowed != null)
            {
                list.Add("-" + disallowed.UserIds.Count);
            }
            if (allowed != null)
            {
                list.Add("+" + allowed.UserIds.Count);
            }

            if (list.Count > 0)
            {
                badge = string.Format("{0} ({1})", badge, string.Join(", ", list));
            }

            BeginOnUIThread(() =>
            {
                Badge = badge;

                SelectedItem = primary ?? PrivacyValue.DisallowAll;
                Allowed = allowed ?? new UserPrivacySettingRuleAllowUsers(new int[0]);
                Disallowed = disallowed ?? new UserPrivacySettingRuleRestrictUsers(new int[0]);
            });

        }

        private string _badge;
        public string Badge
        {
            get
            {
                return _badge;
            }
            set
            {
                Set(ref _badge, value);
            }
        }

        private PrivacyValue _selectedItem;
        public PrivacyValue SelectedItem
        {
            get
            {
                return _selectedItem;
            }
            set
            {
                Set(ref _selectedItem, value);
            }
        }

        private UserPrivacySettingRuleAllowUsers _allowed;
        public UserPrivacySettingRuleAllowUsers Allowed
        {
            get
            {
                return _allowed;
            }
            set
            {
                Set(ref _allowed, value);
            }
        }

        private UserPrivacySettingRuleRestrictUsers _disallowed;
        public UserPrivacySettingRuleRestrictUsers Disallowed
        {
            get
            {
                return _disallowed;
            }
            set
            {
                Set(ref _disallowed, value);
            }
        }

        public RelayCommand AlwaysCommand { get; }
        public async void AlwaysExecute()
        {
            UserPrivacySettingRuleAllowUsers result = null;
            switch (_inputKey)
            {
                case UserPrivacySettingAllowCalls allowCalls:
                    result = await NavigationService.NavigateWithResult<UserPrivacySettingRuleAllowUsers>(typeof(SettingsPrivacyAlwaysAllowCallsPage), _rules);
                    break;
                case UserPrivacySettingAllowChatInvites allowChatInvites:
                    result = await NavigationService.NavigateWithResult<UserPrivacySettingRuleAllowUsers>(typeof(SettingsPrivacyAlwaysAllowChatInvitesPage), _rules);
                    break;
                case UserPrivacySettingShowStatus showStatus:
                    result = await NavigationService.NavigateWithResult<UserPrivacySettingRuleAllowUsers>(typeof(SettingsPrivacyAlwaysShowStatusPage), _rules);
                    break;
            }

            if (result != null)
            {
                Allowed = result;
            }
        }

        public RelayCommand NeverCommand { get; }
        public async void NeverExecute()
        {
            UserPrivacySettingRuleRestrictUsers result = null;
            switch (_inputKey)
            {
                case UserPrivacySettingAllowCalls allowCalls:
                    result = await NavigationService.NavigateWithResult<UserPrivacySettingRuleRestrictUsers>(typeof(SettingsPrivacyNeverAllowCallsPage), _rules);
                    break;
                case UserPrivacySettingAllowChatInvites allowChatInvites:
                    result = await NavigationService.NavigateWithResult<UserPrivacySettingRuleRestrictUsers>(typeof(SettingsPrivacyNeverAllowChatInvitesPage), _rules);
                    break;
                case UserPrivacySettingShowStatus showStatus:
                    result = await NavigationService.NavigateWithResult<UserPrivacySettingRuleRestrictUsers>(typeof(SettingsPrivacyNeverShowStatusPage), _rules);
                    break;
            }

            if (result != null)
            {
                Disallowed = result;
            }
        }

        public RelayCommand SendCommand { get; }
        public async void SendExecute()
        {
            var rules = new List<UserPrivacySettingRule>();

            if (_disallowed != null && _disallowed.UserIds.Count > 0 && _selectedItem != PrivacyValue.DisallowAll)
            {
                rules.Add(_disallowed);
            }

            if (_allowed != null && _allowed.UserIds.Count > 0 && _selectedItem != PrivacyValue.AllowAll)
            {
                rules.Add(_allowed);
            }

            switch (_selectedItem)
            {
                case PrivacyValue.AllowAll:
                    rules.Add(new UserPrivacySettingRuleAllowAll());
                    break;
                case PrivacyValue.AllowContacts:
                    rules.Add(new UserPrivacySettingRuleAllowContacts());
                    break;
                case PrivacyValue.DisallowAll:
                    rules.Add(new UserPrivacySettingRuleRestrictAll());
                    break;
            }

            var response = await ProtoService.SendAsync(new SetUserPrivacySettingRules(_inputKey, new UserPrivacySettingRules(rules)));
            if (response is Ok)
            {
                NavigationService.GoBack();
            }
            else if (response is Error error)
            {

            }
        }
    }

    public enum PrivacyValue
    {
        AllowAll,
        AllowContacts,
        DisallowAll
    }
}
