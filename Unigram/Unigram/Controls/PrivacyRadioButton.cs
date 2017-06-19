using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unigram.ViewModels.Settings;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

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
            get { return (PrivacyValue)GetValue(TypeProperty); }
            set { SetValue(TypeProperty, value); }
        }

        public static readonly DependencyProperty TypeProperty =
            DependencyProperty.Register("Type", typeof(PrivacyValue), typeof(PrivacyRadioButton), new PropertyMetadata(default(PrivacyValue)));

        #endregion

        #region Value

        public PrivacyValue Value
        {
            get { return (PrivacyValue)GetValue(ValueProperty); }
            set { SetValue(ValueProperty, value); }
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
