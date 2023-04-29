//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Telegram.Common;
using Telegram.Controls;
using Telegram.Controls.Cells;
using Telegram.ViewModels.BasicGroups;
using Telegram.Views.Popups;
using Windows.Storage.Pickers;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;

namespace Telegram.Views.BasicGroups
{
    public sealed partial class BasicGroupCreateStep1Page : HostedPage
    {
        public BasicGroupCreateStep1ViewModel ViewModel => DataContext as BasicGroupCreateStep1ViewModel;

        public BasicGroupCreateStep1Page()
        {
            InitializeComponent();
            Title = Strings.NewGroup;
        }

        private void Title_Loaded(object sender, RoutedEventArgs e)
        {
            TitleLabel.Focus(FocusState.Keyboard);
        }

        private async void EditPhoto_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var picker = new FileOpenPicker();
                picker.ViewMode = PickerViewMode.Thumbnail;
                picker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
                picker.FileTypeFilter.AddRange(Constants.PhotoTypes);

                var media = await picker.PickSingleMediaAsync();
                if (media != null)
                {
                    var dialog = new EditMediaPopup(media, ImageCropperMask.Ellipse);

                    var confirm = await dialog.ShowAsync();
                    if (confirm == ContentDialogResult.Primary)
                    {
                        ViewModel.EditPhotoCommand.Execute(media);
                    }
                }
            }
            catch { }
        }

        #region Binding

        private object ConvertPhoto(string title, BitmapImage preview)
        {
            if (preview != null)
            {
                return preview;
            }

            return PlaceholderHelper.GetNameForChat(title);
        }

        private Visibility ConvertPhotoVisibility(string title, BitmapImage preview)
        {
            return !string.IsNullOrWhiteSpace(title) || preview != null ? Visibility.Collapsed : Visibility.Visible;
        }

        #endregion

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }
            else if (args.ItemContainer.ContentTemplateRoot is ProfileCell content)
            {
                content.UpdateChat(ViewModel.ClientService, args, OnContainerContentChanging);
            }
        }
    }
}
