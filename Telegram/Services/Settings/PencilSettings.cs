//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System.Collections.Generic;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Entities;
using Windows.UI;

namespace Telegram.Services.Settings
{
    /// <summary>
    /// The media editor's drawing tools: each tool's own weight and colour, the recent colours, and
    /// the look new text takes.
    /// </summary>
    public partial class PencilSettings : SettingsServiceBase
    {
        public static readonly IReadOnlyList<Color> PresetColors = new[]
        {
            ColorEx.FromHex(0xFFFF453A),
            ColorEx.FromHex(0xFFFF8A00),
            ColorEx.FromHex(0xFFFFD60A),
            ColorEx.FromHex(0xFF34C759),
            ColorEx.FromHex(0xFF63E6E2),
            ColorEx.FromHex(0xFF0A84FF),
            ColorEx.FromHex(0xFFBF5AF2),
            ColorEx.FromHex(0xFF000000),
            ColorEx.FromHex(0xFFFFFFFF)
        };

        private static readonly uint[] DefaultRecentColors =
        {
            0xFFD7A07C,
            0xFF7FAFFE,
            0xFFA58FDB,
            0xFFDB95AE,
            0xFFBADC9F
        };

        public PencilSettings(ISettingsStore container)
            : base(container)
        {
        }

        public double GetWeight(PencilTool tool)
        {
            return GetValueOrDefault("PencilWeight" + (int)tool, PencilTools.GetDefaultWeight(tool));
        }

        public void SetWeight(PencilTool tool, double value)
        {
            AddOrUpdateValue("PencilWeight" + (int)tool, value);
        }

        public Color GetColor(PencilTool tool)
        {
            return ColorEx.FromHex(GetValueOrDefault("PencilColor" + (int)tool, ColorEx.ToHex(PencilTools.GetDefaultColor(tool))));
        }

        /// <summary>
        /// Gives <paramref name="tool"/> a colour, and puts it first among the recent ones unless it
        /// is a preset or already there.
        /// </summary>
        public void SetColor(PencilTool tool, Color value)
        {
            AddOrUpdateValue("PencilColor" + (int)tool, ColorEx.ToHex(value));

            var recent = GetRecentColors();
            if (recent.Contains(value) || ((IList<Color>)PresetColors).Contains(value))
            {
                return;
            }

            recent.Insert(0, value);

            for (int i = 0; i < DefaultRecentColors.Length; i++)
            {
                AddOrUpdateValue("PencilRecent" + i, ColorEx.ToHex(recent[i]));
            }
        }

        public PaintTextFrame TextFrame
        {
            get => (PaintTextFrame)GetValueOrDefault("PencilTextFrame", (int)PaintTextFrame.Filled);
            set => AddOrUpdateValue("PencilTextFrame", (int)value);
        }

        public PaintTextAlignment TextAlignment
        {
            get => (PaintTextAlignment)GetValueOrDefault("PencilTextAlignment", (int)PaintTextAlignment.Left);
            set => AddOrUpdateValue("PencilTextAlignment", (int)value);
        }

        public PaintTextFont TextFont
        {
            get => (PaintTextFont)GetValueOrDefault("PencilTextFont", (int)PaintTextFont.Regular);
            set => AddOrUpdateValue("PencilTextFont", (int)value);
        }

        public List<Color> GetRecentColors()
        {
            var colors = new List<Color>(DefaultRecentColors.Length + 1);

            for (int i = 0; i < DefaultRecentColors.Length; i++)
            {
                colors.Add(ColorEx.FromHex(GetValueOrDefault("PencilRecent" + i, (int)DefaultRecentColors[i])));
            }

            return colors;
        }
    }
}
