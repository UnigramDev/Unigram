using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Converters;
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
            DataContext = TLContainer.Current.Resolve<SupergroupAdministratorsViewModel, ISupergroupDelegate>(this);
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

            ViewModel.NavigationService.Navigate(typeof(SupergroupEditAdministratorPage), state: NavigationState.GetChatMember(chat.Id, member.UserId));
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
                args.ItemContainer = new ListViewItem();
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

            var content = args.ItemContainer.ContentTemplateRoot as Grid;
            var member = args.Item as ChatMember;

            args.ItemContainer.Tag = args.Item;
            content.Tag = args.Item;

            var user = ViewModel.ProtoService.GetUser(member.UserId);
            if (user == null)
            {
                return;
            }

            if (args.Phase == 0)
            {
                var title = content.Children[1] as TextBlock;
                title.Text = user.GetFullName();
            }
            else if (args.Phase == 1)
            {
                var subtitle = content.Children[2] as TextBlock;
                subtitle.Text = ChannelParticipantToTypeConverter.Convert(ViewModel.ProtoService, member);
            }
            else if (args.Phase == 2)
            {
                var photo = content.Children[0] as ProfilePicture;
                photo.Source = PlaceholderHelper.GetUser(ViewModel.ProtoService, user, 36);
            }

            if (args.Phase < 2)
            {
                args.RegisterUpdateCallback(OnContainerContentChanging);
            }

            args.Handled = true;
        }

        #endregion

        #region Binding

        public void UpdateChat(Chat chat) { }
        public void UpdateChatTitle(Chat chat) { }
        public void UpdateChatPhoto(Chat chat) { }

        public void UpdateSupergroup(Chat chat, Supergroup group)
        {
            EventLog.Visibility = Visibility.Visible;
            AddNew.Visibility = group.CanPromoteMembers() ? Visibility.Visible : Visibility.Collapsed;
            Footer.Visibility = group.CanPromoteMembers() ? Visibility.Visible : Visibility.Collapsed;
            Footer.Text = group.IsChannel ? Strings.Resources.ChannelAdminsInfo : Strings.Resources.MegaAdminsInfo;

            HeaderPanel.Visibility = Visibility.Visible;
        }

        public void UpdateSupergroupFullInfo(Chat chat, Supergroup group, SupergroupFullInfo fullInfo) { }

        public void UpdateBasicGroup(Chat chat, BasicGroup group)
        {
            EventLog.Visibility = Visibility.Collapsed;
            AddNew.Visibility = group.CanPromoteMembers() ? Visibility.Visible : Visibility.Collapsed;
            Footer.Visibility = group.CanPromoteMembers() ? Visibility.Visible : Visibility.Collapsed;
            Footer.Text = Strings.Resources.MegaAdminsInfo;

            HeaderPanel.Visibility = EventLog.Visibility == Visibility.Visible || AddNew.Visibility == Visibility.Visible
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        public void UpdateBasicGroupFullInfo(Chat chat, BasicGroup group, BasicGroupFullInfo fullInfo) { }

        #endregion

    }
}
