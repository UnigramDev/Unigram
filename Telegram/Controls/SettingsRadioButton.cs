//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Windows.UI.Xaml;

namespace Telegram.Controls
{
    public class SettingsRadioButton : FauxRadioButton
    {
        private UIElement DescriptionPresenter;

        public SettingsRadioButton()
        {
            DefaultStyleKey = typeof(SettingsCheckBox);
        }

        protected override void OnApplyTemplate()
        {
            if (ComputedIsDescriptionVisible)
            {
                DescriptionPresenter = GetTemplateChild(nameof(DescriptionPresenter)) as UIElement;
                DescriptionPresenter?.Visibility = Visibility.Visible;
            }

            base.OnApplyTemplate();
        }

        #region Description

        public object Description
        {
            get { return (object)GetValue(DescriptionProperty); }
            set { SetValue(DescriptionProperty, value); }
        }

        public static readonly DependencyProperty DescriptionProperty =
            DependencyProperty.Register("Description", typeof(object), typeof(SettingsRadioButton), new PropertyMetadata(null, OnDescriptionChanged));

        private static void OnDescriptionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var sender = d as SettingsRadioButton;
            if (sender?.DescriptionPresenter != null || sender.ComputedIsDescriptionVisible)
            {
                sender.DescriptionPresenter ??= sender.GetTemplateChild(nameof(sender.DescriptionPresenter)) as UIElement;

                sender.DescriptionPresenter?.Visibility = sender.ComputedIsDescriptionVisible
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }
        }

        #endregion

        #region IsDescriptionVisible

        public bool ComputedIsDescriptionVisible => Description is string description ? !string.IsNullOrEmpty(description) : Description != null;

        #endregion
    }
}
