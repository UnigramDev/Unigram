using System;
using System.Collections.Generic;
using Telegram.Common;
using Windows.UI;

namespace Telegram.Td.Api
{
    public class ProfileColor
    {
        public ProfileColor(ProfileAccentColor accent)
        {
            DarkThemeColors = new ProfileColors(accent.DarkThemeColors);
            LightThemeColors = new ProfileColors(accent.LightThemeColors);

            Id = accent.Id;
        }

        /// <summary>
        /// The list of 1-3 colors in RGB format, describing the accent color, as expected
        /// to be shown in dark themes.
        /// </summary>
        public ProfileColors DarkThemeColors { get; }

        /// <summary>
        /// The list of 1-3 colors in RGB format, describing the accent color, as expected
        /// to be shown in light themes.
        /// </summary>
        public ProfileColors LightThemeColors { get; }

        /// <summary>
        /// Profile accent color identifier.
        /// </summary>
        public int Id { get; }
    }

    public class ProfileColors
    {
        public ProfileColors(ProfileAccentColors colors)
        {
            StoryColors = Populate(colors.StoryColors);
            BackgroundColors = Populate(colors.BackgroundColors);
            PaletteColors = Populate(colors.PaletteColors);
        }

        private IList<Color> Populate(IList<int> source)
        {
            if (source.Count > 0)
            {
                var destination = new List<Color>();

                foreach (var item in source)
                {
                    destination.Add(item.ToColor());
                }

                return destination;
            }

            return Array.Empty<Color>();
        }

        /// <summary>
        /// The list of 2 colors in RGB format, describing the colors of the gradient to
        ///  be used for the unread active story indicator around profile photo.
        /// </summary>
        public IList<Color> StoryColors { get; }

        /// <summary>
        /// The list of 1-2 colors in RGB format, describing the colors, as expected to be
        /// used for the profile photo background.
        /// </summary>
        public IList<Color> BackgroundColors { get; }

        /// <summary>
        /// The list of 1-2 colors in RGB format, describing the colors, as expected to be
        /// shown in the color palette settings.
        /// </summary>
        public IList<Color> PaletteColors { get; }
    }
}
