//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using Telegram.Common;
using Telegram.Controls;
using Windows.UI;
using Windows.UI.Xaml;

namespace Telegram.Td.Api
{
    public partial class NameColor
    {
        public NameColor(AccentColor accent)
        {
            DarkThemeColors = Populate(accent.DarkThemeColors);
            LightThemeColors = Populate(accent.LightThemeColors);

            BuiltInAccentColorId = accent.BuiltInAccentColorId;
            Id = accent.Id;
            MinChannelChatBoostLevel = accent.MinChannelChatBoostLevel;
        }

        private Vector<Color> Populate(Vector<int> source)
        {
            if (source.Count > 0)
            {
                var destination = new MutableVector<Color>();

                foreach (var item in source)
                {
                    destination.Add(item.ToColor());
                }

                return destination;
            }

            return Array.Empty<Color>();
        }

        public NameColor(int builtInAccentColorId)
        {
            DarkThemeColors = Array.Empty<Color>();
            LightThemeColors = new MutableVector<Color>
            {
                ProfilePictureSourceText.GetColor(builtInAccentColorId)
            };

            BuiltInAccentColorId = builtInAccentColorId;
            Id = builtInAccentColorId;
        }

        public Vector<Color> ForTheme(ElementTheme theme)
        {
            if (theme == ElementTheme.Dark && DarkThemeColors.Count > 0)
            {
                return DarkThemeColors;
            }

            return LightThemeColors;
        }

        /// <summary>
        /// The list of 1-3 colors in RGB format, describing the accent color, as expected
        /// to be shown in dark themes.
        /// </summary>
        public Vector<Color> DarkThemeColors { get; }

        /// <summary>
        /// The list of 1-3 colors in RGB format, describing the accent color, as expected
        /// to be shown in light themes.
        /// </summary>
        public Vector<Color> LightThemeColors { get; }

        /// <summary>
        /// Identifier of a built-in color to use in places, where only one color is needed;
        /// 0-6.
        /// </summary>
        public int BuiltInAccentColorId { get; }

        /// <summary>
        /// Accent color identifier.
        /// </summary>
        public int Id { get; }

        /// <summary>
        /// The minimum chat boost level required to use the color.
        /// </summary>
        public int MinChannelChatBoostLevel { get; }
    }
}
