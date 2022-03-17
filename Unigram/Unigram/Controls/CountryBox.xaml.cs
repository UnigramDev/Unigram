using System;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Unigram.Entities;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Unigram.Controls
{
    public sealed partial class CountryBox : Grid
    {
        private Country _country;

        public CountryBox()
        {
            InitializeComponent();
        }

        public int TabIndex
        {
            get => Input.TabIndex;
            set => Input.TabIndex = value;
        }

        public event EventHandler SelectionChanged;

        private void OnTextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput)
            {
                return;
            }

            var source = sender.ItemsSource as Country[];
            var match = Country.All.Where(x => x.DisplayName.Contains(sender.Text, StringComparison.OrdinalIgnoreCase));

            if (source != null && source.SequenceEqual(match))
            {
                return;
            }

            sender.ItemsSource = match.ToArray();
        }

        private void OnSuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
        {
            _country = args.SelectedItem as Country;
            Country = args.SelectedItem as Country;
        }

        private void OnQuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        {
            if (args.ChosenSuggestion is Country country)
            {
                _country = country;
                Country = country;

                SelectionChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        private void OnGotFocus(object sender, RoutedEventArgs e)
        {
            Input.IsSuggestionListOpen = true;
        }

        #region Country

        public Country Country
        {
            get => (Country)GetValue(CountryProperty);
            set => SetValue(CountryProperty, value);
        }

        public static readonly DependencyProperty CountryProperty =
            DependencyProperty.Register("Country", typeof(Country), typeof(CountryBox), new PropertyMetadata(null, OnCountryChanged));

        private static void OnCountryChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((CountryBox)d).OnCountryChanged((Country)e.NewValue, (Country)e.OldValue);
        }

        private void OnCountryChanged(Country newValue, Country oldValue)
        {
            if (newValue?.PhoneCode == oldValue?.PhoneCode)
            {
                return;
            }

            if (newValue?.PhoneCode == _country?.PhoneCode)
            {
                return;
            }

            Input.Text = newValue?.DisplayName ?? string.Empty;

            _country = newValue;
        }

        #endregion
    }
}
