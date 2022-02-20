using System;
using Windows.Foundation;
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
            _player = GetTemplateChild("ContentPresenter") as LottieView;
            _player.IsLoopingEnabled = false;
            _player.IsCachingEnabled = false;
            _player.IsBackward = ActualTheme == ElementTheme.Dark;
            _player.FrameSize = new Size(24, 24);
            _player.DecodeFrameType = Windows.UI.Xaml.Media.Imaging.DecodePixelType.Logical;
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
