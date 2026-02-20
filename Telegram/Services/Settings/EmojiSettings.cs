//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System.Collections.Generic;
using System.Text;
using Telegram.Common;

namespace Telegram.Services.Settings
{
    public partial class EmojiSettings : SettingsServiceBase
    {
        private readonly string[] _modifiers = new string[]
        {
            "\uD83C\uDFFB" /* emoji modifier fitzpatrick type-1-2 */,
            "\uD83C\uDFFC" /* emoji modifier fitzpatrick type-3 */,
            "\uD83C\uDFFD" /* emoji modifier fitzpatrick type-4 */,
            "\uD83C\uDFFE" /* emoji modifier fitzpatrick type-5 */,
            "\uD83C\uDFFF" /* emoji modifier fitzpatrick type-6 */
        };

        private readonly Dictionary<string, int> _emojiUseHistory = new();
        private readonly List<string> _recentEmoji = new();
        private readonly object _recentEmojiLock = new();
        private bool _recentEmojiLoaded;

        private const int MAX_RECENT_EMOJI_COUNT = 35;

        public EmojiSettings()
            : base("Emoji")
        {
        }

        public List<string> RecentEmoji
        {
            get
            {
                lock (_recentEmojiLock)
                {
                    LoadRecentEmoji();
                    return _recentEmoji;
                }
            }
        }

        public bool HasSkinTone(EmojiSkinData data)
        {
            return _container.Values.ContainsKey("Skin" + data.Emoji);
        }

        public void SetEmojiSkinTone(EmojiSkinData data)
        {
            AddOrUpdateValue("Skin" + data.Emoji, ((long)data.Tone1 << 32) | (uint)data.Tone2);
        }

        public EmojiSkinData GetEmojiSkinTone(string code)
        {
            // TODO: does it make sense to cache values for fast access?

            var tones = GetValueOrDefault("Skin" + code, (0L << 32) | 0u);
            int tone1 = (int)(tones >> 32);
            int tone2 = (int)tones;

            if (Emoji.EmojiGroupInternal._doubleSkinEmojis.Contains(code))
            {
                return new EmojiSkinData(code, (EmojiSkinTone)tone1, (EmojiSkinTone)tone2);
            }

            return new EmojiSkinData(code, (EmojiSkinTone)tone1);
        }

        public void AddRecentEmoji(EmojiData emoji)
        {
            AddRecentEmoji(emoji.Emoji);
        }

        public void AddRecentEmoji(string emoji, long customEmojiId)
        {
            AddRecentEmoji($"{emoji};{customEmojiId}");
        }

        private void AddRecentEmoji(string code)
        {
            lock (_recentEmojiLock)
            {
                LoadRecentEmoji();

                _emojiUseHistory.TryGetValue(code, out int count);

                if (count == 0 && _emojiUseHistory.Count >= MAX_RECENT_EMOJI_COUNT)
                {
                    var emoji = _recentEmoji[_recentEmoji.Count - 1];
                    _emojiUseHistory.Remove(emoji);
                    _recentEmoji[_recentEmoji.Count - 1] = code;
                }

                _emojiUseHistory[code] = ++count;

                SortRecentEmoji();
                SaveRecentEmoji();
            }
        }

        private void SortRecentEmoji()
        {
            _recentEmoji.Clear();

            foreach (var entry in _emojiUseHistory)
            {
                _recentEmoji.Add(entry.Key);
            }

            _recentEmoji.Sort((lhs, rhs) =>
            {
                _emojiUseHistory.TryGetValue(lhs, out int count1);
                _emojiUseHistory.TryGetValue(rhs, out int count2);

                if (count1 > count2)
                {
                    return -1;
                }
                else if (count1 < count2)
                {
                    return 1;
                }

                return 0;
            });

            while (_recentEmoji.Count > MAX_RECENT_EMOJI_COUNT)
            {
                _recentEmoji.RemoveAt(_recentEmoji.Count - 1);
            }
        }

        private void SaveRecentEmoji()
        {
            var stringBuilder = new StringBuilder();

            foreach (var entry in _emojiUseHistory)
            {
                if (stringBuilder.Length > 0)
                {
                    stringBuilder.Append(",");
                }

                stringBuilder.Append(entry.Key);
                stringBuilder.Append("=");
                stringBuilder.Append(entry.Value);
            }

            AddOrUpdateValue("RecentEmoji", stringBuilder.ToString());
        }

        public void ClearRecentEmoji()
        {
            AddOrUpdateValue("RecentEmojiFilledDefault", true);

            _emojiUseHistory.Clear();
            _recentEmoji.Clear();
            SaveRecentEmoji();
        }

        private void LoadRecentEmoji()
        {
            if (_recentEmojiLoaded)
            {
                return;
            }

            _recentEmojiLoaded = true;

            try
            {
                _emojiUseHistory.Clear();

                var str = GetValueOrDefault("RecentEmoji", string.Empty);
                if (str != null && str.Length > 0)
                {
                    var args = str.Split(',');
                    foreach (var arg in args)
                    {
                        var args2 = arg.Split('=');
                        _emojiUseHistory[args2[0]] = int.Parse(args2[1]);
                    }
                }

                if (_emojiUseHistory.Count == 0)
                {
                    if (!GetValueOrDefault("RecentEmojiFilledDefault", false))
                    {
                        var newRecent = new string[]
                        {
                            "\uD83D\uDE02", "\uD83D\uDE18", "\u2764", "\uD83D\uDE0D", "\uD83D\uDE0A", "\uD83D\uDE01",
                            "\uD83D\uDC4D", "\u263A", "\uD83D\uDE14", "\uD83D\uDE04", "\uD83D\uDE2D", "\uD83D\uDC8B",
                            "\uD83D\uDE12", "\uD83D\uDE33", "\uD83D\uDE1C", "\uD83D\uDE48", "\uD83D\uDE09", "\uD83D\uDE03",
                            "\uD83D\uDE22", "\uD83D\uDE1D", "\uD83D\uDE31", "\uD83D\uDE21", "\uD83D\uDE0F", "\uD83D\uDE1E",
                            "\uD83D\uDE05", "\uD83D\uDE1A", "\uD83D\uDE4A", "\uD83D\uDE0C", "\uD83D\uDE00", "\uD83D\uDE0B",
                            "\uD83D\uDE06", "\uD83D\uDC4C", "\uD83D\uDE10", "\uD83D\uDE15"
                        };

                        for (int i = 0; i < newRecent.Length; i++)
                        {
                            _emojiUseHistory[newRecent[i]] = newRecent.Length - i;
                        }

                        AddOrUpdateValue("RecentEmojiFilledDefault", true);
                        SaveRecentEmoji();
                    }
                }

                SortRecentEmoji();
            }
            catch { }
        }
    }
}
