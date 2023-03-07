//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.UI.Xaml.Controls;
using System;
using Unigram.Common;
using Unigram.Controls;

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
            DaysPicker.Header = string.Format(Locale.Declension("Days", duration.Days, false), string.Empty).Trim();

            HoursPicker.Value = duration.Hours;
            HoursPicker.Header = string.Format(Locale.Declension("Hours", duration.Hours, false), string.Empty).Trim();
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
            DaysPicker.Header = string.Format(Locale.Declension("Days", args.NewValue, false), string.Empty).Trim();
        }

        private void HoursPicker_ValueChanged(LoopingPicker sender, LoopingPickerValueChangedEventArgs args)
        {
            HoursPicker.Header = string.Format(Locale.Declension("Hours", args.NewValue, false), string.Empty).Trim();
        }
    }
}
