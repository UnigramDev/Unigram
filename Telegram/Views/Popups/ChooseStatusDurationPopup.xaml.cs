//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System.Collections.Generic;
using System.Linq;
using Telegram.Common;
using Telegram.Controls;
using Windows.UI.Xaml.Controls;

namespace Telegram.Views.Popups
{
    public sealed partial class ChooseStatusDurationPopup : ContentPopup
    {
        private readonly List<SettingsOptionItem<int>> _items;

        public ChooseStatusDurationPopup()
        {
            InitializeComponent();

            Title = Strings.SetEmojiStatusUntilTitle;
            PrimaryButtonText = Strings.SetEmojiStatusUntilButton;
            SecondaryButtonText = Strings.Cancel;

            int[] seconds;
            seconds = new int[]
            {
                    60 * 15,
                    60 * 30,
                    60 * 60,
                    60 * 60 * 2,
                    60 * 60 * 3,
                    60 * 60 * 4,
                    60 * 60 * 8,
                    60 * 60 * 12,
                    60 * 60 * 24,
                    60 * 60 * 24 * 2,
                    60 * 60 * 24 * 3,
                    60 * 60 * 24 * 7,
                    60 * 60 * 24 * 7 * 2,
                    60 * 60 * 24 * 31,
                    60 * 60 * 24 * 31 * 2,
                    60 * 60 * 24 * 31 * 3
            };

            var items = new List<SettingsOptionItem<int>>();

            foreach (var option in seconds)
            {
                items.Add(new SettingsOptionItem<int>(option, Locale.FormatTtl(option)));
            }

            _items = items;
            FieldSeconds.ItemsSource = items;
            FieldSeconds.SelectedIndex = 2;
        }

        public int Value
        {
            get => SelectedItem?.Value ?? 0;
            set => SelectedItem = _items.FirstOrDefault(x => x.Value == value) ?? _items.FirstOrDefault();
        }

        public SettingsOptionItem<int> SelectedItem
        {
            get => FieldSeconds.SelectedItem as SettingsOptionItem<int>;
            set => FieldSeconds.SelectedItem = value;
        }

        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
        }

        private void ContentDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
        }
    }
}
