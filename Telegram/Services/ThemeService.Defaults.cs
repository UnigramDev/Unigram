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
        public static Dictionary<TelegramThemeType, Color> Accents => _accent;

        protected static readonly Dictionary<TelegramThemeType, Color> _accent = new()
        {
            {
                // Tinted: dark theme
                // - shell gets colorized
                // - incoming messages gets colorized
                // - outgoing messages gets colorized
                TelegramThemeType.Tinted, Color.FromArgb(0xFF, 0x52, 0x88, 0xC1)
            },
            {
                // Night: dark theme
                // - shell does not get colorized
                // - incoming messages gets colorized
                // - outgoing messages foreground turns white
                TelegramThemeType.Night, Color.FromArgb(0xFF, 0x25, 0x8D, 0xE5)
            },
            {
                // Day: light theme
                // - shell does not get colorized
                // - incoming messages gets colorized
                // - outgoing messages foreground turns white
                TelegramThemeType.Day, Color.FromArgb(0xFF, 0x40, 0xA7, 0xE3)
            },
            {
                // Classic: light theme
                // - shell does not get colorized
                // - incoming messages gets colorized
                // - outgoing messages gets colorized
                TelegramThemeType.Classic, Color.FromArgb(0xFF, 0x40, 0xA7, 0xE3)
            }
        };

        /// <summary>
        /// What an outgoing bubble takes when its fill is far enough from the accent that the
        /// themed foregrounds no longer sit on it - Android's <c>textColor = MSG_OUT_COLOR_WHITE</c>
        /// branch, key for key.
        /// </summary>
        /// <remarks>
        /// <c>MessageMediaForegroundOutgoing</c> is the one that is not a shade of the text colour:
        /// Android gives <c>outMediaIcon</c> and <c>outFileProgress</c> the fill's first colour, so
        /// the glyph reads as a hole punched in the white circle. The constant here stands in for
        /// that until it is threaded through.
        /// </remarks>
        protected static readonly Dictionary<string, Color> _foregroundOverride = new()
        {
            { "MessageForegroundOutgoing", Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF) },
            { "MessageForegroundLinkOutgoing", Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF) },
            { "MessageSubtleForegroundOutgoing", Color.FromArgb(0xAA, 0xFF, 0xFF, 0xFF) },
            { "MessageSubtleLabelOutgoing", Color.FromArgb(0xAA, 0xFF, 0xFF, 0xFF) },
            { "MessageHeaderForegroundOutgoing", Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF) },
            { "MessageHeaderBorderOutgoing", Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF) },
            { "MessageHeaderBackgroundOutgoing", Color.FromArgb(0x20, 0xFF, 0xFF, 0xFF) },
            { "MessageMediaBackgroundOutgoing", Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF) },
            { "MessageMediaForegroundOutgoing", Color.FromArgb(0xFF, 0x4C, 0x9C, 0xE2) },
            { "MessageReactionBackgroundOutgoing", Color.FromArgb(0x35, 0xFF, 0xFF, 0xFF) },
            { "MessageReactionChosenBackgroundOutgoing", Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF) },
        };

        /// <summary>
        /// The same for a fill light enough to need dark text - Android's
        /// <c>MSG_OUT_COLOR_BLACK</c> branch, which is #212121 rather than pure black.
        /// </summary>
        /// <remarks>
        /// Mirrored from the white table rather than authored: the inversions in particular want
        /// looking at on a real bubble before this ships.
        /// </remarks>
        protected static readonly Dictionary<string, Color> _foregroundOverrideDark = new()
        {
            { "MessageForegroundOutgoing", Color.FromArgb(0xFF, 0x00, 0x00, 0x00) },
            { "MessageForegroundLinkOutgoing", Color.FromArgb(0xFF, 0x00, 0x00, 0x00) },
            { "MessageSubtleForegroundOutgoing", Color.FromArgb(0xAA, 0x00, 0x00, 0x00) },
            { "MessageSubtleLabelOutgoing", Color.FromArgb(0xAA, 0x00, 0x00, 0x00) },
            { "MessageHeaderForegroundOutgoing", Color.FromArgb(0xFF, 0x00, 0x00, 0x00) },
            { "MessageHeaderBorderOutgoing", Color.FromArgb(0xFF, 0x00, 0x00, 0x00) },
            { "MessageHeaderBackgroundOutgoing", Color.FromArgb(0x20, 0x00, 0x00, 0x00) },
            { "MessageMediaBackgroundOutgoing", Color.FromArgb(0xFF, 0x00, 0x00, 0x00) },
            { "MessageMediaForegroundOutgoing", Color.FromArgb(0xFF, 0x4C, 0x9C, 0xE2) },
            { "MessageReactionBackgroundOutgoing", Color.FromArgb(0x35, 0x00, 0x00, 0x00) },
            { "MessageReactionChosenBackgroundOutgoing", Color.FromArgb(0xFF, 0x00, 0x00, 0x00) },
        };

        /// <summary>
        /// The keys the override writes even when the server sent an outbox accent. Android keeps
        /// the rest inside <c>if (accentColor2 == 0)</c>, so the reply line and name stay accent
        /// coloured while the text it quotes still flips - the block ends up mixed on purpose.
        /// </summary>
        protected static readonly HashSet<string> _alwaysOverride = new()
        {
            "MessageForegroundOutgoing"
        };

        /// <summary>
        /// The keys the override table names that paint text or a glyph, so measuring them against
        /// the bubble says something - the rest are chrome, translucent over the fill by design.
        /// </summary>
        protected static readonly string[] _foregroundContrastKeys =
        {
            "MessageForegroundOutgoing",
            "MessageForegroundLinkOutgoing",
            "MessageSubtleForegroundOutgoing",
            "MessageSubtleLabelOutgoing",
            "MessageHeaderForegroundOutgoing",
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
                    { "MessageAccentIncoming", Color.FromArgb(0xFF, 0x52, 0x88, 0xC1) },
                    { "MessageAccentOutgoing", Color.FromArgb(0xFF, 0x52, 0x88, 0xC1) },
                    { "MessageBackgroundIncoming", Color.FromArgb(0xFF, 0x1C, 0x27, 0x33) },
                    { "MessageElevationIncoming", Color.FromArgb(0x29, 0x74, 0x8E, 0xA2) },
                    { "MessageSubtleLabelIncoming", Color.FromArgb(0xFF, 0x7D, 0x8E, 0x98) },
                    { "MessageSubtleGlyphIncoming", Color.FromArgb(0xFF, 0x7D, 0x8E, 0x98) },
                    { "MessageSubtleForegroundOutgoing", Color.FromArgb(0xFF, 0x7D, 0xA8, 0xD3) },
                    { "MessageHeaderForegroundIncoming", Color.FromArgb(0xFF, 0x61, 0xA9, 0xE1) },
                    { "MessageHeaderBorderIncoming", Color.FromArgb(0xFF, 0x53, 0x8E, 0xBD) },
                    { "MessageHeaderBackgroundIncoming", Color.FromArgb(0x20, 0x53, 0x8E, 0xBD) },
                    { "MessageBackgroundOutgoing", Color.FromArgb(0xFF, 0x45, 0x6A, 0x93) },
                    { "MessageElevationOutgoing", Color.FromArgb(0x1D, 0x3A, 0xC3, 0x46) },
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
                    { "PinnedMessageForegroundBrush", Color.FromArgb(0xFF, 0x40, 0xA7, 0xE3) },
                    { "PageHeaderHighlightBrush", Color.FromArgb(0xFF, 0x40, 0xA7, 0xE3) },
                    { "ChatVerifiedBadgeBrush", Color.FromArgb(0xFF, 0x40, 0xA7, 0xE3) },
                    { "ChatLastMessageStateBrush", Color.FromArgb(0xFF, 0x40, 0xA7, 0xE3) },
                    { "ChatFromLabelBrush", Color.FromArgb(0xFF, 0x40, 0xA7, 0xE3) },
                    { "ChatUnreadBadgeBrush", Color.FromArgb(0xFF, 0x40, 0xA7, 0xE3) },
                    //{ "ChatUnreadBadgeMutedBrush", Color.FromArgb(0xFF7D8E98) },
                    //{ "ChatFailedBadgeBrush", Color.FromArgb(0xFFD32F2F) },
                    { "MessageAccentIncoming", Color.FromArgb(0xFF, 0x25, 0x8D, 0xE5) },
                    { "MessageAccentOutgoing", Color.FromArgb(0xFF, 0x25, 0x8D, 0xE5) },
                    { "MessageBackgroundIncoming", Color.FromArgb(0xFF, 0x1C, 0x27, 0x33) },
                    { "MessageElevationIncoming", Color.FromArgb(0x29, 0x74, 0x8E, 0xA2) },
                    { "MessageSubtleLabelIncoming", Color.FromArgb(0xFF, 0x7D, 0x8E, 0x98) },
                    { "MessageSubtleGlyphIncoming", Color.FromArgb(0xFF, 0x7D, 0x8E, 0x98) },
                    { "MessageHeaderForegroundIncoming", Color.FromArgb(0xFF, 0x61, 0xA9, 0xE1) },
                    { "MessageHeaderBorderIncoming", Color.FromArgb(0xFF, 0x53, 0x8E, 0xBD) },
                    { "MessageHeaderBackgroundIncoming", Color.FromArgb(0x20, 0x53, 0x8E, 0xBD) },
                    { "MessageBackgroundOutgoing", Color.FromArgb(0xFF, 0x45, 0x6A, 0x93) },
                    { "MessageElevationOutgoing", Color.FromArgb(0x1D, 0x3A, 0xC3, 0x46) },
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
                    { "PinnedMessageForegroundBrush", Color.FromArgb(0xFF, 0x40, 0xA7, 0xE3) },
                    { "PageHeaderHighlightBrush", Color.FromArgb(0xFF, 0x40, 0xA7, 0xE3) },
                    { "ChatVerifiedBadgeBrush", Color.FromArgb(0xFF, 0x40, 0xA7, 0xE3) },
                    { "ChatLastMessageStateBrush", Color.FromArgb(0xFF, 0x40, 0xA7, 0xE3) },
                    { "ChatFromLabelBrush", Color.FromArgb(0xFF, 0x40, 0xA7, 0xE3) },
                    { "ChatUnreadBadgeBrush", Color.FromArgb(0xFF, 0x40, 0xA7, 0xE3) },
                    //{ "ChatUnreadBadgeMutedBrush", Color.FromArgb(0xFF7D8E98) },
                    //{ "ChatFailedBadgeBrush", Color.FromArgb(0xFFD32F2F) },
                    { "MessageAccentIncoming", Color.FromArgb(0xFF, 0x40, 0xA7, 0xE3) },
                    { "MessageAccentOutgoing", Color.FromArgb(0xFF, 0x40, 0xA7, 0xE3) },
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
            {
                TelegramThemeType.Classic, new Dictionary<string, Color>
                {
                    { "PinnedMessageForegroundBrush", Color.FromArgb(0xFF, 0x40, 0xA7, 0xE3) },
                    { "PageHeaderHighlightBrush", Color.FromArgb(0xFF, 0x40, 0xA7, 0xE3) },
                    { "ChatVerifiedBadgeBrush", Color.FromArgb(0xFF, 0x40, 0xA7, 0xE3) },
                    { "ChatLastMessageStateBrush", Color.FromArgb(0xFF, 0x40, 0xA7, 0xE3) },
                    { "ChatFromLabelBrush", Color.FromArgb(0xFF, 0x40, 0xA7, 0xE3) },
                    { "ChatUnreadBadgeBrush", Color.FromArgb(0xFF, 0x40, 0xA7, 0xE3) },
                    //{ "ChatUnreadBadgeMutedBrush", Color.FromArgb(0xFF7D8E98) },
                    //{ "ChatFailedBadgeBrush", Color.FromArgb(0xFFD32F2F) },
                    { "MessageAccentIncoming", Color.FromArgb(0xFF, 0x40, 0xA7, 0xE3) },
                    { "MessageAccentOutgoing", Color.FromArgb(0xFF, 0x40, 0xA7, 0xE3) },
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
