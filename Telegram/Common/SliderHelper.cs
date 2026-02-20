//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using Telegram.Navigation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace Telegram.Common
{
    public static class SliderHelper
    {
        public static void InitializeTicks(Slider slider, Grid container, int count, Func<int, string> callback)
        {
            container.ColumnDefinitions.Clear();
            container.ColumnDefinitions.Add(12, GridUnitType.Pixel);

            int j = 1;
            for (int i = 0; i < count; i++)
            {
                var label = new TextBlock
                {
                    Text = callback(i),
                    TextAlignment = i == 0 ? TextAlignment.Left : i == count - 1 ? TextAlignment.Right : TextAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    Style = BootStrapper.Current.Resources["InfoCaptionTextBlockStyle"] as Style,
                    FontFamily = BootStrapper.Current.Resources["EmojiThemeFontFamilyWithSymbols"] as FontFamily
                };

                if (i > 0 && i < count - 1)
                {
                    container.ColumnDefinitions.Add(1, GridUnitType.Star);
                    Grid.SetColumn(label, ++j);
                }
                else
                {
                    container.ColumnDefinitions.Add(0.5, GridUnitType.Star);
                    Grid.SetColumnSpan(label, count + 2);
                }

                container.Children.Add(label);
            }

            container.ColumnDefinitions.Add(12, GridUnitType.Pixel);

            Grid.SetColumnSpan(slider, container.ColumnDefinitions.Count);
        }
    }
}
