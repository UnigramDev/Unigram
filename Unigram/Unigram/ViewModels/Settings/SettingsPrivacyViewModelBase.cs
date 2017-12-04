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
using Telegram.Api.TL.Account;
using Unigram.Common;
using Unigram.Strings;

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
            var badge = string.Empty;
            PrivacyValue? primary = null;
            TLPrivacyValueDisallowUsers disallowed = null;
            TLPrivacyValueAllowUsers allowed = null;
            foreach (TLPrivacyRuleBase current in rules.Rules)
            {
                if (current is TLPrivacyValueAllowAll)
                {
                    primary = PrivacyValue.AllowAll;
                    badge = Strings.Android.LastSeenEverybody;
                }
                else if (current is TLPrivacyValueAllowContacts)
                {
                    primary = PrivacyValue.AllowContacts;
                    badge = Strings.Android.LastSeenContacts;
                }
                else if (current is TLPrivacyValueDisallowAll)
                {
                    primary = PrivacyValue.DisallowAll;
                    badge = Strings.Android.LastSeenNobody;
                }
                else if (current is TLPrivacyValueDisallowUsers disallowUsers)
                {
                    disallowed = disallowUsers;
                }
                else if (current is TLPrivacyValueAllowUsers allowUsers)
                {
                    allowed = allowUsers;
                }
            }

            if (primary == null)
            {
                primary = PrivacyValue.DisallowAll;
                badge = Strings.Android.LastSeenNobody;
            }

            var list = new List<string>();
            if (disallowed != null)
            {
                list.Add("-" + disallowed.Users.Count);
            }
            if (allowed != null)
            {
                list.Add("+" + allowed.Users.Count);
            }

            if (list.Count > 0)
            {
                badge = string.Format("{0} ({1})", badge, string.Join(", ", list));
            }

            BeginOnUIThread(() =>
            {
                Badge = badge;

                SelectedItem = primary ?? PrivacyValue.DisallowAll;
                Allowed = allowed ?? new TLPrivacyValueAllowUsers { Users = new TLVector<int>() };
                Disallowed = disallowed ?? new TLPrivacyValueDisallowUsers { Users = new TLVector<int>() };
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

        private TLPrivacyValueAllowUsers _allowed;
        public TLPrivacyValueAllowUsers Allowed
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

        private TLPrivacyValueDisallowUsers _disallowed;
        public TLPrivacyValueDisallowUsers Disallowed
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

        protected override void BeginOnUIThread(Action action)
        {
            // This is somehow needed because this viewmodel requires a Dispatcher
            // in some situations where base one might be null.
            Execute.BeginOnUIThread(action);
        }
    }

    public enum PrivacyValue
    {
        AllowAll,
        AllowContacts,
        DisallowAll
    }
}
