//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Converters;
using Unigram.ViewModels.Channels;
using Unigram.ViewModels.Delegates;
using Windows.UI.Xaml.Controls;

namespace Unigram.Views.Channels
{
    public sealed partial class ChannelCreateStep2Page : HostedPage, ISupergroupEditDelegate
    {
        public ChannelCreateStep2ViewModel ViewModel => DataContext as ChannelCreateStep2ViewModel;

        public ChannelCreateStep2Page()
        {
            InitializeComponent();
            Title = Strings.Resources.ChannelSettings;

            var debouncer = new EventDebouncer<TextChangedEventArgs>(Constants.TypingTimeout, handler => Username.TextChanged += new TextChangedEventHandler(handler));
            debouncer.Invoked += (s, args) =>
            {
                if (ViewModel.UpdateIsValid(Username.Value))
                {
                    ViewModel.CheckAvailability(Username.Value);
                }
            };
        }

        private void ListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is Chat chat)
            {

            }
        }

        #region Recycle

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
                title.Text = ViewModel.ClientService.GetTitle(chat);
            }
            else if (args.Phase == 1)
            {
                if (chat.Type is ChatTypeSupergroup super)
                {
                    var supergroup = ViewModel.ClientService.GetSupergroup(super.SupergroupId);
                    if (supergroup != null)
                    {
                        var subtitle = content.Children[2] as TextBlock;
                        subtitle.Text = MeUrlPrefixConverter.Convert(ViewModel.ClientService, supergroup.ActiveUsername(), true);
                    }
                }
            }
            else if (args.Phase == 2)
            {
                var photo = content.Children[0] as ProfilePicture;
                photo.SetChat(ViewModel.ClientService, chat, 36);
            }

            if (args.Phase < 2)
            {
                args.RegisterUpdateCallback(OnContainerContentChanging);
            }

            args.Handled = true;
        }

        #endregion

        #region Delegate

        public void UpdateSupergroup(Chat chat, Supergroup group)
        {
            var username = group.EditableUsername();

            Title = group.IsChannel ? Strings.Resources.ChannelSettingsTitle : Strings.Resources.GroupSettingsTitle;
            Subheader.Header = group.IsChannel ? Strings.Resources.ChannelTypeHeader : Strings.Resources.GroupTypeHeader;
            Subheader.Footer = username.Length > 0 ? group.IsChannel ? Strings.Resources.ChannelPublicInfo : Strings.Resources.MegaPublicInfo : group.IsChannel ? Strings.Resources.ChannelPrivateInfo : Strings.Resources.MegaPrivateInfo;

            Public.Content = group.IsChannel ? Strings.Resources.ChannelPublic : Strings.Resources.MegaPublic;
            Private.Content = group.IsChannel ? Strings.Resources.ChannelPrivate : Strings.Resources.MegaPrivate;

            UsernameHelp.Footer = group.IsChannel ? Strings.Resources.ChannelUsernameHelp : Strings.Resources.MegaUsernameHelp;
            PrivateLinkHelp.Footer = group.IsChannel ? Strings.Resources.ChannelPrivateLinkHelp : Strings.Resources.MegaPrivateLinkHelp;



            ViewModel.Username = username;
            ViewModel.IsPublic = username.Length > 0;
        }

        public void UpdateSupergroupFullInfo(Chat chat, Supergroup group, SupergroupFullInfo fullInfo)
        {
            ViewModel.InviteLink = fullInfo.InviteLink?.InviteLink;

            if (fullInfo.InviteLink == null && !group.HasEditableUsername())
            {
                ViewModel.ClientService.Send(new CreateChatInviteLink(chat.Id, string.Empty, 0, 0, false));
            }
        }

        public void UpdateBasicGroup(Chat chat, BasicGroup group)
        {
            Title = Strings.Resources.GroupSettingsTitle;
            Subheader.Header = Strings.Resources.GroupTypeHeader;
            Subheader.Footer = Strings.Resources.MegaPrivateInfo;

            Public.Content = Strings.Resources.MegaPublic;
            Private.Content = Strings.Resources.MegaPrivate;

            UsernameHelp.Footer = Strings.Resources.MegaUsernameHelp;
            PrivateLinkHelp.Footer = Strings.Resources.MegaPrivateLinkHelp;



            ViewModel.Username = string.Empty;
            ViewModel.IsPublic = false;
        }

        public void UpdateBasicGroupFullInfo(Chat chat, BasicGroup group, BasicGroupFullInfo fullInfo)
        {
            ViewModel.InviteLink = fullInfo.InviteLink?.InviteLink;

            if (fullInfo.InviteLink == null)
            {
                ViewModel.ClientService.Send(new CreateChatInviteLink(chat.Id, string.Empty, 0, 0, false));
            }
        }

        public void UpdateChat(Chat chat)
        {
            Username.Prefix = MeUrlPrefixConverter.Convert(ViewModel.ClientService, string.Empty);
        }

        public void UpdateChatTitle(Chat chat) { }
        public void UpdateChatPhoto(Chat chat) { }

        #endregion

        #region Binding

        private string ConvertAvailable(string username)
        {
            return string.Format(Strings.Resources.LinkAvailable, username);
        }

        private string ConvertFooter(bool pubblico)
        {
            if (ViewModel.Chat?.Type is ChatTypeSupergroup supergroup && supergroup.IsChannel)
            {
                return pubblico ? Strings.Resources.ChannelPublicInfo : Strings.Resources.ChannelPrivateInfo;
            }

            return pubblico ? Strings.Resources.MegaPublicInfo : Strings.Resources.MegaPrivateInfo;
        }

        #endregion

    }
}
