//
// Copyright Fela Ameghino 2015-2025
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Collections.Generic;
using Windows.Storage;

namespace Telegram.Services.Settings
{
    public partial class TranslateSettings : SettingsServiceBase
    {
        public TranslateSettings(ApplicationDataContainer container)
            : base(container)
        {

        }

        private bool? _messages;
        public bool Messages
        {
            get => _messages ??= GetValueOrDefault("IsTranslateEnabled", false);
            set => AddOrUpdateValue(ref _messages, "IsTranslateEnabled", value);
        }

        private bool? _chats;
        public bool Chats
        {
            get => _chats ??= GetValueOrDefault("IsTranslateAllEnabled", true);
            set => AddOrUpdateValue(ref _chats, "IsTranslateAllEnabled", value);
        }

        private string _to;
        public string To
        {
            get => _to ??= GetValueOrDefault("TranslateTo", LocaleService.Current.Id);
            set => AddOrUpdateValue(ref _to, "TranslateTo", value);
        }

        private HashSet<string> _doNot;
        public HashSet<string> DoNot
        {
            get
            {
                _doNot ??= GetDoNot();
                return new HashSet<string>(_doNot);
            }
            set
            {
                _doNot = value;
                AddOrUpdateValue("DoNotTranslate", value?.Count > 0 ? string.Join(';', value) : null);
            }
        }

        private HashSet<string> GetDoNot()
        {
            var value = GetValueOrDefault<string>("DoNotTranslate", null);
            if (value == null)
            {
                return new HashSet<string>();
            }

            var split = value.Split(';', StringSplitOptions.RemoveEmptyEntries);
            return new HashSet<string>(split);
        }
    }
}
