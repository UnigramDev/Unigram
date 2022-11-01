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

        public CreateChatPhotoPopup()
        {
            InitializeComponent();

            PrimaryButtonText = Strings.Resources.Save;
            SecondaryButtonText = Strings.Resources.Cancel;
        }

        private void OnClosing(ContentDialog sender, ContentDialogClosingEventArgs args)
        {
            UnloadAtIndex(0);
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
            if (index == 0)
            {
                if (unload)
                {
                    UnloadAtIndex(1);
                }

                if (StickersRoot == null)
                {
                    FindName(nameof(StickersPanel));
                    StickersRoot.DataContext = StickerDrawerViewModel.GetForCurrentView(ViewModel.SessionId);
                    StickersRoot.ItemClick = Stickers_ItemClick;
                }

                StickersRoot.Activate(null);
                SettingsService.Current.Stickers.SelectedTab = StickersTab.Stickers;
            }
            //else if (index == 1)
            //{
            //    if (unload)
            //    {
            //        UnloadAtIndex(0);
            //    }

            //    if (EmojisRoot == null)
            //    {
            //        FindName(nameof(EmojisPanel));
            //        EmojisRoot.DataContext = EmojiDrawerViewModel.GetForCurrentView(ViewModel.SessionId);
            //    }

            //    EmojisRoot.Activate(null);
            //    SettingsService.Current.Stickers.SelectedTab = StickersTab.Emoji;
            //}
        }

        private void UnloadAtIndex(int index)
        {
            if (index == 0 && StickersPanel != null)
            {
                var viewModel = StickersRoot.DataContext as StickerDrawerViewModel;

                StickersRoot.Deactivate();
                UnloadObject(StickersPanel);

                if (viewModel != null)
                {
                    viewModel.Search(string.Empty);
                }
            }
            //else if (index == 1 && EmojisPanel != null)
            //{
            //    EmojisRoot.Deactivate();
            //    UnloadObject(EmojisPanel);
            //}
        }

        #region Binding

        private object ConvertBackground(Background background)
        {
            if (background != null)
            {
                Renderer.UpdateSource(ViewModel.ClientService, background, true);
            }

            return null;
        }

        private object ConvertForeground(Sticker foreground)
        {
            if (foreground != null)
            {
                Icon.SetSticker(ViewModel.ClientService, foreground);
            }

            return null;
        }

        private double ConvertScale(float scale)
        {
            return scale * 100;
        }

        private void ConvertScaleBack(double scale)
        {
            ViewModel.Scale = (float)scale / 100f;
        }

        private bool ConvertEnabled(Background background, Sticker foreground)
        {
            return background != null
                && foreground != null;
        }

        #endregion

    }
}
