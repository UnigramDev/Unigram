//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Telegram.Controls
{
    public class BadgeControl : Control
    {
        public BadgeControl()
        {
            DefaultStyleKey = typeof(BadgeControl);
        }

        protected override void OnApplyTemplate()
        {
            if (IsUnmuted is false)
            {
                OnIsUnmutedChanged(false, true);
            }

            base.OnApplyTemplate();
        }

        #region Text

        public string Text
        {
            get { return (string)GetValue(TextProperty); }
            set { SetValue(TextProperty, value); }
        }

        public static readonly DependencyProperty TextProperty =
            DependencyProperty.Register("Text", typeof(string), typeof(BadgeControl), new PropertyMetadata(string.Empty));

        #endregion

        #region IsUnmuted

        public bool IsUnmuted
        {
            get { return (bool)GetValue(IsUnmutedProperty); }
            set { SetValue(IsUnmutedProperty, value); }
        }

        public static readonly DependencyProperty IsUnmutedProperty =
            DependencyProperty.Register("IsUnmuted", typeof(bool), typeof(BadgeControl), new PropertyMetadata(true, OnIsUnmutedChanged));

        private static void OnIsUnmutedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((BadgeControl)d).OnIsUnmutedChanged((bool)e.NewValue, (bool)e.OldValue);
        }

        private void OnIsUnmutedChanged(bool newValue, bool oldValue)
        {
            VisualStateManager.GoToState(this, newValue ? "Unmuted" : "Muted", false);
        }

        #endregion
    }
}
