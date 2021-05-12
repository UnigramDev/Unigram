using System;
using Windows.Graphics;
using Windows.Graphics.Display;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Unigram.Controls
{
    public class SwitchThemeButton : Button
    {
        private LottieView _player;

        public SwitchThemeButton()
        {
            DefaultStyleKey = typeof(SwitchThemeButton);
            ActualThemeChanged += OnActualThemeChanged;
        }

        protected override void OnApplyTemplate()
        {
            var dpi = DisplayInformation.GetForCurrentView().LogicalDpi / 96.0f;
            var size = (int)(24 * dpi);

            _player = GetTemplateChild("ContentPresenter") as LottieView;
            _player.IsLoopingEnabled = false;
            _player.IsCachingEnabled = false;
            _player.IsBackward = ActualTheme == ElementTheme.Dark;
            _player.FrameSize = new SizeInt32 { Width = size, Height = size };
            _player.Source = new Uri("ms-appx:///Assets/Animations/Sun.tgs");
        }

        private void OnActualThemeChanged(FrameworkElement sender, object args)
        {
            if (_player != null)
            {
                _player.Seek(ActualTheme != ElementTheme.Dark ? 1 : 0);
                _player.Play(ActualTheme != ElementTheme.Dark);
            }
        }
    }
}
