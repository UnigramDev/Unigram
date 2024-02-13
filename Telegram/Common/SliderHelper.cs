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
            int j = 0;
            for (int i = 0; i < count; i++)
            {
                var label = new TextBlock
                {
                    Text = callback(i),
                    TextAlignment = TextAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    Style = BootStrapper.Current.Resources["InfoCaptionTextBlockStyle"] as Style,
                    FontFamily = BootStrapper.Current.Resources["EmojiThemeFontFamilyWithSymbols"] as FontFamily
                };

                Grid.SetColumn(label, j);

                container.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });

                if (i < count - 1)
                {
                    container.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                }

                container.Children.Add(label);
                j += 2;
            }

            Grid.SetColumnSpan(slider, container.ColumnDefinitions.Count);
        }
    }
}
