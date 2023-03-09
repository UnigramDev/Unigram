//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Controls.Chats;
using Unigram.Services;
using Unigram.Services.Settings;
using Unigram.ViewModels;
using Unigram.ViewModels.Drawers;
using Windows.UI.Xaml.Controls;

namespace Unigram.Views.Popups
{
    public sealed partial class CreateChatPhotoPopup : ContentPopup
    {
        public CreateChatPhotoViewModel ViewModel => DataContext as CreateChatPhotoViewModel;

        public CreateChatPhotoPopup(TaskCompletionSource<object> completion)
        {
            InitializeComponent();

            _completion = completion;

            PrimaryButtonText = Strings.Resources.Save;
            SecondaryButtonText = Strings.Resources.Cancel;
        }

        private readonly TaskCompletionSource<object> _completion;

        private void OnClosing(ContentDialog sender, ContentDialogClosingEventArgs args)
        {
            UnloadAtIndex(0);
            UnloadAtIndex(1);
        }

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }

            var wallpaper = args.Item as Background;
            var content = args.ItemContainer.ContentTemplateRoot as ChatBackgroundRenderer;

            content.UpdateSource(ViewModel.ClientService, wallpaper, true);
        }

        private void Emojis_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is EmojiData emoji)
            {

            }
            else if (e.ClickedItem is StickerViewModel sticker)
            {
                Stickers_ItemClick(sticker);
            }
        }

        private void Stickers_ItemClick(Sticker obj)
        {
            ViewModel.SelectedForeground = obj;
        }

        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            ViewModel.Completion = _completion;
        }

        private void ContentDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
        }

        private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender == Navigation && Navigation.SelectedIndex >= 0)
            {
                Pivot.SelectedIndex = Navigation.SelectedIndex;
            }
            else
            {
                Navigation.SelectedIndex = Pivot.SelectedIndex;
                LoadAtIndex(Pivot.SelectedIndex, /* unsure here */ false);
            }
        }

        private void LoadAtIndex(int index, bool unload)
        {
            if (index == 1)
            {
                if (unload)
                {
                    UnloadAtIndex(0);
                }

                if (StickersRoot == null)
                {
                    FindName(nameof(StickersPanel));
                    StickersRoot.DataContext = StickerDrawerViewModel.GetForCurrentView(ViewModel.SessionId);
                    StickersRoot.ItemClick = Stickers_ItemClick;
                }

                StickersRoot.Activate(null, EmojiSearchType.ChatPhoto);
                SettingsService.Current.Stickers.SelectedTab = StickersTab.Stickers;
            }
            else if (index == 0)
            {
                if (unload)
                {
                    UnloadAtIndex(1);
                }

                if (EmojisRoot == null)
                {
                    FindName(nameof(EmojisPanel));
                    EmojisRoot.DataContext = EmojiDrawerViewModel.GetForCurrentView(ViewModel.SessionId, EmojiDrawerMode.ChatPhoto);
                }

                EmojisRoot.Activate(null);
                SettingsService.Current.Stickers.SelectedTab = StickersTab.Emoji;
            }
        }

        private void UnloadAtIndex(int index)
        {
            if (index == 0 && StickersPanel != null)
            {
                var viewModel = StickersRoot.DataContext as StickerDrawerViewModel;

                StickersRoot.Deactivate();
                UnloadObject(StickersPanel);

                viewModel?.Search(string.Empty, false);
            }
            else if (index == 1 && EmojisPanel != null)
            {
                EmojisRoot.Deactivate();
                UnloadObject(EmojisPanel);
            }
        }

        #region Binding

        private object ConvertForeground(Sticker foreground)
        {
            if (foreground != null)
            {
                double maxSize = 128d / 3 * 2;
                double width = foreground.Width;
                double height = foreground.Height;

                double ratioX = (double)maxSize / width;
                double ratioY = (double)maxSize / height;

                if (ratioX <= ratioY)
                {
                    width = maxSize;
                    height *= ratioX;
                }
                else
                {
                    width *= ratioY;
                    height = maxSize;
                }

                Icon.Width = width;
                Icon.Height = height;
                Icon.FrameSize = (int)Math.Max(width, height);

                Icon.SetSticker(ViewModel.ClientService, foreground);
            }

            return null;
        }

        private bool ConvertEnabled(BackgroundFill background, Sticker foreground)
        {
            return background != null
                && foreground != null;
        }

        #endregion

    }
}
