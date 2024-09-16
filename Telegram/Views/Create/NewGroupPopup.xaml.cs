//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Telegram.Common;
using Telegram.Controls;
using Telegram.Controls.Media;
using Telegram.ViewModels.Create;
using Telegram.ViewModels.Drawers;
using Telegram.Views.Popups;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media.Imaging;

namespace Telegram.Views.Create
{
    public sealed partial class NewGroupPopup : ContentPopup
    {
        public NewGroupViewModel ViewModel => DataContext as NewGroupViewModel;

        public NewGroupPopup()
        {
            InitializeComponent();

            //Title = Strings.NewGroup;

            PrimaryButtonText = Strings.OK;
            SecondaryButtonText = Strings.Cancel;
        }

        public override void OnNavigatedTo(object parameter)
        {
            EmojiPanel.DataContext = EmojiDrawerViewModel.Create(ViewModel.SessionId, EmojiDrawerMode.Text);
        }

        private void Title_Loaded(object sender, RoutedEventArgs e)
        {
            TitleLabel.Focus(FocusState.Keyboard);
        }

        #region Binding

        private object ConvertPhoto(string title, BitmapImage preview)
        {
            if (preview != null)
            {
                return preview;
            }
            else if (string.IsNullOrWhiteSpace(title))
            {
                return PlaceholderImage.GetGlyph(Icons.CameraAddFilled);
            }

            return PlaceholderImage.GetNameForChat(title);
        }

        #endregion

        private void Emoji_Click(object sender, RoutedEventArgs e)
        {
            // We don't want to unfocus the text are when the context menu gets opened
            EmojiPanel.ViewModel.Update();
            EmojiFlyout.ShowAt(TitleLabel, new FlyoutShowOptions { ShowMode = FlyoutShowMode.Transient });
        }

        private void Emoji_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is EmojiData emoji)
            {
                EmojiFlyout.Hide();

                var text = TitleLabel.Text;
                var index = TitleLabel.SelectionStart;

                if (TitleLabel.SelectionLength > 0)
                {
                    text = text.Remove(TitleLabel.SelectionStart, TitleLabel.SelectionLength);
                }

                text = text.Insert(index, emoji.Value);

                TitleLabel.Text = text;
                TitleLabel.Focus(FocusState.Programmatic);
                TitleLabel.SelectionStart = index;
            }
        }

        private async void OnPrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            var users = await ChooseChatsPopup.PickUsersAsync(ViewModel.ClientService, ViewModel.NavigationService, Strings.SelectContacts, allowEmptySelection: true);
            if (users == null)
            {
                await this.ShowQueuedAsync(XamlRoot);
                return;
            }

            ViewModel.Create(users);
        }
    }
}
