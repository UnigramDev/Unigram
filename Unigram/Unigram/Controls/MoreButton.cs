using Unigram.Assets.Icons;
using Windows.UI;
using Windows.UI.Xaml;

namespace Unigram.Controls
{
    public class MoreButton : BadgeButton
    {
        public MoreButton()
        {
            DefaultStyleKey = typeof(MoreButton);
            IconSource = new More();

            ActualThemeChanged += OnActualThemeChanged;
            ThemeChanged();
        }

        private void OnActualThemeChanged(FrameworkElement sender, object args)
        {
            ThemeChanged();
        }

        private void ThemeChanged()
        {
            if (IconSource != null)
            {
                IconSource.SetColorProperty("Foreground", ActualTheme == ElementTheme.Light ? Colors.Black : Colors.White);
            }
        }
    }
}
