using Microsoft.UI.Xaml.Controls;
using System;
using Unigram.Assets.Animations;
using Windows.UI.Xaml;

namespace Unigram.Controls
{
    public class SwitchThemeButton : SimpleButton
    {
        private AnimatedVisualPlayer _player;

        public SwitchThemeButton()
        {
            DefaultStyleKey = typeof(SwitchThemeButton);
            ActualThemeChanged += OnActualThemeChanged;
        }

        protected override void OnApplyTemplate()
        {
            _player = GetTemplateChild("ContentPresenter") as AnimatedVisualPlayer;
            _player.Source = new SwitchThemeAnimation();
            _player.SetProgress(ActualTheme == ElementTheme.Light ? 1 : 0);
        }

        private async void OnActualThemeChanged(FrameworkElement sender, object args)
        {
            _player.PlaybackRate = ActualTheme == ElementTheme.Light ? 1 : -1;
            await _player.PlayAsync(0, 1, false);
        }
    }
}
