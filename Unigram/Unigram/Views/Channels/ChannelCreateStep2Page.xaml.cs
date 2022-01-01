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
            DataContext = TLContainer.Current.Resolve<ChannelCreateStep2ViewModel, ISupergroupEditDelegate>(this);

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
            ViewModel.RevokeLinkCommand.Execute(e.ClickedItem);
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
                        subtitle.Text = MeUrlPrefixConverter.Convert(ViewModel.CacheService, supergroup.Username, true);
                    }
                }
            }
            else if (args.Phase == 2)
            {
                var photo = content.Children[0] as ProfilePicture;
                photo.SetChat(ViewModel.ProtoService, chat, 36);
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
            Header.Text = group.IsChannel ? Strings.Resources.ChannelSettingsTitle : Strings.Resources.GroupSettingsTitle;
            Subheader.Header = group.IsChannel ? Strings.Resources.ChannelTypeHeader : Strings.Resources.GroupTypeHeader;
            Subheader.Footer = group.Username.Length > 0 ? group.IsChannel ? Strings.Resources.ChannelPublicInfo : Strings.Resources.MegaPublicInfo : group.IsChannel ? Strings.Resources.ChannelPrivateInfo : Strings.Resources.MegaPrivateInfo;

            Public.Content = group.IsChannel ? Strings.Resources.ChannelPublic : Strings.Resources.MegaPublic;
            Private.Content = group.IsChannel ? Strings.Resources.ChannelPrivate : Strings.Resources.MegaPrivate;

            UsernameHelp.Footer = group.IsChannel ? Strings.Resources.ChannelUsernameHelp : Strings.Resources.MegaUsernameHelp;
            PrivateLinkHelp.Footer = group.IsChannel ? Strings.Resources.ChannelPrivateLinkHelp : Strings.Resources.MegaPrivateLinkHelp;



            ViewModel.Username = group.Username;
            ViewModel.IsPublic = !string.IsNullOrEmpty(group.Username);
        }

        public void UpdateSupergroupFullInfo(Chat chat, Supergroup group, SupergroupFullInfo fullInfo)
        {
            ViewModel.InviteLink = fullInfo.InviteLink?.InviteLink;

            if (fullInfo.InviteLink == null && string.IsNullOrEmpty(group.Username))
            {
                ViewModel.ProtoService.Send(new CreateChatInviteLink(chat.Id, string.Empty, 0, 0, false));
            }
        }

        public void UpdateBasicGroup(Chat chat, BasicGroup group)
        {
            Header.Text = Strings.Resources.GroupSettingsTitle;
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
                ViewModel.ProtoService.Send(new CreateChatInviteLink(chat.Id, string.Empty, 0, 0, false));
            }
        }

        public void UpdateChat(Chat chat)
        {
            Username.Prefix = MeUrlPrefixConverter.Convert(ViewModel.CacheService, string.Empty);
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
