using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Windows.Input;
using Telegram.Api.TL;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Controls.Views;
using Unigram.Strings;
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
                var dialog = new EditYourPhotoView(file)
                {
                    CroppingProportions = ImageCroppingProportions.Square,
                    IsCropEnabled = false
                };
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

        private void Participant_ContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
            var menu = new MenuFlyout();

            var element = sender as FrameworkElement;
            var participant = element.DataContext as TLChannelParticipantBase;

            CreateFlyoutItem(ref menu, ParticipantEdit_Loaded, ViewModel.ParticipantEditCommand, participant, AppResources.ParticipantEdit);
            CreateFlyoutItem(ref menu, ParticipantPromote_Loaded, ViewModel.ParticipantPromoteCommand, participant, AppResources.ParticipantPromote);
            CreateFlyoutItem(ref menu, ParticipantRestrict_Loaded, ViewModel.ParticipantRestrictCommand, participant, AppResources.ParticipantRestrict);

            if (menu.Items.Count > 0 && args.TryGetPosition(sender, out Point point))
            {
                menu.ShowAt(sender, point);
            }
        }

        private void CreateFlyoutItem(ref MenuFlyout menu, Func<TLChannelParticipantBase, Visibility> visibility, ICommand command, object parameter, string text)
        {
            var value = visibility(parameter as TLChannelParticipantBase);
            if (value == Visibility.Visible)
            {
                var flyoutItem = new MenuFlyoutItem();
                //flyoutItem.Loaded += (s, args) => flyoutItem.Visibility = visibility(parameter as TLMessageCommonBase);
                flyoutItem.Command = command;
                flyoutItem.CommandParameter = parameter;
                flyoutItem.Text = text;

                menu.Items.Add(flyoutItem);
            }
        }

        private Visibility ParticipantEdit_Loaded(TLChannelParticipantBase participant)
        {
            var channel = ViewModel.Item as TLChannel;
            if (channel == null)
            {
                return Visibility.Collapsed;
            }

            if ((channel.IsCreator || (channel.HasAdminRights && (channel.AdminRights.IsAddAdmins || channel.AdminRights.IsBanUsers))) && ((participant is TLChannelParticipantAdmin admin && admin.IsCanEdit) || participant is TLChannelParticipantBanned))
            {
                return participant is TLChannelParticipantCreator || participant.User.IsSelf ? Visibility.Collapsed : Visibility.Visible;
            }

            return Visibility.Collapsed;
        }

        private Visibility ParticipantPromote_Loaded(TLChannelParticipantBase participant)
        {
            var channel = ViewModel.Item as TLChannel;
            if (channel == null)
            {
                return Visibility.Collapsed;
            }

            if ((channel.IsCreator || (channel.HasAdminRights && channel.AdminRights.IsAddAdmins)) && !(participant is TLChannelParticipantAdmin))
            {
                return participant is TLChannelParticipantCreator || participant.User.IsSelf ? Visibility.Collapsed : Visibility.Visible;
            }

            return Visibility.Collapsed;
        }

        private Visibility ParticipantRestrict_Loaded(TLChannelParticipantBase participant)
        {
            var channel = ViewModel.Item as TLChannel;
            if (channel == null)
            {
                return Visibility.Collapsed;
            }

            if ((channel.IsCreator || (channel.HasAdminRights && channel.AdminRights.IsBanUsers)) && ((participant is TLChannelParticipantAdmin admin && admin.IsCanEdit) || (!(participant is TLChannelParticipantBanned) && !(participant is TLChannelParticipantAdmin))))
            {
                return participant is TLChannelParticipantCreator || participant.User.IsSelf ? Visibility.Collapsed : Visibility.Visible;
            }

            return Visibility.Collapsed;
        }

        #endregion
    }
}
