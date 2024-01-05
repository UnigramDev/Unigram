//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using Telegram.Common;
using Telegram.Controls;
using Windows.UI.Xaml.Controls;

namespace Telegram.Views.Popups
{
    public sealed partial class ChatMutePopup : ContentPopup
    {
        public ChatMutePopup(int mutedFor)
        {
            InitializeComponent();

            Title = Strings.MuteForAlert;
            PrimaryButtonText = Strings.OK;
            SecondaryButtonText = Strings.Cancel;

            var duration = TimeSpan.FromSeconds(mutedFor);

            DaysPicker.Value = duration.Days;
            DaysPicker.Header = string.Format(Locale.Declension(Strings.R.Days, duration.Days, false), string.Empty).Trim();

            HoursPicker.Value = duration.Hours;
            HoursPicker.Header = string.Format(Locale.Declension(Strings.R.Hours, duration.Hours, false), string.Empty).Trim();
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
            DaysPicker.Header = string.Format(Locale.Declension(Strings.R.Days, args.NewValue, false), string.Empty).Trim();
        }

        private void HoursPicker_ValueChanged(LoopingPicker sender, LoopingPickerValueChangedEventArgs args)
        {
            HoursPicker.Header = string.Format(Locale.Declension(Strings.R.Hours, args.NewValue, false), string.Empty).Trim();
        }
    }
}
