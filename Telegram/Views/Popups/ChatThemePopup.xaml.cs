//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System.Collections.Generic;
using System.Linq;
using Telegram.Controls;
using Telegram.Controls.Cells;
using Telegram.Services;
using Telegram.Td.Api;
using Windows.UI.Xaml.Controls;

namespace Telegram.Views.Popups
{
    public sealed partial class ChatThemePopup : ContentPopup
    {
        private readonly IClientService _clientService;

        public ChatThemePopup(IClientService clientService, string selectedTheme)
        {
            InitializeComponent();

            _clientService = clientService;

            Title = Strings.SelectTheme;
            PrimaryButtonText = Strings.ChatApplyTheme;
            SecondaryButtonText = Strings.Cancel;

            var items = new List<ChatTheme>(clientService.GetChatThemes());
            items.Insert(0, new ChatTheme("\u274C", null, null));

            List.ItemsSource = items;
            List.SelectedItem = string.IsNullOrEmpty(selectedTheme) ? items[0] : items.FirstOrDefault(x => x.Name == selectedTheme);
        }

        public string ThemeName => List.SelectedItem is ChatTheme theme && theme.LightSettings != null ? theme.Name : string.Empty;

        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
        }

        private void ContentDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
        }

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }

            var theme = args.Item as ChatTheme;
            var cell = args.ItemContainer.ContentTemplateRoot as ChatThemeCell;

            if (cell != null && theme != null)
            {
                cell.Update(_clientService, theme);
            }
        }

        private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (List.SelectedItem is ChatTheme theme && theme.LightSettings == null)
            {
                PrimaryButtonText = Strings.ChatResetTheme;
            }
            else
            {
                PrimaryButtonText = Strings.ChatApplyTheme;
            }
        }
    }
}
