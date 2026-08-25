//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

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

        public EmojiSettings()
            : base("Emoji")
        {
        }

        public bool HasSkinTone(EmojiSkinData data)
        {
            return _container.ContainsKey("Skin" + data.Emoji);
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

    }
}
