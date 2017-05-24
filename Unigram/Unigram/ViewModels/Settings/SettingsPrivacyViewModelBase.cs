using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Aggregator;
using Telegram.Api.Helpers;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Telegram.Api.TL;

namespace Unigram.ViewModels.Settings
{
    public class SettingsPrivacyViewModelBase : UnigramViewModelBase, IHandle<TLUpdatePrivacy>
    {
        private readonly TLInputPrivacyKeyBase _inputKey;
        private readonly TLType _key;

        public SettingsPrivacyViewModelBase(IMTProtoService protoService, ICacheService cacheService, ITelegramEventAggregator aggregator, TLInputPrivacyKeyBase inputKey, TLType key)
            : base(protoService, cacheService, aggregator)
        {
            _inputKey = inputKey;
            _key = key;

            UpdatePrivacyAsync();
            Aggregator.Subscribe(this);
        }

        public void Handle(TLUpdatePrivacy update)
        {
            if (update.Key.TypeId == _key)
            {
                UpdatePrivacy(new TLAccountPrivacyRules { Rules = update.Rules });
            }
        }

        private void UpdatePrivacyAsync()
        {
            ProtoService.GetPrivacyAsync(_inputKey, result => UpdatePrivacy(result));
        }

        private void UpdatePrivacy(TLAccountPrivacyRules rules)
        {
            TLPrivacyRuleBase primary = null;
            var badge = string.Empty;
            var disallowed = 0;
            var allowed = 0;
            foreach (TLPrivacyRuleBase current in rules.Rules)
            {
                if (current is TLPrivacyValueAllowAll)
                {
                    primary = current;
                    badge = "Everybody";
                }
                else if (current is TLPrivacyValueAllowContacts)
                {
                    primary = current;
                    badge = "My Contacts";
                }
                else if (current is TLPrivacyValueDisallowAll)
                {
                    primary = current;
                    badge = "Nobody";
                }
                else if (current is TLPrivacyValueDisallowUsers disallowUsers)
                {
                    disallowed += disallowUsers.Users.Count;
                }
                else if (current is TLPrivacyValueAllowUsers allowUsers)
                {
                    allowed += allowUsers.Users.Count;
                }
            }

            if (primary == null)
            {
                primary = new TLPrivacyValueDisallowAll();
                badge = "Nobody";
            }

            var list = new List<string>();
            if (disallowed > 0)
            {
                list.Add("-" + disallowed);
            }
            if (allowed > 0)
            {
                list.Add("+" + allowed);
            }

            if (list.Count > 0)
            {
                badge += string.Format(" ({0})", string.Join(", ", list));
            }

            Execute.BeginOnUIThread(() =>
            {
                Badge = badge;
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
    }
}
