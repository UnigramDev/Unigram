//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Entities;
using Telegram.ViewModels.Channels;
using Telegram.Views.Popups;
using Windows.Storage.Pickers;

namespace Telegram.Views.Channels
{
    public sealed partial class ChannelCreateStep1Page : HostedPage
    {
        public ChannelCreateStep1ViewModel ViewModel => DataContext as ChannelCreateStep1ViewModel;

        public ChannelCreateStep1Page()
        {
            InitializeComponent();
            Title = Strings.NewChannel;
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
                if (media is StoragePhoto or StorageVideo)
                {
                    var dialog = new EditMediaPopup(media, ImageCropperMask.Ellipse);

                    var confirm = await dialog.ShowAsync();
                    if (confirm == ContentDialogResult.Primary)
                    {
                        ViewModel.EditPhotoCommand.Execute(media);
                    }
                }
                else
                {
                    await ViewModel.ShowPopupAsync(Strings.OpenImageUnsupported, Strings.AppName, Strings.OK);
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

            return PlaceholderImage.GetNameForChat(title);
        }

        private Visibility ConvertPhotoVisibility(string title, BitmapImage preview)
        {
            return !string.IsNullOrWhiteSpace(title) || preview != null ? Visibility.Collapsed : Visibility.Visible;
        }

        #endregion
    }
}
