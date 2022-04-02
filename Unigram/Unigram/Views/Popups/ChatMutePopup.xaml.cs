using System;
using Unigram.Common;
using Unigram.Controls;
using Windows.UI.Xaml.Controls;

namespace Unigram.Views.Popups
{
    public sealed partial class ChatMutePopup : ContentPopup
    {
        public ChatMutePopup(int mutedFor)
        {
            InitializeComponent();

            Title = Strings.Resources.MuteForAlert;
            PrimaryButtonText = Strings.Resources.OK;
            SecondaryButtonText = Strings.Resources.Cancel;

            var duration = TimeSpan.FromSeconds(mutedFor);

            DaysPicker.Value = duration.Days;
            DaysPicker.Header = Locale.Declension("Days", duration.Days, false);

            HoursPicker.Value = duration.Hours;
            HoursPicker.Header = Locale.Declension("Hours", duration.Hours, false);
        }

        public int Value { get; set; }

        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            var duration = new TimeSpan(DaysPicker.Value, HoursPicker.Value, 0, 0);
            Value = (int)duration.TotalSeconds;
        }

        private void ContentDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
        }

        private void DaysPicker_ValueChanged(LoopingPicker sender, LoopingPickerValueChangedEventArgs args)
        {
            DaysPicker.Header = Locale.Declension("Days", args.NewValue, false);
        }

        private void HoursPicker_ValueChanged(LoopingPicker sender, LoopingPickerValueChangedEventArgs args)
        {
            HoursPicker.Header = Locale.Declension("Hours", args.NewValue, false);
        }
    }
}
