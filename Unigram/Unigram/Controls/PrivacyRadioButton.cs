//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Unigram.ViewModels.Settings;

namespace Unigram.Controls
{
    public class PrivacyRadioButton : RadioButton
    {
        public PrivacyRadioButton()
        {
            Click += OnClick;
        }

        private void OnClick(object sender, RoutedEventArgs e)
        {
            if (Value != Type)
            {
                Value = Type;
            }
        }

        #region Type

        public PrivacyValue Type
        {
            get => (PrivacyValue)GetValue(TypeProperty);
            set => SetValue(TypeProperty, value);
        }

        public static readonly DependencyProperty TypeProperty =
            DependencyProperty.Register("Type", typeof(PrivacyValue), typeof(PrivacyRadioButton), new PropertyMetadata(default(PrivacyValue)));

        #endregion

        #region Value

        public PrivacyValue Value
        {
            get => (PrivacyValue)GetValue(ValueProperty);
            set => SetValue(ValueProperty, value);
        }

        public static readonly DependencyProperty ValueProperty =
            DependencyProperty.Register("Value", typeof(PrivacyValue), typeof(PrivacyRadioButton), new PropertyMetadata(default(PrivacyValue), OnValueChanged));

        private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((PrivacyRadioButton)d).OnValueChanged();
        }

        #endregion

        private void OnValueChanged()
        {
            if (Type == Value)
            {
                IsChecked = true;
            }
            else
            {
                ClearValue(IsCheckedProperty);
            }
        }
    }
}
