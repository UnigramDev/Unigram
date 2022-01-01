using System;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Controls;
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
            DataContext = TLContainer.Current.Resolve<SettingsBlockedChatsViewModel>();
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
                args.ItemContainer = new TextListViewItem();
                args.ItemContainer.Style = sender.ItemContainerStyle;
                args.ItemContainer.ContentTemplate = sender.ItemTemplate;
                args.ItemContainer.ContextRequested += User_ContextRequested;
            }

            args.IsContainerPrepared = true;
        }

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            var content = args.ItemContainer.ContentTemplateRoot as Grid;
            var messageSender = args.Item as MessageSender;

            content.Tag = messageSender;

            ViewModel.CacheService.TryGetUser(messageSender, out User user);
            ViewModel.CacheService.TryGetChat(messageSender, out Chat chat);

            if (args.Phase == 0)
            {
                var title = content.Children[1] as TextBlock;
                if (user != null)
                {
                    title.Text = user.GetFullName();
                }
                else if (chat != null)
                {
                    title.Text = ViewModel.ProtoService.GetTitle(chat);
                }
            }
            else if (args.Phase == 2)
            {
                var photo = content.Children[0] as ProfilePicture;
                if (user != null)
                {
                    photo.SetUser(ViewModel.ProtoService, user, 36);
                }
                else if (chat != null)
                {
                    photo.SetChat(ViewModel.ProtoService, chat, 36);
                }
            }

            if (args.Phase < 2)
            {
                args.RegisterUpdateCallback(OnContainerContentChanging);
            }

            args.Handled = true;
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
