//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System.Collections.Generic;
using System.Linq;
using Unigram.Common;
using Unigram.Controls;
using Windows.UI.Xaml.Controls;

namespace Unigram.Views.Popups
{
    public enum ChatTtlType
    {
        Secret,
        Normal,
        Auto
    }

    public sealed partial class ChatTtlPopup : ContentPopup
    {
        private readonly List<SettingsOptionItem<int>> _items;

        public ChatTtlPopup(ChatTtlType type)
        {
            InitializeComponent();

            Title = Strings.Resources.MessageLifetime;
            PrimaryButtonText = Strings.Resources.OK;
            SecondaryButtonText = Strings.Resources.Cancel;

            int[] seconds;
            if (type == ChatTtlType.Secret)
            {
                seconds = new int[]
                {
                    0,
                    1,
                    2,
                    3,
                    4,
                    5,
                    6,
                    7,
                    8,
                    9,
                    10,
                    11,
                    12,
                    13,
                    14,
                    15,
                    30,
                    60,
                    60 * 60,
                    60 * 60 * 24,
                    60 * 60 * 24 * 7
                };
            }
            else if (type == ChatTtlType.Auto)
            {
                seconds = new int[]
                {
                    0,
                    60 * 60 * 24,
                    60 * 60 * 24 * 2,
                    60 * 60 * 24 * 3,
                    60 * 60 * 24 * 4,
                    60 * 60 * 24 * 5,
                    60 * 60 * 24 * 6,
                    60 * 60 * 24 * 7,
                    60 * 60 * 24 * 7 * 2,
                    60 * 60 * 24 * 7 * 3,
                    60 * 60 * 24 * 31,
                    60 * 60 * 24 * 31 * 2,
                    60 * 60 * 24 * 31 * 3,
                    60 * 60 * 24 * 31 * 4,
                    60 * 60 * 24 * 31 * 5,
                    60 * 60 * 24 * 31 * 6,
                    60 * 60 * 24 * 365
                };
            }
            else
            {
                seconds = new int[]
                {
                    0,
                    60 * 60 * 24,
                    60 * 60 * 24 * 7
                };
            }

            var items = new List<SettingsOptionItem<int>>();

            foreach (var option in seconds)
            {
                items.Add(new SettingsOptionItem<int>(option, option == 0 ? Strings.Resources.ShortMessageLifetimeForever : Locale.FormatTtl(option)));
            }

            _items = items;
            FieldSeconds.ItemsSource = items;
            FieldSeconds.SelectedIndex = 0;
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

        public static string ConvertTtl(int value)
        {
            if (value == 0)
            {
                return Strings.Resources.ShortMessageLifetimeForever;
            }

            return Locale.FormatTtl(value);
        }
    }
}
