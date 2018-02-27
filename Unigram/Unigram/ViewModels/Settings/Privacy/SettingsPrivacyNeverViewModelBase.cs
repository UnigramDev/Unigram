using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TdWindows;
using Telegram.Api.Helpers;
using Unigram.Common;
using Unigram.Services;

namespace Unigram.ViewModels.Settings.Privacy
{
    public abstract class SettingsPrivacyNeverViewModelBase : UsersSelectionViewModel, INavigableWithResult<UserPrivacySettingRuleRestrictUsers>
    {
        private readonly UserPrivacySetting _inputKey;
        private TaskCompletionSource<UserPrivacySettingRuleRestrictUsers> _tsc;

        public SettingsPrivacyNeverViewModelBase(IProtoService protoService, ICacheService cacheService, IEventAggregator aggregator, UserPrivacySetting inputKey)
            : base(protoService, cacheService, aggregator)
        {
            _inputKey = inputKey;

            //UpdatePrivacyAsync();
        }

        public override int Maximum => int.MaxValue;

        public void SetAwaiter(TaskCompletionSource<UserPrivacySettingRuleRestrictUsers> tsc, object parameter)
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
            if (_tsc != null)
            {
                _tsc.SetResult(new UserPrivacySettingRuleRestrictUsers(SelectedItems.Select(x => x.Id).ToList()));
            }

            NavigationService.GoBack();
        }

        protected override void BeginOnUIThread(Action action)
        {
            // This is somehow needed because this viewmodel requires a Dispatcher
            // in some situations where base one might be null.
            Execute.BeginOnUIThread(action);
        }
    }
}
