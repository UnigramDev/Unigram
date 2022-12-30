using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Controls.Cells;
using Unigram.Navigation.Services;
using Unigram.ViewModels.Delegates;
using Unigram.ViewModels.Supergroups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;

namespace Unigram.Views.Supergroups
{
    public sealed partial class SupergroupAdministratorsPage : HostedPage, IBasicAndSupergroupDelegate
    {
        public SupergroupAdministratorsViewModel ViewModel => DataContext as SupergroupAdministratorsViewModel;

        public SupergroupAdministratorsPage()
        {
            InitializeComponent();
            Title = Strings.Resources.ChannelAdministrators;
        }

        private void ListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            var chat = ViewModel.Chat;
            if (chat == null)
            {
                return;
            }

            var member = e.ClickedItem as ChatMember;
            if (member == null)
            {
                return;
            }

            ViewModel.NavigationService.Navigate(typeof(SupergroupEditAdministratorPage), state: NavigationState.GetChatMember(chat.Id, member.MemberId));
        }

        #region Context menu

        private void Member_ContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {

        }

        #endregion

        #region Recycle

        private void OnChoosingItemContainer(ListViewBase sender, ChoosingItemContainerEventArgs args)
        {
            if (args.ItemContainer == null)
            {
                args.ItemContainer = new TableListViewItem();
                args.ItemContainer.Style = sender.ItemContainerStyle;
                args.ItemContainer.ContentTemplate = sender.ItemTemplate;
                args.ItemContainer.ContextRequested += Member_ContextRequested;
            }

            args.IsContainerPrepared = true;
        }

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }
            else if (args.ItemContainer.ContentTemplateRoot is UserCell content)
            {
                content.UpdateSupergroupMember(ViewModel.ClientService, args, OnContainerContentChanging);
            }
        }

        #endregion

        #region Binding

        public void UpdateChat(Chat chat) { }
        public void UpdateChatTitle(Chat chat) { }
        public void UpdateChatPhoto(Chat chat) { }

        public void UpdateSupergroup(Chat chat, Supergroup group)
        {
            HeaderPanel.Footer = group.CanDeleteMessages() && !group.IsChannel ? Strings.Resources.ChannelAntiSpamInfo : string.Empty;
            AntiSpam.Visibility = group.CanDeleteMessages() && !group.IsChannel ? Visibility.Visible : Visibility.Collapsed;

            EventLog.Visibility = Visibility.Visible;
            AddNew.Visibility = group.CanPromoteMembers() ? Visibility.Visible : Visibility.Collapsed;
            Footer.Visibility = group.CanPromoteMembers() ? Visibility.Visible : Visibility.Collapsed;
            Footer.Text = group.IsChannel ? Strings.Resources.ChannelAdminsInfo : Strings.Resources.MegaAdminsInfo;

            HeaderPanel.Visibility = Visibility.Visible;
        }

        public void UpdateSupergroupFullInfo(Chat chat, Supergroup group, SupergroupFullInfo fullInfo)
        {
            ViewModel.UpdateIsAggressiveAntiSpamEnabled(fullInfo.HasAggressiveAntiSpamEnabled);
            AntiSpam.Visibility = fullInfo.CanToggleAggressiveAntiSpam ? Visibility.Visible : Visibility.Collapsed;
        }

        public void UpdateBasicGroup(Chat chat, BasicGroup group)
        {
            HeaderPanel.Footer = string.Empty;
            AntiSpam.Visibility = Visibility.Collapsed;

            EventLog.Visibility = Visibility.Collapsed;
            AddNew.Visibility = group.CanPromoteMembers() ? Visibility.Visible : Visibility.Collapsed;
            Footer.Visibility = group.CanPromoteMembers() ? Visibility.Visible : Visibility.Collapsed;
            Footer.Text = Strings.Resources.MegaAdminsInfo;

            HeaderPanel.Visibility = EventLog.Visibility == Visibility.Visible || AddNew.Visibility == Visibility.Visible
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        public void UpdateBasicGroupFullInfo(Chat chat, BasicGroup group, BasicGroupFullInfo fullInfo)
        {
            ViewModel.UpdateIsAggressiveAntiSpamEnabled(false);
            AntiSpam.Visibility = fullInfo.CanToggleAggressiveAntiSpam ? Visibility.Visible : Visibility.Collapsed;
        }

        #endregion

    }
}
