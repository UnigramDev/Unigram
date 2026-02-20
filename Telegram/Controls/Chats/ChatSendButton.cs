//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Telegram.Controls.Chats
{
    public partial class ChatSendButton : GlyphButton
    {
        private TextBlock ExpiresInLabel;

        public ChatSendButton()
        {
            DefaultStyleKey = typeof(ChatSendButton);
        }

        protected override void OnApplyTemplate()
        {
            ExpiresInLabel = GetTemplateChild(nameof(ExpiresInLabel)) as TextBlock;

            OnSlowModeDelayChanged(SlowModeDelay, SlowModeDelayExpiresIn);
            OnReadOnlyChanged(IsReadOnly);

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
            if (ExpiresInLabel == null)
            {
                return;
            }

            ExpiresInLabel.Text = TimeSpan.FromSeconds(expiresIn).ToString("mm\\:ss");
            VisualStateManager.GoToState(this, expiresIn > 0 ? "ExpiresIn" : "Expired", false);
        }

        #region IsReadOnly

        public bool IsReadOnly
        {
            get { return (bool)GetValue(IsReadOnlyProperty); }
            set { SetValue(IsReadOnlyProperty, value); }
        }

        public static readonly DependencyProperty IsReadOnlyProperty =
            DependencyProperty.Register(nameof(IsReadOnly), typeof(bool), typeof(ChatSendButton), new PropertyMetadata(false, OnReadOnlyChanged));

        private static void OnReadOnlyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((ChatSendButton)d).OnReadOnlyChanged((bool)e.NewValue);
        }

        private void OnReadOnlyChanged(bool newValue)
        {
            VisualStateManager.GoToState(this, newValue ? "ReadOnly" : "NotReadOnly", false);
        }

        #endregion
    }
}
