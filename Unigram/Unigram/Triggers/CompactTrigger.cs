using Windows.UI.Xaml;

namespace Unigram.Triggers
{
    public class CompactTrigger : StateTriggerBase
    {
        private const double MinWindowWidth = 501;
        private const double MaxWindowWidth = 820;
        private double _oldWidth;

        public CompactTrigger()
        {
            SetActive(Allow && Window.Current.Bounds.Width >= MinWindowWidth && Window.Current.Bounds.Width < MaxWindowWidth);
            Window.Current.SizeChanged += OnSizeChanged;
        }

        private void OnSizeChanged(object sender, Windows.UI.Core.WindowSizeChangedEventArgs e)
        {
            //if (e.Size.Width != _oldWidth && (e.Size.Width >= MinWindowWidth && _oldWidth < MinWindowWidth) || (e.Size.Width < MinWindowWidth && _oldWidth >= MinWindowWidth))
            {
                SetActive(Allow && e.Size.Width >= MinWindowWidth && e.Size.Width < MaxWindowWidth);
            }

            _oldWidth = e.Size.Width;
        }

        public bool Allow
        {
            get { return (bool)GetValue(AllowProperty); }
            set { SetValue(AllowProperty, value); }
        }

        public static readonly DependencyProperty AllowProperty =
            DependencyProperty.Register("Allow", typeof(bool), typeof(CompactTrigger), new PropertyMetadata(true, OnAllowChanged));

        private static void OnAllowChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((CompactTrigger)d).OnAllowChanged((bool)e.NewValue);
        }

        private void OnAllowChanged(bool newValue)
        {
            SetActive(newValue && Window.Current.Bounds.Width >= MinWindowWidth && Window.Current.Bounds.Width < MaxWindowWidth);
        }
    }
}
