using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using TdWindows;
using Telegram.Api.Helpers;
using Telegram.Api.TL;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Controls.Views;
using Unigram.Converters;
using Unigram.ViewModels;
using Unigram.ViewModels.Channels;
using Unigram.ViewModels.Supergroups;
using Windows.ApplicationModel.Resources;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage.Pickers;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace Unigram.Views.Supergroups
{
    public sealed partial class SupergroupEditPage : Page, ISupergroupDelegate
    {
        public SupergroupEditViewModel ViewModel => DataContext as SupergroupEditViewModel;

        public SupergroupEditPage()
        {
            InitializeComponent();
            DataContext = UnigramContainer.Current.ResolveType<SupergroupEditViewModel>();
            ViewModel.Delegate = this;

            var observable = Observable.FromEventPattern<TextChangedEventArgs>(Username, "TextChanged");
            var throttled = observable.Throttle(TimeSpan.FromMilliseconds(Constants.TypingTimeout)).ObserveOnDispatcher().Subscribe(x =>
            {
                if (ViewModel.UpdateIsValid(Username.Text))
                {
                    ViewModel.CheckAvailability(Username.Text);
                }
            });
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

                var confirm = await dialog.ShowAsync();
                if (confirm == ContentDialogBaseResult.OK)
                {
                    ViewModel.EditPhotoCommand.Execute(dialog.Result);
                }
            }
        }

        private void ListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            ViewModel.RevokeLinkCommand.Execute(e.ClickedItem);
        }

        #region Binding

        public void UpdateChat(Chat chat)
        {
            //UpdateChatTitle(chat);
            UpdateChatPhoto(chat);
        }

        public void UpdateChatTitle(Chat chat)
        {
            Title.Text = ViewModel.ProtoService.GetTitle(chat);
        }

        public void UpdateChatPhoto(Chat chat)
        {
            Photo.Source = PlaceholderHelper.GetChat(ViewModel.ProtoService, chat, 64, 64);
        }

        public void UpdateSupergroup(Chat chat, Supergroup group)
        {
            Title.PlaceholderText = group.IsChannel ? Strings.Android.EnterChannelName : Strings.Android.GroupName;

            Public.Content = group.IsChannel ? Strings.Android.ChannelPublic : Strings.Android.MegaPublic;
            PublicInfo.Text = group.IsChannel ? Strings.Android.ChannelPublicInfo : Strings.Android.MegaPublicInfo;

            Private.Content = group.IsChannel ? Strings.Android.ChannelPrivate : Strings.Android.MegaPrivate;
            PrivateInfo.Text = group.IsChannel ? Strings.Android.ChannelPrivateInfo : Strings.Android.MegaPrivateInfo;

            UsernameHelp.Text = group.IsChannel ? Strings.Android.ChannelUsernameHelp : Strings.Android.MegaUsernameHelp;
            PrivateLinkHelp.Text = group.IsChannel ? Strings.Android.ChannelPrivateLinkHelp : Strings.Android.MegaPrivateLinkHelp;

            Delete.Content = group.IsChannel ? Strings.Android.ChannelDelete : Strings.Android.DeleteMega;
            DeleteInfo.Text = group.IsChannel ? Strings.Android.ChannelDeleteInfo : Strings.Android.MegaDeleteInfo;


            ViewModel.Title = chat.Title;
            ViewModel.Username = group.Username;
            ViewModel.IsPublic = !string.IsNullOrEmpty(group.Username);
            ViewModel.IsDemocracy = group.AnyoneCanInvite;
            ViewModel.IsSignatures = group.SignMessages;


            UsernamePanel.Visibility = Visibility.Collapsed;
            ChannelSignMessagesPanel.Visibility = group.CanChangeInfo() && group.IsChannel ? Visibility.Visible : Visibility.Collapsed;
            GroupInvitesPanel.Visibility = group.CanChangeInfo() && !group.IsChannel ? Visibility.Visible : Visibility.Collapsed;
            GroupHistoryPanel.Visibility = group.CanChangeInfo() && string.IsNullOrEmpty(group.Username) && !group.IsChannel ? Visibility.Visible : Visibility.Collapsed;
            GroupStickersPanel.Visibility = Visibility.Collapsed;
            DeletePanel.Visibility = group.Status is ChatMemberStatusCreator ? Visibility.Visible : Visibility.Collapsed;
        }

        public void UpdateSupergroupFullInfo(Chat chat, Supergroup group, SupergroupFullInfo fullInfo)
        {
            UsernamePanel.Visibility = fullInfo.CanSetUsername ? Visibility.Visible : Visibility.Collapsed;

            //GroupStickers.Content = fullInfo.sticker
            GroupStickersPanel.Visibility = fullInfo.CanSetStickerSet ? Visibility.Visible : Visibility.Collapsed;


            ViewModel.About = fullInfo.Description;
            ViewModel.InviteLink = fullInfo.InviteLink;
            ViewModel.IsAllHistoryAvailable = fullInfo.IsAllHistoryAvailable;


            if (fullInfo.StickerSetId == 0 || !fullInfo.CanSetStickerSet)
            {
                return;
            }

            ViewModel.ProtoService.Send(new GetStickerSet(fullInfo.StickerSetId), result =>
            {
                this.BeginOnUIThread(() =>
                {
                    if (result is StickerSet set && ViewModel.Chat?.Id == chat.Id)
                    {
                        GroupStickers.Badge = set.Title;
                    }
                });
            });
        }

        #endregion

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }

            var content = args.ItemContainer.ContentTemplateRoot as Grid;
            var chat = args.Item as Chat;

            if (args.Phase == 0)
            {
                var title = content.Children[1] as TextBlock;
                title.Text = ViewModel.ProtoService.GetTitle(chat);
            }
            else if (args.Phase == 1)
            {
                if (chat.Type is ChatTypeSupergroup super)
                {
                    var supergroup = ViewModel.ProtoService.GetSupergroup(super.SupergroupId);
                    if (supergroup != null)
                    {
                        var subtitle = content.Children[2] as TextBlock;
                        subtitle.Text = MeUrlPrefixConverter.Convert(supergroup.Username, true);
                    }
                }
            }
            else if (args.Phase == 2)
            {
                var photo = content.Children[0] as ProfilePicture;
                photo.Source = PlaceholderHelper.GetChat(ViewModel.ProtoService, chat, 36, 36);
            }

            if (args.Phase < 2)
            {
                args.RegisterUpdateCallback(OnContainerContentChanging);
            }

            args.Handled = true;
        }
    }
}
