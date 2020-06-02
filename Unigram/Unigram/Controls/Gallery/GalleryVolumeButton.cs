using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Unigram.Controls.Gallery
{
    public class GalleryVolumeButton : Button
    {
        public GalleryVolumeButton()
        {
            DefaultStyleKey = typeof(GalleryVolumeButton);
        }

        #region Value

        public double Value
        {
            get { return (double)GetValue(ValueProperty); }
            set { SetValue(ValueProperty, value); }
        }

        public static readonly DependencyProperty ValueProperty =
            DependencyProperty.Register("Value", typeof(double), typeof(GalleryVolumeButton), new PropertyMetadata(1d, OnValueChanged));

        private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((GalleryVolumeButton)d).OnValueChanged((double)e.NewValue, (double)e.OldValue);
        }

        #endregion

        private void OnValueChanged(double newValue, double oldValue)
        {
            var range = 1d / 3d;
            if (newValue > 0 && newValue < range)
            {
                VisualStateManager.GoToState(this, "OneThird", false);
            }
            else if (newValue >= range && newValue < range * 2)
            {
                VisualStateManager.GoToState(this, "TwoThirds", false);
            }
            else if (newValue >= range * 2)
            {
                VisualStateManager.GoToState(this, "Maximum", false);
            }
            else
            {
                VisualStateManager.GoToState(this, "Muted", false);
            }
        }
    }
}
