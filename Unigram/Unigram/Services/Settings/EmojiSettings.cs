using System.Collections.Generic;
using System.Linq;
using System.Text;
using Unigram.Common;

namespace Unigram.Services.Settings
{
    public class EmojiSettings : SettingsServiceBase
    {
        private readonly string[] _modifiers = new string[]
        {
            "\uD83C\uDFFB" /* emoji modifier fitzpatrick type-1-2 */,
            "\uD83C\uDFFC" /* emoji modifier fitzpatrick type-3 */,
            "\uD83C\uDFFD" /* emoji modifier fitzpatrick type-4 */,
            "\uD83C\uDFFE" /* emoji modifier fitzpatrick type-5 */,
            "\uD83C\uDFFF" /* emoji modifier fitzpatrick type-6 */
        };

        private Dictionary<string, int> _emojiUseHistory = new Dictionary<string, int>();
        private List<string> _recentEmoji = new List<string>();
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
                LoadRecentEmoji();
                return _recentEmoji;
            }
        }


        public EmojiData[] GetRecentEmoji()
        {
            LoadRecentEmoji();
            return _recentEmoji.Select(x => new EmojiData(x)).ToArray();
        }

        public void AddRecentEmoji(string code)
        {
            foreach (var modifier in _modifiers)
            {
                code = code.Replace(modifier, string.Empty);
            }

            _emojiUseHistory.TryGetValue(code, out int count);

            if (count == 0 && _emojiUseHistory.Count >= MAX_RECENT_EMOJI_COUNT)
            {
                var emoji = _recentEmoji[_recentEmoji.Count - 1];
                _emojiUseHistory.Remove(emoji);
                _recentEmoji[_recentEmoji.Count - 1] = code;
            }

            _emojiUseHistory[code] = ++count;
        }

        public void SortRecentEmoji()
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

        public void SaveRecentEmoji()
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

        public void LoadRecentEmoji()
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

                if (_emojiUseHistory.IsEmpty())
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
