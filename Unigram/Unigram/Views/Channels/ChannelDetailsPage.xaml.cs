using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Telegram.Api.TL;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Controls.Views;
using Unigram.ViewModels.Channels;
using Unigram.ViewModels.Chats;
using Unigram.Views.Users;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage.Pickers;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;

namespace Unigram.Views.Channels
{
    public sealed partial class ChannelDetailsPage : Page
    {
        public ChannelDetailsViewModel ViewModel => DataContext as ChannelDetailsViewModel;

        public ChannelDetailsPage()
        {
            InitializeComponent();
            DataContext = UnigramContainer.Current.ResolveType<ChannelDetailsViewModel>();
        }

        private async void Photo_Click(object sender, RoutedEventArgs e)
        {
            var channel = ViewModel.Item as TLChannel;
            var channelFull = ViewModel.Full as TLChannelFull;
            if (channelFull != null && channelFull.ChatPhoto is TLPhoto && channel != null)
            {
                var viewModel = new ChatPhotosViewModel(ViewModel.ProtoService, channelFull, channel);
                await GalleryView.Current.ShowAsync(viewModel, () => Picture);
            }
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

        private void ListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is TLChannelParticipantBase participant && participant.User != null)
            {
                ViewModel.NavigationService.Navigate(typeof(UserDetailsPage), participant.User.ToPeer());
            }
        }

        private void Notifications_Toggled(object sender, RoutedEventArgs e)
        {
            var toggle = sender as ToggleSwitch;
            if (toggle.FocusState != FocusState.Unfocused)
            {
                ViewModel.ToggleMuteCommand.Execute();
            }
        }

        private Visibility ConvertBooleans(int? first, bool second, bool third)
        {
            return first.HasValue && first > 0 && second && third ? Visibility.Visible : Visibility.Collapsed;
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

        private void ParticipantEdit_Loaded(object sender, RoutedEventArgs e)
        {
            var element = sender as FrameworkElement;
            var participant = element.DataContext as TLChannelParticipantBase;

            var channel = ViewModel.Item as TLChannel;
            if (channel == null)
            {
                return;
            }

            if ((channel.IsCreator || (channel.HasAdminRights && (channel.AdminRights.IsAddAdmins || channel.AdminRights.IsBanUsers))) && ((participant is TLChannelParticipantAdmin admin && admin.IsCanEdit) || participant is TLChannelParticipantBanned))
            {
                element.Visibility = participant is TLChannelParticipantCreator || participant.User.IsSelf ? Visibility.Collapsed : Visibility.Visible;
            }
            else
            {
                element.Visibility = Visibility.Collapsed;
            }
        }

        private void ParticipantPromote_Loaded(object sender, RoutedEventArgs e)
        {
            var element = sender as FrameworkElement;
            var participant = element.DataContext as TLChannelParticipantBase;

            var channel = ViewModel.Item as TLChannel;
            if (channel == null)
            {
                return;
            }

            if ((channel.IsCreator || (channel.HasAdminRights && channel.AdminRights.IsAddAdmins)) && !(participant is TLChannelParticipantAdmin))
            {
                element.Visibility = participant is TLChannelParticipantCreator || participant.User.IsSelf ? Visibility.Collapsed : Visibility.Visible;
            }
            else
            {
                element.Visibility = Visibility.Collapsed;
            }
        }

        private void ParticipantRestrict_Loaded(object sender, RoutedEventArgs e)
        {
            var element = sender as FrameworkElement;
            var participant = element.DataContext as TLChannelParticipantBase;

            var channel = ViewModel.Item as TLChannel;
            if (channel == null)
            {
                return;
            }

            if ((channel.IsCreator || (channel.HasAdminRights && channel.AdminRights.IsBanUsers)) && ((participant is TLChannelParticipantAdmin admin && admin.IsCanEdit) || (!(participant is TLChannelParticipantBanned) && !(participant is TLChannelParticipantAdmin))))
            {
                element.Visibility = participant is TLChannelParticipantCreator || participant.User.IsSelf ? Visibility.Collapsed : Visibility.Visible;
            }
            else
            {
                element.Visibility = Visibility.Collapsed;
            }
        }

        #endregion
    }
}
