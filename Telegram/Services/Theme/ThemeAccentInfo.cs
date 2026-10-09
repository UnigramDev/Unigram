//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System.Collections.Generic;
using Telegram.Common;
using Telegram.Navigation;
using Telegram.Services.Settings;
using Telegram.Td.Api;
using Windows.UI;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;

namespace Telegram.Services
{
    public partial class ThemeAccentInfo : ThemeInfoBase
    {
        protected ThemeAccentInfo(TelegramThemeType type, Color accent, Dictionary<string, Color> values, Dictionary<AccentShade, Color> shades)
        {
            Type = type;
            AccentColor = accent;
            Values = values ?? new Dictionary<string, Color>();
            Shades = shades ?? new Dictionary<AccentShade, Color>();

            switch (type)
            {
                case TelegramThemeType.Classic:
                    Parent = TelegramTheme.Light;
                    Name = Strings.ThemeClassic;
                    break;
                case TelegramThemeType.Day:
                    Parent = TelegramTheme.Light;
                    Name = Strings.ThemeDay;
                    break;
                case TelegramThemeType.Night:
                    Parent = TelegramTheme.Dark;
                    Name = Strings.ThemeNight;
                    break;
                case TelegramThemeType.Tinted:
                    Parent = TelegramTheme.Dark;
                    Name = Strings.ThemeDark;
                    break;
            }

            IsOfficial = type != TelegramThemeType.Custom;
        }

        public static ThemeAccentInfo FromSettings(TelegramTheme requested, ThemeSettings settings)
        {
            return FromAccent(ThemeSettingsStore.ToThemeType(settings.BaseTheme, requested), settings.AccentColor.ToColor(), settings.GetOutgoingMessageAccentColor(), settings.OutgoingMessageFill, settings.HasOutgoingMessageAccentColor);
        }

        public static ThemeAccentInfo FromAccent(TelegramThemeType type, Color accent, Color outgoing = default, BackgroundFill outgoingFill = null, bool hasOutgoingAccent = false)
        {
            var color = accent;
            if (color == default)
            {
                color = BootStrapper.Current.UISettings.GetColorValue(UIColorType.Accent);
            }

            var colorizer = ThemeColorizer.FromTheme(type, _accent[type], color);
            var outgoingColorizer = outgoing != default ? ThemeColorizer.FromTheme(type, _accent[type], outgoing) : null;
            var outgoingBackgroundColorizer = outgoingFill is BackgroundFillSolid solid ? ThemeColorizer.FromTheme(type, _accent[type], solid.Color.ToColor()) : null;

            var values = new Dictionary<string, Color>();
            var shades = new Dictionary<AccentShade, Color>();

            foreach (var item in _map[type])
            {
                if (outgoingBackgroundColorizer != null && item.Key == "MessageBackgroundOutgoing")
                {
                    values[item.Key] = outgoingBackgroundColorizer.Colorize(item.Value);
                }
                else if (outgoingColorizer != null && item.Key.EndsWith("Outgoing"))
                {
                    values[item.Key] = outgoingColorizer.Colorize(item.Value);
                }
                else
                {
                    values[item.Key] = colorizer.Colorize(item.Value);
                }
            }

            // Three conditions, the first two as Android's fillAccentColors has them.
            //
            // A. Only a real gradient is a candidate, and only when it sits far enough from the
            //    colour the accent would have produced on its own - otherwise the themed
            //    foregrounds still work and replacing them would be gratuitous.
            // B. Which of the two sets, decided by the fill's perceived brightness.
            // C. A server-sent outbox accent keeps most of the set: only the text follows, so the
            //    reply line and name stay the accent while the message they quote flips. Android
            //    stops there, at provenance - but that says nothing about whether the result can
            //    be read, and #4BB065 over a green fill colorizes the timestamp to #7C907B, which
            //    is 1.06:1 against the gradient. So honour the accent only while it survives on
            //    the colours actually painted.
            var fill = outgoingFill is BackgroundFillFreeformGradient or BackgroundFillGradient
                ? outgoingFill.GetColors()
                : null;

            if (fill != null && fill.Count > 1)
            {
                var useBlackText = ColorEx.UseBlackText(fill);

                // Only a black-text fill can skip the override, whatever the base theme is: it is
                // the one case where the themed foregrounds beat the replacement, being a pale
                // near-copy of the bubble they were authored against. A white-text fill always
                // takes it, because that is what makes a coloured bubble legible at all.
                //
                // The first colour against the bubble the loop above just produced, and against the
                // second - not the fill's colours against each other, which is a different question
                // and the one ColorEx.AreNear answers.
                var shifted = values["MessageBackgroundOutgoing"].ToValue();
                var near = useBlackText
                    && ColorEx.GetColorDistance(fill[0], shifted) <= NearDistance
                    && ColorEx.GetColorDistance(fill[0], fill[1]) <= NearDistance;

                if (!near)
                {
                    var keepAccent = hasOutgoingAccent && IsLegible(values, fill);

                    foreach (var item in useBlackText ? _foregroundOverrideDark : _foregroundOverride)
                    {
                        if (keepAccent && !_alwaysOverride.Contains(item.Key))
                        {
                            continue;
                        }

                        values[item.Key] = item.Value;
                    }

                    if (!keepAccent)
                    {
                        // Inside the same guard as the rest, and the fill's own first colour rather
                        // than the text colour, so the glyph reads as a hole in the circle.
                        values["MessageMediaForegroundOutgoing"] = fill[0].ToColor();
                    }
                }
            }

            for (int i = 0; i < 7; i++)
            {
                shades[(AccentShade)i] = SystemAccentPalette.GetShade(color, (AccentShade)i);
            }

            return new ThemeAccentInfo(type, accent, values, shades);
        }

        /// <summary>
        /// Android's threshold, in its own red-weighted metric.
        /// </summary>
        private const int NearDistance = 35000;

        /// <summary>
        /// Whether the accent-derived foregrounds can be read anywhere over the fill.
        /// </summary>
        private static bool IsLegible(Dictionary<string, Color> values, IReadOnlyList<int> fill)
        {
            foreach (var key in _foregroundContrastKeys)
            {
                // The keys that flip either way decide nothing here - the question is only whether
                // the ones the outbox accent would spare are worth sparing.
                if (_alwaysOverride.Contains(key))
                {
                    continue;
                }

                if (values.TryGetValue(key, out var value)
                    && ColorEx.GetTextContrast(fill, value) < ColorEx.ReadableContrast)
                {
                    return false;
                }
            }

            return true;
        }

        public static Color Colorize(ThemeSettings settings, Color accent, string key)
        {
            return Colorize(ThemeSettingsStore.ToThemeType(settings.BaseTheme, TelegramTheme.Light), accent, key);
        }

        public static Color Colorize(TelegramThemeType type, Color accent, string key)
        {
            var colorizer = ThemeColorizer.FromTheme(type, _accent[type], accent);
            if (_map[type].TryGetValue(key, out Color color))
            {
                return colorizer.Colorize(color);
            }

            var lookup = type == TelegramThemeType.Day ? ThemeIncoming.DefaultLight : ThemeIncoming.DefaultDark;
            return colorizer.Colorize(lookup[key]);
        }

        public override Color AccentColor { get; }

        public TelegramThemeType Type { get; private set; }

        public Dictionary<string, Color> Values { get; protected set; }

        public Dictionary<AccentShade, Color> Shades { get; protected set; }

        public override bool IsOfficial { get; }




        public override Color SelectionColor => Shades[AccentShade.Default];

        public override Color ChatBackgroundColor
        {
            get
            {
                if (Values.TryGetValue("PageBackgroundDarkBrush", out Color color))
                {
                    return color;
                }

                return base.ChatBackgroundColor;
            }
        }

        public override Color ChatBorderColor
        {
            get
            {
                if (Values.TryGetValue("PageHeaderBackgroundBrush", out Color color))
                {
                    return color;
                }

                return base.ChatBorderColor;
            }
        }

        public override Color MessageBackgroundColor
        {
            get
            {
                if (Values.TryGetValue("MessageBackgroundIncoming", out Color color))
                {
                    return color;
                }

                return base.MessageBackgroundColor;
            }
        }

        public override Color MessageBackgroundOutColor
        {
            get
            {
                if (Values.TryGetValue("MessageBackgroundOutgoing", out Color color))
                {
                    return color;
                }

                return base.MessageBackgroundOutColor;
            }
        }

        public static bool IsAccent(TelegramThemeType type)
        {
            return type is TelegramThemeType.Tinted or
                TelegramThemeType.Night or
                TelegramThemeType.Day;
        }
    }
}
