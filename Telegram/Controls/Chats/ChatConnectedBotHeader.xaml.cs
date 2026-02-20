//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System.Collections.Generic;
using Telegram.Common;
using Telegram.Controls.Media;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Telegram.Views;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;

namespace Telegram.Controls.Chats
{
    public sealed partial class ChatConnectedBotHeader : UserControl
    {
        public DialogViewModel ViewModel => DataContext as DialogViewModel;

        private Chat _chat;

        private ChatView _chatView;

        public ChatConnectedBotHeader()
        {
            InitializeComponent();

            _collapsed = new SlidePanel.SlideState(this, false, 48);
        }

        public float AnimatedHeight => _collapsed ? 0 : 48;

        public void InitializeParent(ChatView chatView)
        {
            _chatView = chatView;
        }

        public void UpdateChatBusinessBotManageBar(Chat chat, BusinessBotManageBar manageBar)
        {
            _chat = chat;

            if (manageBar != null && ViewModel.ClientService.TryGetUser(manageBar.BotUserId, out User user))
            {
                ShowHide(true);

                Photo.Source = ProfilePictureSource.User(ViewModel.ClientService, user);
                Title.Text = user.FullName();
                Subtitle.Text = manageBar.IsBotPaused
                    ? Strings.BizBotStatusStopped
                    : manageBar.CanBotReply
                    ? Strings.BizBotStatusManages
                    : Strings.BizBotStatusAccess;

                ToggleButton.Content = manageBar.IsBotPaused
                    ? Strings.BizBotStart
                    : Strings.BizBotStop;

                ToggleButton.Visibility = manageBar.CanBotReply
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }
            else
            {
                ShowHide(false);
            }
        }

        private SlidePanel.SlideState _collapsed;

        public void ShowHide(bool show)
        {
            if (_collapsed != show)
            {
                return;
            }

            _collapsed.IsVisible = show;
            _chatView.UpdateMessagesHeaderPadding();
        }

        public IEnumerable<UIElement> GetAnimatableVisuals()
        {
            if (_collapsed)
            {
                yield break;
            }

            yield return ToggleButton;
            yield return MenuButton;
        }

        private void Menu_ContextRequested(object sender, RoutedEventArgs e)
        {
            var flyout = new MenuFlyout();

            flyout.CreateFlyoutItem(Remove, Strings.BizBotRemove, Icons.DismissCircle, destructive: true);
            flyout.CreateFlyoutItem(Manage, Strings.BizBotManage, Icons.Settings);

            flyout.ShowAt(sender as Button, FlyoutPlacementMode.BottomEdgeAlignedRight);
        }

        private void Remove()
        {
            var manage = _chat?.BusinessBotManageBar;
            if (manage != null)
            {
                ViewModel.ClientService.Send(new RemoveBusinessConnectedBotFromChat(_chat.Id));
            }
        }

        private void Manage()
        {
            var manage = _chat?.BusinessBotManageBar;
            if (manage != null)
            {
                MessageHelper.OpenUrl(ViewModel.ClientService, ViewModel.NavigationService, manage.ManageUrl);
            }
        }

        private void Toggle_Click(object sender, RoutedEventArgs e)
        {
            var manage = _chat?.BusinessBotManageBar;
            if (manage != null)
            {
                ViewModel.ClientService.Send(new ToggleBusinessConnectedBotChatIsPaused(_chat.Id, !manage.IsBotPaused));
            }
        }
    }
}
