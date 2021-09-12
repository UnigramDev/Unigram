using System.Collections.Generic;
using System.Linq;
using Telegram.Td.Api;
using Unigram.Controls;
using Unigram.Controls.Cells;
using Unigram.Services;
using Windows.UI.Xaml.Controls;

namespace Unigram.Views.Popups
{
    public sealed partial class ChatThemePopup : ContentPopup
    {
        public ChatThemePopup(IProtoService protoService, string selectedTheme)
        {
            InitializeComponent();

            Title = Strings.Resources.SelectTheme;
            PrimaryButtonText = Strings.Resources.ChatApplyTheme;
            SecondaryButtonText = Strings.Resources.Cancel;

            Initialize(protoService, selectedTheme);
        }

        private async void Initialize(IProtoService protoService, string selectedTheme)
        {
            var response = await protoService.GetChatThemesAsync();
            if (response != null)
            {
                var items = new List<ChatTheme>(response);
                items.Insert(0, new ChatTheme("\u274C", null, null));

                List.ItemsSource = items;
                List.SelectedItem = string.IsNullOrEmpty(selectedTheme) ? items[0] : items.FirstOrDefault(x => x.Name == selectedTheme);
            }
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
                cell.Update(theme);
            }
        }

        private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (List.SelectedItem is ChatTheme theme && theme.LightSettings == null)
            {
                PrimaryButtonText = Strings.Resources.ChatResetTheme;
            }
            else
            {
                PrimaryButtonText = Strings.Resources.ChatApplyTheme;
            }
        }
    }
}
