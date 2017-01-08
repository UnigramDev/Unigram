using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml;

namespace Unigram.Triggers
{
    public class BooleanTrigger : StateTriggerBase
    {
        public bool Value
        {
            get { return (bool)GetValue(ValueProperty); }
            set { SetValue(ValueProperty, value); }
        }

        public static readonly DependencyProperty ValueProperty =
            DependencyProperty.Register("Value", typeof(bool), typeof(BooleanTrigger), new PropertyMetadata(false, OnValueChanged));

        private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((BooleanTrigger)d).OnValueChanged((bool)e.NewValue, (bool)e.OldValue);
        }

        private void OnValueChanged(bool newValue, bool oldValue)
        {
            SetActive(newValue);
        }
    }

    public class BooleanNegationTrigger : StateTriggerBase
    {
        public bool Value
        {
            get { return (bool)GetValue(ValueProperty); }
            set { SetValue(ValueProperty, value); }
        }

        public static readonly DependencyProperty ValueProperty =
            DependencyProperty.Register("Value", typeof(bool), typeof(BooleanNegationTrigger), new PropertyMetadata(true, OnValueChanged));

        private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((BooleanNegationTrigger)d).OnValueChanged((bool)e.NewValue, (bool)e.OldValue);
        }

        private void OnValueChanged(bool newValue, bool oldValue)
        {
            SetActive(!newValue);
        }
    }
}
