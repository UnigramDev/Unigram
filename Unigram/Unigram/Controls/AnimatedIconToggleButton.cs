using Microsoft.UI.Xaml.Controls;
using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Media;

namespace Unigram.Controls
{
    public class AnimatedIconToggleButton : ToggleButton
    {
        public AnimatedIconToggleButton()
        {
            DefaultStyleKey = typeof(AnimatedIconToggleButton);
            RegisterPropertyChangedCallback(ForegroundProperty, OnForegroundChanged);
        }

        private void OnForegroundChanged(DependencyObject sender, DependencyProperty dp)
        {
            if (Source != null && Foreground is SolidColorBrush foreground)
            {
                Source.SetColorProperty("Color_000000", foreground.Color);
            }
        }

        #region Source

        public IAnimatedVisualSource2 Source
        {
            get { return (IAnimatedVisualSource2)GetValue(SourceProperty); }
            set { SetValue(SourceProperty, value); }
        }

        public static readonly DependencyProperty SourceProperty =
            DependencyProperty.Register("Source", typeof(IAnimatedVisualSource2), typeof(AnimatedIconToggleButton), new PropertyMetadata(null, OnSourceChanged));

        private static void OnSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var sender = d as AnimatedIconToggleButton;
            var newValue = e.NewValue as IAnimatedVisualSource2;

            if (newValue != null && sender?.Foreground is SolidColorBrush foreground)
            {
                newValue.SetColorProperty("Color_000000", foreground.Color);
            }
        }

        #endregion

        #region IsOneWay

        public bool IsOneWay
        {
            get => (bool)GetValue(IsOneWayProperty);
            set => SetValue(IsOneWayProperty, value);
        }

        public static readonly DependencyProperty IsOneWayProperty =
            DependencyProperty.Register("IsOneWay", typeof(bool), typeof(AnimatedIconToggleButton), new PropertyMetadata(true));

        #endregion

        protected override void OnToggle()
        {
            if (IsOneWay)
            {
                var binding = GetBindingExpression(IsCheckedProperty);
                if (binding != null && binding.ParentBinding.Mode == BindingMode.TwoWay)
                {
                    base.OnToggle();
                }
            }
            else
            {
                base.OnToggle();
            }
        }
    }
}
