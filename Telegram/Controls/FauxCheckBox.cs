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
    // TODO: CheckBoxEx?
    public class FauxCheckBox : CheckBox
    {
        private UIElement Chevron;

        protected override void OnApplyTemplate()
        {
            if (IsChevronVisible is false)
            {
                Chevron = GetTemplateChild(nameof(Chevron)) as UIElement;
                Chevron.Opacity = 0;
            }

            base.OnApplyTemplate();
        }

        protected override void OnToggle()
        {
            if (IsFaux)
            {
                return;
            }

            base.OnToggle();
        }

        #region IsFaux

        public bool IsFaux
        {
            get { return (bool)GetValue(IsFauxProperty); }
            set { SetValue(IsFauxProperty, value); }
        }

        public static readonly DependencyProperty IsFauxProperty =
            DependencyProperty.Register("IsFaux", typeof(bool), typeof(FauxCheckBox), new PropertyMetadata(false));

        #endregion

        #region IsChevronVisible

        public bool IsChevronVisible
        {
            get { return (bool)GetValue(IsChevronVisibleProperty); }
            set { SetValue(IsChevronVisibleProperty, value); }
        }

        public static readonly DependencyProperty IsChevronVisibleProperty =
            DependencyProperty.Register("IsChevronVisible", typeof(bool), typeof(FauxCheckBox), new PropertyMetadata(true, OnChevronVisibleChanged));

        private static void OnChevronVisibleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var sender = d as FauxCheckBox;
            if (sender?.Chevron != null || (bool)e.NewValue)
            {
                sender.Chevron ??= sender.GetTemplateChild(nameof(sender.Chevron)) as UIElement;

                if (sender.Chevron != null)
                {
                    sender.Chevron.Opacity = (bool)e.NewValue ? 1 : 0;
                }
            }
        }

        #endregion
    }
}
