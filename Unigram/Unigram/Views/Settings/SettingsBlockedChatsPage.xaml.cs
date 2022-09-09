using System;
using Telegram.Td.Api;
using Unigram.Controls;
using Unigram.Controls.Cells;
using Unigram.ViewModels.Settings;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Point = Windows.Foundation.Point;

namespace Unigram.Views.Settings
{
    public sealed partial class SettingsBlockedChatsPage : HostedPage
    {
        public SettingsBlockedChatsViewModel ViewModel => DataContext as SettingsBlockedChatsViewModel;

        public SettingsBlockedChatsPage()
        {
            InitializeComponent();
            Title = Strings.Resources.BlockedUsers;
        }

        private async void ListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is MessageSender messageSender)
            {
                if (ViewModel.CacheService.TryGetUser(messageSender, out User user))
                {
                    var response = await ViewModel.ProtoService.SendAsync(new CreatePrivateChat(user.Id, false));
                    if (response is Chat chat)
                    {
                        ViewModel.NavigationService.Navigate(typeof(ProfilePage), chat.Id);
                    }
                }
                else if (ViewModel.CacheService.TryGetChat(messageSender, out Chat chat))
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
            else if (args.ItemContainer.ContentTemplateRoot is UserCell content)
            {
                content.UpdateMessageSender(ViewModel.ProtoService, args, OnContainerContentChanging);
            }
        }

        #endregion

        private void User_ContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
            var flyout = new MenuFlyout();

            var element = sender as FrameworkElement;
            var messageSender = ScrollingHost.ItemFromContainer(element) as MessageSender;

            flyout.Items.Add(new MenuFlyoutItem { Text = Strings.Resources.Unblock, Command = ViewModel.UnblockCommand, CommandParameter = messageSender });

            if (args.TryGetPosition(sender, out Point point))
            {
                if (point.X < 0 || point.Y < 0)
                {
                    point = new Point(Math.Max(point.X, 0), Math.Max(point.Y, 0));
                }

                flyout.ShowAt(sender, point);
            }
        }
    }
}
