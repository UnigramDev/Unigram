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
            for (int i = 0; i < container.Children.Count - 1; i++)
            {
                container.Children.RemoveAt(i);
            }

            container.ColumnDefinitions.Clear();
            container.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12, GridUnitType.Pixel) });

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
                    container.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    Grid.SetColumn(label, ++j);
                }
                else
                {
                    container.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.5, GridUnitType.Star) });
                    Grid.SetColumnSpan(label, count + 2);
                }

                container.Children.Add(label);
            }

            container.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12, GridUnitType.Pixel) });

            Grid.SetColumnSpan(slider, container.ColumnDefinitions.Count);
        }
    }
}
