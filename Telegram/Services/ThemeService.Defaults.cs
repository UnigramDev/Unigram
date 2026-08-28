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
    public partial class ThemeInfoBase
    {
        public static Dictionary<TelegramThemeType, Dictionary<AccentShade, Color>> Accents => _accent;

        protected static readonly Dictionary<TelegramThemeType, Dictionary<AccentShade, Color>> _accent = new()
        {
            {
                TelegramThemeType.Tinted, new Dictionary<AccentShade, Color>
                {
                    { AccentShade.Default, Color.FromArgb(0xFF, 0x52, 0x88, 0xC1) },
                    { AccentShade.Light1, Color.FromArgb(0xFF, 0x58, 0x94, 0xd4) },
                    { AccentShade.Light2, Color.FromArgb(0xFF, 0x72, 0xa1, 0xd3) },
                    { AccentShade.Light3, Color.FromArgb(0xFF, 0x9a, 0xb4, 0xcf) },
                    { AccentShade.Dark1, Color.FromArgb(0xFF, 0x41, 0x7b, 0xb7) },
                    { AccentShade.Dark2, Color.FromArgb(0xFF, 0x3c, 0x6e, 0xa3) },
                    { AccentShade.Dark3, Color.FromArgb(0xFF, 0x35, 0x5d, 0x86) },
                }
            },
            {
                TelegramThemeType.Night, new Dictionary<AccentShade, Color>
                {
                    { AccentShade.Default, Color.FromArgb(0xFF, 0x52, 0x88, 0xC1) },
                    { AccentShade.Light1, Color.FromArgb(0xFF, 0x58, 0x94, 0xd4) },
                    { AccentShade.Light2, Color.FromArgb(0xFF, 0x72, 0xa1, 0xd3) },
                    { AccentShade.Light3, Color.FromArgb(0xFF, 0x9a, 0xb4, 0xcf) },
                    { AccentShade.Dark1, Color.FromArgb(0xFF, 0x41, 0x7b, 0xb7) },
                    { AccentShade.Dark2, Color.FromArgb(0xFF, 0x3c, 0x6e, 0xa3) },
                    { AccentShade.Dark3, Color.FromArgb(0xFF, 0x35, 0x5d, 0x86) },
                }
            },
            {
                TelegramThemeType.Day, new Dictionary<AccentShade, Color>
                {
                    { AccentShade.Default, Color.FromArgb(0xFF, 0x40, 0xA7, 0xE3) },
                    { AccentShade.Light1, Color.FromArgb(0xFF, 0x4d, 0xb3, 0xee) },
                    { AccentShade.Light2, Color.FromArgb(0xFF, 0x6f, 0xba, 0xe6) },
                    { AccentShade.Light3, Color.FromArgb(0xFF, 0x98, 0xc6, 0xe1) },
                    { AccentShade.Dark1, Color.FromArgb(0xFF, 0x29, 0x9c, 0xdf) },
                    { AccentShade.Dark2, Color.FromArgb(0xFF, 0x1e, 0x8f, 0xd1) },
                    { AccentShade.Dark3, Color.FromArgb(0xFF, 0x21, 0x78, 0xaa) },
                }
            }
        };

        protected static readonly Dictionary<TelegramThemeType, Dictionary<string, Color>> _map = new()
        {
            {
                TelegramThemeType.Tinted, new Dictionary<string, Color>
                {
                    { "PageTitleBackgroundBrush", Color.FromArgb(0xFF, 0x15, 0x1D, 0x26) },
                    { "PinnedMessageForegroundBrush", Color.FromArgb(0xFF, 0x52, 0x88, 0xC1) },
                    { "PageHeaderHighlightBrush", Color.FromArgb(0xFF, 0x52, 0x88, 0xC1) },
                    { "PageBackgroundDarkBrush", Color.FromArgb(0xFF, 0x15, 0x1D, 0x26) },
                    { "PinnedMessageBorderBrush", Color.FromArgb(0xFF, 0x15, 0x1D, 0x26) },
                    { "ApplicationPageBackgroundThemeBrush", Color.FromArgb(0xFF, 0x1C, 0x27, 0x33) },
                    { "PageHeaderBackgroundBrush", Color.FromArgb(0xFF, 0x1C, 0x27, 0x33) },
                    { "PageSubHeaderBackgroundBrush", Color.FromArgb(0xFF, 0x1C, 0x27, 0x33) },
                    { "ContentDialogBackground", Color.FromArgb(0xFF, 0x17, 0x1B, 0x21) },
                    { "ContentDialogTopOverlaySolid", Color.FromArgb(0xFF, 0x1C, 0x27, 0x33) },
                    { "SolidBackgroundFillColorBaseBrush", Color.FromArgb(0xFF, 0x1C, 0x27, 0x33) },
                    { "TelegramSeparatorMediumBrush", Color.FromArgb(0xFF, 0x10, 0x17, 0x1E) },
                    { "SystemControlDisabledChromeDisabledLowBrush", Color.FromArgb(0xFF, 0x7D, 0x8E, 0x98) },
                    { "ChatVerifiedBadgeBrush", Color.FromArgb(0xFF, 0x52, 0x88, 0xC1) },
                    { "ChatLastMessageStateBrush", Color.FromArgb(0xFF, 0x52, 0x88, 0xC1) },
                    { "ChatFromLabelBrush", Color.FromArgb(0xFF, 0x52, 0x88, 0xC1) },
                    { "ChatUnreadBadgeBrush", Color.FromArgb(0xFF, 0x52, 0x88, 0xC1) },
                    //{ "ChatUnreadBadgeMutedBrush", Color.FromArgb(0xFF7D8E98) },
                    //{ "ChatFailedBadgeBrush", Color.FromArgb(0xFFD32F2F) },
                    { "MessageBackgroundIncoming", Color.FromArgb(0xFF, 0x1C, 0x27, 0x33) },
                    { "MessageSubtleLabelIncoming", Color.FromArgb(0xFF, 0x7D, 0x8E, 0x98) },
                    { "MessageSubtleGlyphIncoming", Color.FromArgb(0xFF, 0x7D, 0x8E, 0x98) },
                    { "MessageSubtleForegroundOutgoing", Color.FromArgb(0xFF, 0x7D, 0xA8, 0xD3) },
                    { "MessageHeaderForegroundIncoming", Color.FromArgb(0xFF, 0x61, 0xA9, 0xE1) },
                    { "MessageHeaderBorderIncoming", Color.FromArgb(0xFF, 0x53, 0x8E, 0xBD) },
                    { "MessageHeaderBackgroundIncoming", Color.FromArgb(0x20, 0x53, 0x8E, 0xBD) },
                    { "MessageBackgroundOutgoing", Color.FromArgb(0xFF, 0x45, 0x6A, 0x93) },
                    { "MessageSubtleLabelOutgoing", Color.FromArgb(0xFF, 0x91, 0xAF, 0xC8) },
                    { "MessageSubtleGlyphOutgoing", Color.FromArgb(0xFF, 0x86, 0xCA, 0xFF) },
                    { "MessageHeaderForegroundOutgoing", Color.FromArgb(0xFF, 0x90, 0xCB, 0xFF) },
                    { "MessageHeaderBorderOutgoing", Color.FromArgb(0xFF, 0x65, 0xBB, 0xF4) },
                    { "MessageHeaderBackgroundOutgoing", Color.FromArgb(0x20, 0x65, 0xBB, 0xF4) },
                    { "MessageMediaBackgroundIncoming", Color.FromArgb(0xFF, 0x3F, 0x96, 0xD0) },
                    { "MessageMediaBackgroundOutgoing", Color.FromArgb(0xFF, 0x4C, 0x9C, 0xE2) },
                    { "MessageReactionBackgroundOutgoing", Color.FromArgb(0xFF, 0x2B, 0x41, 0x53) },
                    { "MessageReactionForegroundOutgoing", Color.FromArgb(0xFF, 0x7A, 0xC3, 0xF4) },
                    { "MessageReactionChosenBackgroundOutgoing", Color.FromArgb(0xFF, 0x31, 0x8E, 0xE4) },
                    { "MessageReactionBackgroundIncoming", Color.FromArgb(0xFF, 0x3A, 0x47, 0x54) },
                    { "MessageReactionForegroundIncoming", Color.FromArgb(0xFF, 0x16, 0x8D, 0xCD) },
                    { "MessageReactionChosenBackgroundIncoming", Color.FromArgb(0xFF, 0x6E, 0xB2, 0xEE) },
                    { "MessageReactionChosenForegroundIncoming", Color.FromArgb(0xFF, 0x33, 0x39, 0x3F) },
                }
            },
            {
                TelegramThemeType.Night, new Dictionary<string, Color>
                {
                    { "PinnedMessageForegroundBrush", Color.FromArgb(0xFF, 0x52, 0x88, 0xC1) },
                    { "PageHeaderHighlightBrush", Color.FromArgb(0xFF, 0x52, 0x88, 0xC1) },
                    { "ChatVerifiedBadgeBrush", Color.FromArgb(0xFF, 0x52, 0x88, 0xC1) },
                    { "ChatLastMessageStateBrush", Color.FromArgb(0xFF, 0x52, 0x88, 0xC1) },
                    { "ChatFromLabelBrush", Color.FromArgb(0xFF, 0x52, 0x88, 0xC1) },
                    { "ChatUnreadBadgeBrush", Color.FromArgb(0xFF, 0x52, 0x88, 0xC1) },
                    //{ "ChatUnreadBadgeMutedBrush", Color.FromArgb(0xFF7D8E98) },
                    //{ "ChatFailedBadgeBrush", Color.FromArgb(0xFFD32F2F) },
                    { "MessageBackgroundIncoming", Color.FromArgb(0xFF, 0x1C, 0x27, 0x33) },
                    { "MessageSubtleLabelIncoming", Color.FromArgb(0xFF, 0x7D, 0x8E, 0x98) },
                    { "MessageSubtleGlyphIncoming", Color.FromArgb(0xFF, 0x7D, 0x8E, 0x98) },
                    { "MessageHeaderForegroundIncoming", Color.FromArgb(0xFF, 0x61, 0xA9, 0xE1) },
                    { "MessageHeaderBorderIncoming", Color.FromArgb(0xFF, 0x53, 0x8E, 0xBD) },
                    { "MessageHeaderBackgroundIncoming", Color.FromArgb(0x20, 0x53, 0x8E, 0xBD) },
                    { "MessageBackgroundOutgoing", Color.FromArgb(0xFF, 0x45, 0x6A, 0x93) },
                    { "MessageSubtleForegroundOutgoing", Color.FromArgb(0xFF, 0x7D, 0xA8, 0xD3) },
                    { "MessageSubtleLabelOutgoing", Color.FromArgb(0xFF, 0x91, 0xAF, 0xC8) },
                    { "MessageSubtleGlyphOutgoing", Color.FromArgb(0xFF, 0x86, 0xCA, 0xFF) },
                    { "MessageHeaderForegroundOutgoing", Color.FromArgb(0xFF, 0x86, 0xCA, 0xFF) },
                    { "MessageHeaderBorderOutgoing", Color.FromArgb(0xFF, 0x86, 0xCA, 0xFF) },
                    { "MessageHeaderBackgroundOutgoing", Color.FromArgb(0x20, 0x86, 0xCA, 0xFF) },
                    { "MessageMediaBackgroundIncoming", Color.FromArgb(0xFF, 0x3F, 0x96, 0xD0) },
                    { "MessageMediaBackgroundOutgoing", Color.FromArgb(0xFF, 0x4C, 0x9C, 0xE2) },
                    { "MessageReactionBackgroundOutgoing", Color.FromArgb(0xFF, 0x2B, 0x41, 0x53) },
                    { "MessageReactionForegroundOutgoing", Color.FromArgb(0xFF, 0x7A, 0xC3, 0xF4) },
                    { "MessageReactionChosenBackgroundOutgoing", Color.FromArgb(0xFF, 0x31, 0x8E, 0xE4) },
                    { "MessageReactionBackgroundIncoming", Color.FromArgb(0xFF, 0x3A, 0x47, 0x54) },
                    { "MessageReactionForegroundIncoming", Color.FromArgb(0xFF, 0x16, 0x8D, 0xCD) },
                    { "MessageReactionChosenBackgroundIncoming", Color.FromArgb(0xFF, 0x6E, 0xB2, 0xEE) },
                    { "MessageReactionChosenForegroundIncoming", Color.FromArgb(0xFF, 0x33, 0x39, 0x3F) },
                }
            },
            {
                TelegramThemeType.Day, new Dictionary<string, Color>
                {
                    { "PinnedMessageForegroundBrush", Color.FromArgb(0xFF, 0x52, 0x88, 0xC1) },
                    { "PageHeaderHighlightBrush", Color.FromArgb(0xFF, 0x52, 0x88, 0xC1) },
                    { "ChatVerifiedBadgeBrush", Color.FromArgb(0xFF, 0x52, 0x88, 0xC1) },
                    { "ChatLastMessageStateBrush", Color.FromArgb(0xFF, 0x52, 0x88, 0xC1) },
                    { "ChatFromLabelBrush", Color.FromArgb(0xFF, 0x52, 0x88, 0xC1) },
                    { "ChatUnreadBadgeBrush", Color.FromArgb(0xFF, 0x52, 0x88, 0xC1) },
                    //{ "ChatUnreadBadgeMutedBrush", Color.FromArgb(0xFF7D8E98) },
                    //{ "ChatFailedBadgeBrush", Color.FromArgb(0xFFD32F2F) },
                    { "MessageSubtleLabelIncoming", Color.FromArgb(0xFF, 0x7D, 0x8E, 0x98) },
                    { "MessageSubtleGlyphIncoming", Color.FromArgb(0xFF, 0x7D, 0x8E, 0x98) },
                    { "MessageHeaderForegroundIncoming", Color.FromArgb(0xFF, 0x16, 0x8d, 0xcd) },
                    { "MessageHeaderBorderIncoming", Color.FromArgb(0xFF, 0x53, 0x8E, 0xBD) },
                    { "MessageHeaderBackgroundIncoming", Color.FromArgb(0x20, 0x53, 0x8E, 0xBD) },
                    { "MessageBackgroundOutgoing", Color.FromArgb(0xFF, 0xDE, 0xF1, 0xFD) },
                    { "MessageElevationOutgoing", Color.FromArgb(0x1A, 0x0D, 0x5A, 0x91) },
                    { "MessageSubtleForegroundOutgoing", Color.FromArgb(0xFF, 0x86, 0xA8, 0xC2) },
                    { "MessageSubtleLabelOutgoing", Color.FromArgb(0xFF, 0x91, 0xAF, 0xC8) },
                    { "MessageSubtleGlyphOutgoing", Color.FromArgb(0xFF, 0x86, 0xCA, 0xFF) },
                    { "MessageHeaderForegroundOutgoing", Color.FromArgb(0xFF, 0x16, 0x8D, 0xCD) },
                    { "MessageHeaderBorderOutgoing", Color.FromArgb(0xFF, 0x05, 0xA0, 0xE8) },
                    { "MessageHeaderBackgroundOutgoing", Color.FromArgb(0x20, 0x05, 0xA0, 0xE8) },
                    { "MessageMediaBackgroundIncoming", Color.FromArgb(0xFF, 0x40, 0xA7, 0xE3) },
                    { "MessageMediaBackgroundOutgoing", Color.FromArgb(0xFF, 0x40, 0xA7, 0xE3) },
                    { "MessageReactionBackgroundOutgoing", Color.FromArgb(0xFF, 0xC1, 0xE4, 0xF8) },
                    { "MessageReactionForegroundOutgoing", Color.FromArgb(0xFF, 0x16, 0x8D, 0xCD) },
                    { "MessageReactionChosenBackgroundOutgoing", Color.FromArgb(0xFF, 0x40, 0xA7, 0xE3) },
                }
            },
        };
    }
}
