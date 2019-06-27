using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Services;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels.Settings.Privacy
{
    public class SettingsPrivacyNeverViewModel : UsersSelectionViewModel
    {
        private readonly UserPrivacySetting _inputKey;

        public SettingsPrivacyNeverViewModel(IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator, UserPrivacySetting inputKey)
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
                        return Strings.Resources.NeverAllow;
                    case UserPrivacySettingShowStatus showStatus:
                        return Strings.Resources.NeverShareWithTitle;
                }
            }
        }

        public override int Maximum => int.MaxValue;

        private UserPrivacySettingRuleRestrictUsers _rule;
        public UserPrivacySettingRuleRestrictUsers Rule
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
            var disallowed = rules.Rules.FirstOrDefault(x => x is UserPrivacySettingRuleRestrictUsers) as UserPrivacySettingRuleRestrictUsers;
            if (disallowed == null)
            {
                disallowed = new UserPrivacySettingRuleRestrictUsers(new int[0]);
            }

            var users = ProtoService.GetUsers(disallowed.UserIds);

            BeginOnUIThread(() =>
            {
                SelectedItems.AddRange(users);
            });
        }

        protected override void SendExecute()
        {
            Rule = new UserPrivacySettingRuleRestrictUsers(SelectedItems.Select(x => x.Id).ToList());
            //if (_tsc != null)
            //{
            //    _tsc.SetResult(new UserPrivacySettingRuleRestrictUsers(SelectedItems.Select(x => x.Id).ToList()));
            //}

            //NavigationService.GoBack();
        }
    }
}
