using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TdWindows;
using Template10.Common;
using Unigram.Common;
using Unigram.Services;

namespace Unigram.ViewModels.Settings.Privacy
{
    public abstract class SettingsPrivacyAlwaysViewModelBase : UsersSelectionViewModel, INavigableWithResult<UserPrivacySettingRuleAllowUsers>
    {
        private readonly UserPrivacySetting _inputKey;

        private TaskCompletionSource<UserPrivacySettingRuleAllowUsers> _tsc;

        public SettingsPrivacyAlwaysViewModelBase(IProtoService protoService, ICacheService cacheService, IEventAggregator aggregator, UserPrivacySetting inputKey)
            : base(protoService, cacheService, aggregator)
        {
            _inputKey = inputKey;

            //UpdatePrivacyAsync();
        }

        public override int Maximum => int.MaxValue;

        public void SetAwaiter(TaskCompletionSource<UserPrivacySettingRuleAllowUsers> tsc, object parameter)
        {
            _tsc = tsc;

            if (parameter is UserPrivacySettingRules rules)
            {
                UpdatePrivacy(rules);
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
            if (_tsc != null)
            {
                _tsc.SetResult(new UserPrivacySettingRuleAllowUsers(SelectedItems.Select(x => x.Id).ToList()));
            }

            NavigationService.GoBack();
        }
    }
}
