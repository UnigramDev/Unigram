using System;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Entities;
using Unigram.ViewModels.Channels;
using Unigram.Views.Popups;
using Windows.Storage.Pickers;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;

namespace Unigram.Views.Channels
{
    public sealed partial class ChannelCreateStep1Page : HostedPage
    {
        public ChannelCreateStep1ViewModel ViewModel => DataContext as ChannelCreateStep1ViewModel;

        public ChannelCreateStep1Page()
        {
            InitializeComponent();
            DataContext = TLContainer.Current.Resolve<ChannelCreateStep1ViewModel>();
        }

        private void Title_Loaded(object sender, RoutedEventArgs e)
        {
            Title.Focus(FocusState.Keyboard);
        }

        private async void EditPhoto_Click(object sender, RoutedEventArgs e)
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

        #region Binding

        private ImageSource ConvertPhoto(string title, BitmapImage preview)
        {
            if (preview != null)
            {
                return preview;
            }

            return PlaceholderHelper.GetNameForChat(title, 64);
        }

        private Visibility ConvertPhotoVisibility(string title, BitmapImage preview)
        {
            return !string.IsNullOrWhiteSpace(title) || preview != null ? Visibility.Collapsed : Visibility.Visible;
        }

        #endregion
    }
}
