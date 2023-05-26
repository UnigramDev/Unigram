//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Controls.Cells;
using Telegram.Controls.Media;
using Telegram.Td.Api;
using Telegram.ViewModels.Settings;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;

namespace Telegram.Views.Settings
{
    public sealed partial class SettingsBlockedChatsPage : HostedPage
    {
        public SettingsBlockedChatsViewModel ViewModel => DataContext as SettingsBlockedChatsViewModel;

        public SettingsBlockedChatsPage()
        {
            InitializeComponent();
            Title = Strings.BlockedUsers;
        }

        private async void ListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is MessageSender messageSender)
            {
                if (ViewModel.ClientService.TryGetUser(messageSender, out User user))
                {
                    var response = await ViewModel.ClientService.SendAsync(new CreatePrivateChat(user.Id, false));
                    if (response is Chat chat)
                    {
                        ViewModel.NavigationService.Navigate(typeof(ProfilePage), chat.Id);
                    }
                }
                else if (ViewModel.ClientService.TryGetChat(messageSender, out Chat chat))
                {
                    ViewModel.NavigationService.Navigate(typeof(ProfilePage), chat.Id);
                }
            }
        }

        #region Recycle

        private void OnChoosingItemContainer(ListViewBase sender, ChoosingItemContainerEventArgs args)
        {
            if (args.ItemContainer == null)
            {
                args.ItemContainer = new TableListViewItem();
                args.ItemContainer.Style = sender.ItemContainerStyle;
                args.ItemContainer.ContentTemplate = sender.ItemTemplate;
                args.ItemContainer.ContextRequested += User_ContextRequested;
            }

            args.IsContainerPrepared = true;
        }

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }
            else if (args.ItemContainer.ContentTemplateRoot is ProfileCell content)
            {
                content.UpdateMessageSender(ViewModel.ClientService, args, OnContainerContentChanging);
            }
        }

        #endregion

        private void User_ContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
            var element = sender as FrameworkElement;
            var messageSender = ScrollingHost.ItemFromContainer(element) as MessageSender;

            var flyout = new MenuFlyout();
            flyout.CreateFlyoutItem(ViewModel.Unblock, messageSender, Strings.Unblock, Icons.SubtractCircle);

            args.ShowAt(flyout, element);
        }
    }
}
