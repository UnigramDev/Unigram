//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System.Collections.Generic;
using System.Linq;
using Telegram.Common;
using Telegram.Controls;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Telegram.Views.Popups
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

            Title = type switch
            {
                ChatTtlType.Auto => Strings.AutoDeleteAfteTitle,
                _ => Strings.MessageLifetime
            };

            PrimaryButtonText = Strings.OK;
            SecondaryButtonText = Strings.Cancel;

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
                items.Add(new SettingsOptionItem<int>(option, option == 0 ? Strings.ShortMessageLifetimeForever : Locale.FormatTtl(option)));
            }

            _items = items;
            ItemsHost.ItemsSource = items;
            ItemsHost.SelectedIndex = 0;
        }

        public int Value
        {
            get => SelectedItem?.Value ?? 0;
            set => SelectedItem = _items.FirstOrDefault(x => x.Value == value) ?? _items.FirstOrDefault();
        }

        public SettingsOptionItem<int> SelectedItem
        {
            get => ItemsHost.SelectedItem as SettingsOptionItem<int>;
            set => ItemsHost.SelectedItem = value;
        }

        private void ItemsHost_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue || args.Item is not SettingsOptionItem<int> value)
            {
                return;
            }

            args.ItemContainer.Content = value.Text;
            args.ItemContainer.HorizontalContentAlignment = HorizontalAlignment.Center;
            args.Handled = true;
        }

        private void ItemsHost_Loaded(object sender, RoutedEventArgs e)
        {
            var scrollingHost = ItemsHost.GetScrollViewer();
            if (scrollingHost != null)
            {
                scrollingHost.ViewChanged += ItemsHost_ViewChanged;
            }

            ItemsHost_SelectionChanged(sender, null);
        }

        private void ItemsHost_ViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
        {
            if (e.IsIntermediate || sender is not ScrollViewer scrollingHost)
            {
                return;
            }

            var index = (int)(scrollingHost.VerticalOffset / 40);
            if (index >= 0 && index < ItemsHost.Items.Count)
            {
                ItemsHost.SelectionChanged -= ItemsHost_SelectionChanged;
                ItemsHost.SelectedIndex = index;
                ItemsHost.SelectionChanged += ItemsHost_SelectionChanged;
            }
        }

        private void ItemsHost_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var scrollingHost = ItemsHost.GetScrollViewer();
            if (scrollingHost != null && ItemsHost.SelectedIndex != -1)
            {
                if (e == null)
                {
                    scrollingHost.ViewChanged -= ItemsHost_ViewChanged;
                    ItemsHost.ScrollIntoView(ItemsHost.SelectedItem);
                    scrollingHost.UpdateLayout();
                    scrollingHost.ViewChanged += ItemsHost_ViewChanged;
                }

                scrollingHost?.TryChangeView(null, ItemsHost.SelectedIndex * 40, null, e == null);
            }
        }
    }
}
