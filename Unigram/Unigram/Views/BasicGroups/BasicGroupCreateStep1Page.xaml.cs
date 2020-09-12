using System;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Entities;
using Unigram.ViewModels.BasicGroups;
using Unigram.Views.Popups;
using Windows.Storage.Pickers;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;

namespace Unigram.Views.BasicGroups
{
    public sealed partial class BasicGroupCreateStep1Page : HostedPage
    {
        public BasicGroupCreateStep1ViewModel ViewModel => DataContext as BasicGroupCreateStep1ViewModel;

        public BasicGroupCreateStep1Page()
        {
            InitializeComponent();
            DataContext = TLContainer.Current.Resolve<BasicGroupCreateStep1ViewModel>();
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

            var file = await picker.PickSingleFileAsync();
            if (file != null)
            {
                var media = await StorageMedia.CreateAsync(file);
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

        private void OnElementPrepared(Microsoft.UI.Xaml.Controls.ItemsRepeater sender, Microsoft.UI.Xaml.Controls.ItemsRepeaterElementPreparedEventArgs args)
        {
            var button = args.Element as Button;
            var content = button.Content as Grid;

            var chat = button.DataContext as Chat;

            var title = content.Children[1] as TextBlock;
            title.Text = ViewModel.ProtoService.GetTitle(chat);

            //if (ViewModel.CacheService.TryGetSupergroup(chat, out Supergroup supergroup))
            //{
            //    var subtitle = content.Children[2] as TextBlock;
            //    subtitle.Text = string.Format("{0}, {1}", BindConvert.Distance(nearby.Distance), Locale.Declension("Members", supergroup.MemberCount));
            //}
            //else
            //{
            //    var subtitle = content.Children[2] as TextBlock;
            //    subtitle.Text = BindConvert.Distance(nearby.Distance);
            //}

            var photo = content.Children[0] as ProfilePicture;
            photo.Source = PlaceholderHelper.GetChat(ViewModel.ProtoService, chat, 36);

            //button.Command = ViewModel.OpenChatCommand;
            //button.CommandParameter = nearby;
        }
    }
}
