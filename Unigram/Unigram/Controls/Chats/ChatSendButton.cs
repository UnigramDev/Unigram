using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Unigram.Controls.Chats
{
    public class ChatSendButton : GlyphButton
    {
        private TextBlock _expiresInLabel;

        public ChatSendButton()
        {
            DefaultStyleKey = typeof(ChatSendButton);
        }

        protected override void OnApplyTemplate()
        {
            _expiresInLabel = GetTemplateChild("ExpiresInLabel") as TextBlock;

            OnSlowModeDelayChanged(SlowModeDelay, SlowModeDelayExpiresIn);

            base.OnApplyTemplate();
        }

        #region SlowModeDelay

        public int SlowModeDelay
        {
            get { return (int)GetValue(SlowModeDelayProperty); }
            set { SetValue(SlowModeDelayProperty, value); }
        }

        public static readonly DependencyProperty SlowModeDelayProperty =
            DependencyProperty.Register("SlowModeDelay", typeof(int), typeof(ChatSendButton), new PropertyMetadata(0, OnSlowModeDelayChanged));

        private static void OnSlowModeDelayChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((ChatSendButton)d).OnSlowModeDelayChanged((int)e.NewValue, ((ChatSendButton)d).SlowModeDelayExpiresIn);
        }

        #endregion

        #region SlowModeDelayExpiresIn

        public double SlowModeDelayExpiresIn
        {
            get { return (double)GetValue(SlowModeDelayExpiresInProperty); }
            set { SetValue(SlowModeDelayExpiresInProperty, value); }
        }

        public static readonly DependencyProperty SlowModeDelayExpiresInProperty =
            DependencyProperty.Register("SlowModeDelayExpiresIn", typeof(double), typeof(ChatSendButton), new PropertyMetadata(0d, OnSlowModeDelayExpiresInChanged));

        private static void OnSlowModeDelayExpiresInChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((ChatSendButton)d).OnSlowModeDelayChanged(((ChatSendButton)d).SlowModeDelay, (double)e.NewValue);
        }

        #endregion

        private void OnSlowModeDelayChanged(int delay, double expiresIn)
        {
            if (_expiresInLabel == null)
            {
                return;
            }

            _expiresInLabel.Text = TimeSpan.FromSeconds(expiresIn).ToString("mm\\:ss");
            VisualStateManager.GoToState(this, expiresIn > 0 ? "ExpiresIn" : "Expired", false);
        }
    }
}
