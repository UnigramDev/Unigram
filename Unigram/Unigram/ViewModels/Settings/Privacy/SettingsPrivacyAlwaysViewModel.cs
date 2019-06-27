using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Template10.Common;
using Unigram.Common;
using Unigram.Services;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels.Settings.Privacy
{
    public class SettingsPrivacyAlwaysViewModel : UsersSelectionViewModel
    {
        private readonly UserPrivacySetting _inputKey;

        public SettingsPrivacyAlwaysViewModel(IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator, UserPrivacySetting inputKey)
            : base(protoService, cacheService, settingsService, aggregator)
        {
            _inputKey = inputKey;
        }

        public override string Title
        {
            get
            {
                switch (_inputKey)
                {
                    case UserPrivacySettingAllowCalls allowCalls:
                    case UserPrivacySettingAllowPeerToPeerCalls allowP2PCalls:
                    case UserPrivacySettingAllowChatInvites allowChatInvites:
                    //case UserPrivacySettingShowProfilePhoto showProfilePhoto:
                    //case UserPrivacySettingShowLinkInForwardedMessages showLinkInForwardedMessages:
                    default:
                        return Strings.Resources.AlwaysAllow;
                    case UserPrivacySettingShowStatus showStatus:
                        return Strings.Resources.AlwaysShareWithTitle;
                }
            }
        }

        public override int Maximum => int.MaxValue;

        private UserPrivacySettingRuleAllowUsers _rule;
        public UserPrivacySettingRuleAllowUsers Rule
        {
            get { return _rule; }
            set { Set(ref _rule, value); }
        }

        public override Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            if (parameter is UserPrivacySettingRules rules)
            {
                UpdatePrivacy(rules);
            }

            return base.OnNavigatedToAsync(parameter, mode, state);
        }

        private void UpdatePrivacy(UserPrivacySettingRules rules)
        {
            var allowed = rules.Rules.FirstOrDefault(x => x is UserPrivacySettingRuleAllowUsers) as UserPrivacySettingRuleAllowUsers;
            if (allowed == null)
            {
                allowed = new UserPrivacySettingRuleAllowUsers(new int[0]);
            }

            var users = ProtoService.GetUsers(allowed.UserIds);

            BeginOnUIThread(() =>
            {
                SelectedItems.AddRange(users);
            });
        }

        protected override void SendExecute()
        {
            Rule = new UserPrivacySettingRuleAllowUsers(SelectedItems.Select(x => x.Id).ToList());
            //if (_tsc != null)
            //{
            //    _tsc.SetResult(new UserPrivacySettingRuleAllowUsers(SelectedItems.Select(x => x.Id).ToList()));
            //}

            //NavigationService.GoBack();
        }
    }
}
