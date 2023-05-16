using Telegram.Streams;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Telegram.Controls
{
    public class SettingsHeadline : Control
    {
        public SettingsHeadline()
        {
            DefaultStyleKey = typeof(SettingsHeadline);
        }

        protected override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            if (Text.Length > 0)
            {
                GetTemplateChild("Headline");
            }
        }

        #region Text

        public string Text
        {
            get { return (string)GetValue(TextProperty); }
            set { SetValue(TextProperty, value); }
        }

        public static readonly DependencyProperty TextProperty =
            DependencyProperty.Register("Text", typeof(string), typeof(SettingsHeadline), new PropertyMetadata(string.Empty, OnTextChanged));

        private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((SettingsHeadline)d).OnTextChanged((string)e.NewValue);
        }

        private void OnTextChanged(string newValue)
        {
            if (newValue?.Length > 0)
            {
                GetTemplateChild("Headline");
            }
        }

        #endregion

        #region Source

        public AnimatedImageSource Source
        {
            get { return (AnimatedImageSource)GetValue(SourceProperty); }
            set { SetValue(SourceProperty, value); }
        }

        public static readonly DependencyProperty SourceProperty =
            DependencyProperty.Register("Source", typeof(AnimatedImageSource), typeof(SettingsHeadline), new PropertyMetadata(null));

        #endregion

        #region IsLoopingEnabled

        public bool IsLoopingEnabled
        {
            get { return (bool)GetValue(IsLoopingEnabledProperty); }
            set { SetValue(IsLoopingEnabledProperty, value); }
        }

        public static readonly DependencyProperty IsLoopingEnabledProperty =
            DependencyProperty.Register("IsLoopingEnabled", typeof(bool), typeof(SettingsHeadline), new PropertyMetadata(false));

        #endregion
    }
}
