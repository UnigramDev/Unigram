//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System.Collections.Generic;
using Telegram.Services.Settings;
using Windows.UI;

namespace Telegram.Services
{
    internal static partial class ThemeDefaults
    {
        // Telegram's own defaults. The framework material behind them is frozen and packed into
        // ThemeDefaults.g.cs; these stay written out because they are the ones that still change.
        // They lead both key tables, so the packed arrays leave their slots empty and this is the
        // only place the colours appear.
        private static readonly (string Key, ThemeValue Light, ThemeValue Dark)[] _custom =
        {
            ("MessageReactionBackgroundOutgoing", Color.FromArgb(0xFF, 0xD5, 0xF1, 0xC9), Color.FromArgb(0xFF, 0x2B, 0x41, 0x53)),
            ("MessageReactionForegroundOutgoing", Color.FromArgb(0xFF, 0x45, 0xA3, 0x2D), Color.FromArgb(0xFF, 0x7A, 0xC3, 0xF4)),
            ("MessageReactionChosenBackgroundOutgoing", Color.FromArgb(0xFF, 0x5F, 0xBE, 0x67), Color.FromArgb(0xFF, 0x31, 0x8E, 0xE4)),
            ("MessageReactionChosenForegroundOutgoing", Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF), Color.FromArgb(0xFF, 0x33, 0x39, 0x3F)),
            ("MessageReactionBackgroundIncoming", Color.FromArgb(0xFF, 0xE8, 0xF5, 0xFC), Color.FromArgb(0xFF, 0x3A, 0x47, 0x54)),
            ("MessageReactionForegroundIncoming", Color.FromArgb(0xFF, 0x16, 0x8D, 0xCD), Color.FromArgb(0xFF, 0x67, 0xBB, 0xF3)),
            ("MessageReactionChosenBackgroundIncoming", Color.FromArgb(0xFF, 0x40, 0xA7, 0xE3), Color.FromArgb(0xFF, 0x6E, 0xB2, 0xEE)),
            ("MessageReactionChosenForegroundIncoming", Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF), Color.FromArgb(0xFF, 0x33, 0x39, 0x3F)),
            ("ApplicationPageBackgroundThemeBrush", Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF), Color.FromArgb(0xFF, 0x17, 0x17, 0x17)),
            ("ChatPageBackgroundBrush", Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF), Color.FromArgb(0xFF, 0x17, 0x17, 0x17)),
            ("ContentDialogBackground", Color.FromArgb(0xFF, 0xF3, 0xF3, 0xF3), Color.FromArgb(0xFF, 0x15, 0x15, 0x15)),
            ("PageHeaderForegroundBrush", Color.FromArgb(0xFF, 0x00, 0x00, 0x00), Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF)),
            ("PageHeaderHighlightBrush", AccentShade.Default, AccentShade.Default),
            ("PageHeaderDisabledBrush", Color.FromArgb(0x99, 0x00, 0x00, 0x00), Color.FromArgb(0x99, 0xFF, 0xFF, 0xFF)),
            ("PageTitleBackgroundBrush", Color.FromArgb(0xFF, 0xF2, 0xF2, 0xF2), Color.FromArgb(0xFF, 0x2B, 0x2B, 0x2B)),
            ("PageHeaderBackgroundBrush", Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF), Color.FromArgb(0xFF, 0x17, 0x17, 0x17)),
            ("PageHeaderBorderBrush", Color.FromArgb(0xFF, 0xF2, 0xF2, 0xF2), Color.FromArgb(0xFF, 0x1F, 0x1F, 0x1F)),
            ("PageSubHeaderBackgroundBrush", Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF), Color.FromArgb(0xFF, 0x17, 0x17, 0x17)),
            ("PageBackgroundDarkBrush", Color.FromArgb(0xFF, 0xF2, 0xF2, 0xF2), Color.FromArgb(0xFF, 0x1F, 0x1F, 0x1F)),
            ("TelegramSeparatorMediumBrush", Color.FromArgb(0xFF, 0xF2, 0xF2, 0xF2), Color.FromArgb(0xFF, 0x1F, 0x1F, 0x1F)),
            ("TelegramBackgroundAccentBrush", AccentShade.Default, AccentShade.Default),
            ("TelegramForegroundAccentBrush", AccentShade.Default, AccentShade.Default),
            ("PinnedMessageForegroundBrush", AccentShade.Default, AccentShade.Default),
            ("PinnedMessageBorderBrush", Color.FromArgb(0xFF, 0xE6, 0xE6, 0xE6), Color.FromArgb(0xFF, 0x1F, 0x1F, 0x1F)),
            ("TinyButtonBackgroundBrush", AccentShade.Default, AccentShade.Default),
            ("ChatOnlineBadgeBrush", Color.FromArgb(0xFF, 0x00, 0xB1, 0x2C), Color.FromArgb(0xFF, 0x89, 0xDF, 0x9E)),
            ("ChatVerifiedBadgeBrush", AccentShade.Default, AccentShade.Default),
            ("ChatLastMessageStateBrush", AccentShade.Default, AccentShade.Default),
            ("ChatFromLabelBrush", Color.FromArgb(0xFF, 0x3C, 0x7E, 0xB0), AccentShade.Default),
            ("ChatDraftLabelBrush", Color.FromArgb(0xFF, 0xDD, 0x4B, 0x39), Color.FromArgb(0xFF, 0xDD, 0x4B, 0x39)),
            ("ChatUnreadBadgeMutedBrush", Color.FromArgb(0xFF, 0xBB, 0xBB, 0xBB), Color.FromArgb(0xFF, 0x44, 0x44, 0x44)),
            ("ChatUnreadLabelMutedBrush", Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF), Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF)),
            ("ChatFailedBadgeBrush", Color.FromArgb(0xFF, 0xFF, 0x00, 0x00), Color.FromArgb(0xFF, 0xFF, 0x00, 0x00)),
            ("ChatFailedLabelBrush", Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF), Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF)),
            ("ChatUnreadBadgeBrush", AccentShade.Default, AccentShade.Default),
            ("ChatUnreadLabelBrush", Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF), Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF)),
            ("MessagePollCorrectBrush", Color.FromArgb(0xFF, 0x5D, 0xC4, 0x52), Color.FromArgb(0xFF, 0x5D, 0xC4, 0x52)),
            ("MessagePollWrongBrush", Color.FromArgb(0xFF, 0xED, 0x50, 0x50), Color.FromArgb(0xFF, 0xED, 0x50, 0x50)),
        };

        internal static readonly Dictionary<string, int> Slots;

        internal static ThemeLookup Light => new(_lightValues, _lightOrder);

        internal static ThemeLookup Dark => new(_darkValues, _darkOrder);

        // A static constructor rather than a field initializer: initializer order across the two
        // halves of a partial class is the compiler's to pick, and this has to run after the
        // generated arrays exist.
        static ThemeDefaults()
        {
            Slots = new Dictionary<string, int>(Keys.Length);

            for (int i = 0; i < Keys.Length; i++)
            {
                Slots[Keys[i]] = i;
            }

            foreach (var (key, light, dark) in _custom)
            {
                var slot = Slots[key];

                _lightValues[slot] = light.Packed;
                _darkValues[slot] = dark.Packed;
            }
        }
    }
}
