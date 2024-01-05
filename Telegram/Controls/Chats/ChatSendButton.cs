//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Telegram.Controls.Chats
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
            get => (int)GetValue(SlowModeDelayProperty);
            set => SetValue(SlowModeDelayProperty, value);
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
            get => (double)GetValue(SlowModeDelayExpiresInProperty);
            set => SetValue(SlowModeDelayExpiresInProperty, value);
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
