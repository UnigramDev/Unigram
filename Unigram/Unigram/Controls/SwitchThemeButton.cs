using System;
using System.Collections.Generic;
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
            _player.ColorReplacements = new Dictionary<int, int>
            {
                { 0xffffff, ActualTheme != ElementTheme.Dark ? 0x000000 : 0xffffff }
            };
            _player.Source = new Uri("ms-appx:///Assets/Animations/Sun.tgs");
        }

        private async void OnActualThemeChanged(FrameworkElement sender, object args)
        {
            if (_player != null)
            {
                await _player.UpdateColorsAsync(new Dictionary<int, int>
                {
                    { 0xffffff, ActualTheme != ElementTheme.Dark ? 0x000000 : 0xffffff }
                });

                _player.Seek(ActualTheme != ElementTheme.Dark ? 1 : 0);
                _player.Play(ActualTheme != ElementTheme.Dark);
            }
        }
    }
}
