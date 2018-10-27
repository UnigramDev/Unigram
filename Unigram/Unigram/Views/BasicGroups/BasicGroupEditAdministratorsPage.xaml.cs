using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Converters;
using Unigram.ViewModels.BasicGroups;
using Unigram.ViewModels.Delegates;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace Unigram.Views.BasicGroups
{
    public sealed partial class BasicGroupEditAdministratorsPage : Page, IBasicGroupDelegate
    {
        public BasicGroupEditAdministratorsViewModel ViewModel => DataContext as BasicGroupEditAdministratorsViewModel;

        public BasicGroupEditAdministratorsPage()
        {
            InitializeComponent();
            DataContext = TLContainer.Current.Resolve<BasicGroupEditAdministratorsViewModel, IBasicGroupDelegate>(this);
        }

        private void ListView_ItemClick(object sender, ItemClickEventArgs e)
        {

        }

        #region Context menu

        private void Member_ContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {

        }

        #endregion

        #region Recycle

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }

            var check = args.ItemContainer.ContentTemplateRoot as CheckBox;
            var content = check.Content as Grid;
            var member = args.Item as ChatMember;

            var basicGroup = ViewModel.Chat.Type is ChatTypeBasicGroup basic ? ViewModel.CacheService.GetBasicGroup(basic.BasicGroupId) : null;
            if (basicGroup == null)
            {
                return;
            }

            var user = ViewModel.ProtoService.GetUser(member.UserId);
            if (user == null)
            {
                return;
            }

            if (args.Phase == 0)
            {
                var title = content.Children[1] as TextBlock;
                title.Text = user.GetFullName();

                check.IsChecked = member.Status is ChatMemberStatusCreator || member.Status is ChatMemberStatusAdministrator || basicGroup.EveryoneIsAdministrator;
                check.IsEnabled = !(member.Status is ChatMemberStatusCreator || basicGroup.EveryoneIsAdministrator);
                check.Command = ViewModel.ToggleMemberCommand;
                check.CommandParameter = member;
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

        #region Delegate

        public void UpdateBasicGroup(Chat chat, BasicGroup group)
        {
            SetAll.IsChecked = group.EveryoneIsAdministrator;
            Footer.Text = group.EveryoneIsAdministrator ? Strings.Resources.SetAdminsAllInfo : Strings.Resources.SetAdminsNotAllInfo;

            var items = ScrollingHost.ItemsSource as IList<ChatMember>;
            if (items == null)
            {
                return;
            }

            foreach (var member in items)
            {
                var container = ScrollingHost.ContainerFromItem(member) as SelectorItem;
                if (container == null)
                {
                    continue;
                }

                var check = container.ContentTemplateRoot as CheckBox;
                var content = check.Content as Grid;

                check.IsChecked = member.Status is ChatMemberStatusCreator || member.Status is ChatMemberStatusAdministrator || group.EveryoneIsAdministrator;
                check.IsEnabled = !(member.Status is ChatMemberStatusCreator || group.EveryoneIsAdministrator);
                check.Command = ViewModel.ToggleMemberCommand;
                check.CommandParameter = member;
            }
        }

        public void UpdateBasicGroupFullInfo(Chat chat, BasicGroup group, BasicGroupFullInfo fullInfo)
        {
            ScrollingHost.ItemsSource = fullInfo.Members.OrderBy(x => x, new ChatMemberComparer()).ToArray();
        }

        public void UpdateChat(Chat chat) { }
        public void UpdateChatTitle(Chat chat) { }
        public void UpdateChatPhoto(Chat chat) { }

        #endregion
    }
}
