using System;
using Windows.Graphics;
using Windows.UI.Xaml;

namespace Unigram.Controls
{
    public class SwitchThemeButton : SimpleButton
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
            _player.FrameSize = new SizeInt32 { Width = 40, Height = 40 };
            _player.Source = new Uri("ms-appx:///Assets/Animations/Sun.tgs");
            _player.SetPosition(ActualTheme == ElementTheme.Dark ? 1 : 0);
        }

        private void OnActualThemeChanged(FrameworkElement sender, object args)
        {
            _player.SetPosition(ActualTheme != ElementTheme.Dark ? 1 : 0);
            _player.Play(ActualTheme != ElementTheme.Dark);
        }
    }
}
