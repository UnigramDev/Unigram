using System;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Controls;
using Unigram.ViewModels.Delegates;
using Unigram.ViewModels.Settings;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Input;

namespace Unigram.Views.Settings
{
    public sealed partial class SettingsBlockedChatsPage : HostedPage, IFileDelegate
    {
        public SettingsBlockedChatsViewModel ViewModel => DataContext as SettingsBlockedChatsViewModel;

        public SettingsBlockedChatsPage()
        {
            InitializeComponent();
            DataContext = TLContainer.Current.Resolve<SettingsBlockedChatsViewModel, IFileDelegate>(this);
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
                args.ItemContainer = new ListViewItem();
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

            User user = null;
            Chat chat = null;
            ViewModel.CacheService.TryGetUser(messageSender, out user);
            ViewModel.CacheService.TryGetChat(messageSender, out chat);

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
                    photo.Source = PlaceholderHelper.GetUser(ViewModel.ProtoService, user, 36);
                }
                else if (chat != null)
                {
                    photo.Source = PlaceholderHelper.GetChat(ViewModel.ProtoService, chat, 36);
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

        public void UpdateFile(Telegram.Td.Api.File file)
        {
            foreach (MessageSender sender in ScrollingHost.Items)
            {
                if (ViewModel.CacheService.TryGetUser(sender, out User user) && user.UpdateFile(file))
                {
                    var container = ScrollingHost.ContainerFromItem(sender) as SelectorItem;
                    if (container == null)
                    {
                        return;
                    }

                    var content = container.ContentTemplateRoot as Grid;

                    var photo = content.Children[0] as ProfilePicture;
                    photo.Source = PlaceholderHelper.GetUser(null, user, 36);
                }
                else if (ViewModel.CacheService.TryGetChat(sender, out Chat chat) && chat.UpdateFile(file))
                {
                    var container = ScrollingHost.ContainerFromItem(sender) as SelectorItem;
                    if (container == null)
                    {
                        return;
                    }

                    var content = container.ContentTemplateRoot as Grid;

                    var photo = content.Children[0] as ProfilePicture;
                    photo.Source = PlaceholderHelper.GetChat(null, chat, 36);
                }
            }
        }
    }
}
