//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;

namespace Telegram.Services.Settings
{
    public enum StoryPrivacyMode
    {
        Everyone,
        Contacts,
        CloseFriends,
        SelectedContacts
    }

    // What the last story was posted with, so that the next one starts from it.
    public partial class StorySettings : SettingsServiceBase
    {
        public StorySettings(ISettingsStore container)
            : base(container.GetContainer("Stories"))
        {

        }

        private int? _privacy;
        public StoryPrivacyMode Privacy
        {
            get => (StoryPrivacyMode)(_privacy ??= GetValueOrDefault("Privacy", 0));
            set => AddOrUpdateValue(ref _privacy, "Privacy", (int)value);
        }

        public List<long> ExceptFromEveryone
        {
            get => GetUserIds("ExceptFromEveryone");
            set => SetUserIds("ExceptFromEveryone", value);
        }

        public List<long> ExceptFromContacts
        {
            get => GetUserIds("ExceptFromContacts");
            set => SetUserIds("ExceptFromContacts", value);
        }

        public List<long> SelectedContacts
        {
            get => GetUserIds("SelectedContacts");
            set => SetUserIds("SelectedContacts", value);
        }

        private bool? _allowScreenshots;
        public bool AllowScreenshots
        {
            get => _allowScreenshots ??= GetValueOrDefault("AllowScreenshots", true);
            set => AddOrUpdateValue(ref _allowScreenshots, "AllowScreenshots", value);
        }

        private bool? _keep;
        public bool Keep
        {
            get => _keep ??= GetValueOrDefault("Keep", true);
            set => AddOrUpdateValue(ref _keep, "Keep", value);
        }

        // In hours.
        private int? _period;
        public int Period
        {
            get => _period ??= GetValueOrDefault("Period", 24);
            set => AddOrUpdateValue(ref _period, "Period", value);
        }

        // Read once per sheet, so not cached.
        private List<long> GetUserIds(string key)
        {
            var ids = new List<long>();

            var value = GetValueOrDefault<string>(key, null);
            if (string.IsNullOrEmpty(value))
            {
                return ids;
            }

            foreach (var item in value.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (long.TryParse(item, out long id))
                {
                    ids.Add(id);
                }
            }

            return ids;
        }

        private void SetUserIds(string key, List<long> value)
        {
            AddOrUpdateValue(key, value?.Count > 0 ? string.Join(",", value) : string.Empty);
        }
    }
}
