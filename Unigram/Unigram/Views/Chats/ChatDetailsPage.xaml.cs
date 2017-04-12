using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Telegram.Api.TL;
using Unigram.Views;
using Unigram.ViewModels;
using Unigram.ViewModels.Chats;
using Unigram.Views.Users;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Telegram.Api.Helpers;
using Windows.Storage.Pickers;
using Unigram.Controls.Views;
using Unigram.Controls;
using Unigram.Common;

namespace Unigram.Views.Chats
{
    public sealed partial class ChatDetailsPage : Page
    {
        public ChatDetailsViewModel ViewModel => DataContext as ChatDetailsViewModel;

        public ChatDetailsPage()
        {
            InitializeComponent();
            DataContext = UnigramContainer.Current.ResolveType<ChatDetailsViewModel>();
        }

        private void Photo_Click(object sender, RoutedEventArgs e)
        {

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
                var dialog = new EditYourPhotoView(file);
                var dialogResult = await dialog.ShowAsync();
                if (dialogResult == ContentDialogBaseResult.OK)
                {
                    ViewModel.EditPhotoCommand.Execute(dialog.Result);
                }
            }

        }

        #region Context menu

        private void MenuFlyout_Opening(object sender, object e)
        {
            var flyout = sender as MenuFlyout;

            foreach (var item in flyout.Items)
            {
                item.Visibility = Visibility.Visible;
            }
        }

        private void ParticipantRemove_Loaded(object sender, RoutedEventArgs e)
        {
            var element = sender as MenuFlyoutItem;
            if (element != null)
            {
                switch (element.DataContext)
                {
                    case TLChatParticipant participant:
                        element.Visibility = participant.InviterId == SettingsHelper.UserId ? Visibility.Visible : Visibility.Collapsed;
                        return;
                    case TLChatParticipantAdmin admin:
                        element.Visibility = admin.InviterId == SettingsHelper.UserId ? Visibility.Visible : Visibility.Collapsed;
                        return;
                }

                element.Visibility = Visibility.Collapsed;
            }
        }

        #endregion

        private void ListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is TLChatParticipantBase participant && participant.User != null)
            {
                ViewModel.NavigationService.Navigate(typeof(UserDetailsPage), participant.User.ToPeer());
            }
        }
    }
}
