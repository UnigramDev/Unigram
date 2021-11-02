using Microsoft.UI.Xaml;
using System;

namespace Unigram.Triggers
{
    public class EnumTrigger : StateTriggerBase
    {
        private object _value;
        public object Value
        {
            get => _value;
            set => Set(ref _value, value);
        }

        private object _compareTo;
        public object CompareTo
        {
            get => _compareTo;
            set => Set(ref _compareTo, value);
        }

        private void Set(ref object prop, object value)
        {
            prop = value;

            if (_value is Enum && _compareTo is string name)
            {
                var x = _value as Enum;
                var y = Enum.Parse(x.GetType(), name);

                SetActive(x.Equals(y));
            }
        }
    }
}
