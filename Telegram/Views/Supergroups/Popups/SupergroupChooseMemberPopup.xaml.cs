//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using Telegram.Collections;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Converters;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Telegram.ViewModels.Supergroups;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Media;

namespace Telegram.Views.Supergroups.Popups
{
    public enum SupergroupChooseMemberMode
    {
        Promote,
        Restrict,
        Block
    }

    public class SupergroupChooseMemberArgs
    {
        public SupergroupChooseMemberArgs(long chatId, SupergroupChooseMemberMode mode)
        {
            ChatId = chatId;
            Mode = mode;
        }

        public long ChatId { get; }

        public SupergroupChooseMemberMode Mode { get; }
    }

    public sealed partial class SupergroupChooseMemberPopup : ContentPopup
    {
        public SupergroupChooseMemberViewModel ViewModel => DataContext as SupergroupChooseMemberViewModel;

        public SupergroupChooseMemberPopup()
        {
            InitializeComponent();

            SecondaryButtonText = Strings.Cancel;

            var debouncer = new EventDebouncer<TextChangedEventArgs>(Constants.TypingTimeout, handler => SearchField.TextChanged += new TextChangedEventHandler(handler));
            debouncer.Invoked += async (s, args) =>
            {
                var items = ViewModel.Search;
                if (items != null && string.Equals(SearchField.Text, items.Query))
                {
                    await items.LoadMoreItemsAsync(0);
                    await items.LoadMoreItemsAsync(1);
                    await items.LoadMoreItemsAsync(2);
                    await items.LoadMoreItemsAsync(3);
                }
            };
        }

        public override void OnNavigatedTo()
        {
            Title = ViewModel.Mode switch
            {
                SupergroupChooseMemberMode.Promote => Strings.ChannelAddAdmin,
                SupergroupChooseMemberMode.Restrict => Strings.ChannelAddException,
                SupergroupChooseMemberMode.Block => Strings.ChannelBlockUser,
                _ => string.Empty
            };
        }

        private void ListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            var chat = ViewModel.Chat;
            if (chat == null)
            {
                return;
            }

            Hide();

            var sourcePopupType = ViewModel.Mode switch
            {
                SupergroupChooseMemberMode.Promote => typeof(SupergroupEditAdministratorPopup),
                SupergroupChooseMemberMode.Restrict => typeof(SupergroupEditRestrictedPopup),
                _ => null
            };

            if (sourcePopupType == null)
            {
                return;
            }

            if (e.ClickedItem is ChatMember member)
            {
                ViewModel.NavigationService.ShowPopupAsync(sourcePopupType, new SupergroupEditMemberArgs(chat.Id, member.MemberId));
            }
            else if (e.ClickedItem is SearchResult result)
            {
                if (result.User is User user)
                {
                    ViewModel.NavigationService.ShowPopupAsync(sourcePopupType, new SupergroupEditMemberArgs(chat.Id, new MessageSenderUser(user.Id)));
                }
                else if (result.Chat is Chat temp && temp.Type is ChatTypePrivate privata)
                {
                    ViewModel.NavigationService.ShowPopupAsync(sourcePopupType, new SupergroupEditMemberArgs(chat.Id, new MessageSenderUser(privata.UserId)));
                }
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
            var member = args.Item as ChatMember;

            var user = ViewModel.ClientService.GetMessageSender(member.MemberId) as User;
            if (user == null)
            {
                return;
            }

            if (args.Phase == 0)
            {
                var title = content.Children[1] as TextBlock;
                title.Text = user.FullName();
            }
            else if (args.Phase == 1)
            {
                var subtitle = content.Children[2] as TextBlock;
                subtitle.Text = ChannelParticipantToTypeConverter.Convert(ViewModel.ClientService, member);
            }
            else if (args.Phase == 2)
            {
                var photo = content.Children[0] as ProfilePicture;
                photo.SetUser(ViewModel.ClientService, user, 36);
            }

            if (args.Phase < 2)
            {
                args.RegisterUpdateCallback(OnContainerContentChanging);
            }

            args.Handled = true;
        }

        private void Search_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                //var photo = content.Children[0] as ProfilePicture;
                //photo.Source = null;

                return;
            }

            var result = args.Item as SearchResult;
            var chat = result.Chat;
            var user = result.User ?? ViewModel.ClientService.GetUser(chat);

            if (user == null)
            {
                return;
            }

            var content = args.ItemContainer.ContentTemplateRoot as Grid;
            if (content == null)
            {
                return;
            }

            if (args.Phase == 0)
            {
                var title = content.Children[1] as TextBlock;
                title.Text = user.FullName();
            }
            else if (args.Phase == 1)
            {
                var subtitle = content.Children[2] as TextBlock;
                if (result.IsPublic)
                {
                    subtitle.Text = $"@{user.ActiveUsername(result.Query)}";
                }
                else
                {
                    subtitle.Text = LastSeenConverter.GetLabel(user, true);
                }

                if (subtitle.Text.StartsWith($"@{result.Query}", StringComparison.OrdinalIgnoreCase))
                {
                    var highligher = new TextHighlighter();
                    highligher.Foreground = new SolidColorBrush(Colors.Red);
                    highligher.Background = new SolidColorBrush(Colors.Transparent);
                    highligher.Ranges.Add(new TextRange { StartIndex = 1, Length = result.Query.Length });

                    subtitle.TextHighlighters.Add(highligher);
                }
                else
                {
                    subtitle.TextHighlighters.Clear();
                }
            }
            else if (args.Phase == 2)
            {
                var photo = content.Children[0] as ProfilePicture;
                photo.SetUser(ViewModel.ClientService, user, 36);
            }

            if (args.Phase < 2)
            {
                args.RegisterUpdateCallback(Search_ContainerContentChanging);
            }

            args.Handled = true;
        }

        #endregion

        private void Search_Click(object sender, RoutedEventArgs e)
        {
            //MainHeader.Visibility = Visibility.Collapsed;
            //SearchField.Visibility = Visibility.Visible;

            //SearchField.Focus(FocusState.Keyboard);
        }

        private void Search_LostFocus(object sender, RoutedEventArgs e)
        {
            //if (string.IsNullOrEmpty(SearchField.Text))
            //{
            //    MainHeader.Visibility = Visibility.Visible;
            //    SearchField.Visibility = Visibility.Collapsed;

            //    Focus(FocusState.Programmatic);
            //}
        }

        private void Search_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (string.IsNullOrEmpty(SearchField.Text))
            {
                ContentPanel.Visibility = Visibility.Visible;
                ViewModel.Search = null;
            }
            else
            {
                ContentPanel.Visibility = Visibility.Collapsed;
                ViewModel.Search = new SearchMembersAndUsersCollection(ViewModel.ClientService, ViewModel.Chat.Id, new ChatMembersFilterMembers(), SearchField.Text);
            }
        }
    }
}
