using System;
using System.Collections.Generic;
using Telegram.Common;
using Telegram.Controls;
using Windows.UI;

namespace Telegram.Td.Api
{
    public class NameColor
    {
        public NameColor(AccentColor accent)
        {
            DarkThemeColors = accent.DarkThemeColors.Count > 0
                ? new List<Color>()
                : Array.Empty<Color>();

            LightThemeColors = accent.LightThemeColors.Count > 0
                ? new List<Color>()
                : Array.Empty<Color>();

            Populate(accent.DarkThemeColors, DarkThemeColors);
            Populate(accent.LightThemeColors, LightThemeColors);

            BuiltInAccentColorId = accent.BuiltInAccentColorId;
            Id = accent.Id;
        }

        private void Populate(IList<int> source, IList<Color> destination)
        {
            foreach (var item in source)
            {
                destination.Add(item.ToColor());
            }
        }

        public NameColor(int builtInAccentColorId)
        {
            DarkThemeColors = Array.Empty<Color>();
            LightThemeColors = new List<Color>
            {
                PlaceholderImage.GetColor(builtInAccentColorId)
            };

            BuiltInAccentColorId = builtInAccentColorId;
            Id = builtInAccentColorId;
        }

        /// <summary>
        /// The list of 1-3 colors in RGB format, describing the accent color, as expected
        /// to be shown in dark themes.
        /// </summary>
        public IList<Color> DarkThemeColors { get; }

        /// <summary>
        /// The list of 1-3 colors in RGB format, describing the accent color, as expected
        /// to be shown in light themes.
        /// </summary>
        public IList<Color> LightThemeColors { get; }

        /// <summary>
        /// Identifier of a built-in color to use in places, where only one color is needed;
        /// 0-6.
        /// </summary>
        public int BuiltInAccentColorId { get; }

        /// <summary>
        /// Accent color identifier.
        /// </summary>
        public int Id { get; }

    }

}
