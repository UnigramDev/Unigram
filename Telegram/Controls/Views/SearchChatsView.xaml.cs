﻿using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System;
using Telegram.Collections;
using Telegram.Common;
using Telegram.Controls.Cells;
using Telegram.Controls.Media;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Windows.Foundation;

namespace Telegram.Controls.Views
{
    public partial class ItemContextRequestedEventArgs : EventArgs
    {
        public ItemContextRequestedEventArgs(object item, ContextRequestedEventArgs eventArgs)
        {
            Item = item;
            EventArgs = eventArgs;
        }

        public object Item { get; }

        public ContextRequestedEventArgs EventArgs { get; }
    }

    public sealed partial class SearchChatsView : UserControl
    {
        private SearchChatsViewModel _viewModel;
        public SearchChatsViewModel ViewModel => _viewModel ??= DataContext as SearchChatsViewModel;

        public SearchChatsView()
        {
            InitializeComponent();
        }

        public void Update()
        {
            TopChats.ForEach<Chat>((selector, chat) =>
            {
                var content = selector.ContentTemplateRoot as StackPanel;
                var grid = content.Children[0] as Grid;

                var badge = grid.Children[1] as BadgeControl;
                badge.Visibility = chat.UnreadCount > 0 ? Visibility.Visible : Visibility.Collapsed;
                badge.Text = chat.UnreadCount.ToString();

                var user = ViewModel.ClientService.GetUser(chat);
                if (user != null)
                {
                    var online = grid.Children[2] as Border;
                    online.Visibility = user.Status is UserStatusOnline ? Visibility.Visible : Visibility.Collapsed;
                }
            });
        }

        public event ItemClickEventHandler ItemClick;

        public event TypedEventHandler<UIElement, ItemContextRequestedEventArgs> ItemContextRequested;

        public ListView Root => ScrollingHost;

        public bool AreTabsVisible
        {
            get => ChatFolders.Visibility == Visibility.Visible;
            set => ChatFolders.Visibility = value
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        #region Recycle

        private void OnChoosingItemContainer(ListViewBase sender, ChoosingItemContainerEventArgs args)
        {
            if (args.ItemContainer == null)
            {
                args.ItemContainer = new TextListViewItem();
                args.ItemContainer.ContextRequested += OnContextRequested;
            }

            args.ItemContainer.Style = args.Item is IKeyedCollection ? Resources["HeaderListViewItemStyle"] as Style : sender.ItemContainerStyle;
            args.ItemContainer.ContentTemplate = sender.ItemTemplateSelector.SelectTemplate(args.Item, args.ItemContainer);
            args.IsContainerPrepared = true;
        }

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                if (args.ItemContainer.ContentTemplateRoot is ProfileCell content)
                {
                    content.RecycleSearchResult();
                }

                return;
            }
            else if (args.Item is IKeyedCollection header)
            {
                var content = args.ItemContainer.ContentTemplateRoot as Grid;
                if (content == null)
                {
                    return;
                }

                var text = content.Children[0] as TextBlock;
                var clear = content.Children[1] as Button;

                text.Text = header.Key;
                clear.Visibility = header.Key == Strings.Recent
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }
            else if (args.Item is SearchResult result)
            {
                var content = args.ItemContainer.ContentTemplateRoot as ProfileCell;
                if (content == null)
                {
                    return;
                }

                content.UpdateSearchResult(ViewModel.ClientService, args, OnContainerContentChanging);
            }
            else if (args.Item is Message message)
            {
                var content = args.ItemContainer.ContentTemplateRoot as ChatCell;
                if (content == null)
                {
                    return;
                }

                content.UpdateMessage(ViewModel.ClientService, message);
            }

            args.Handled = true;
        }

        private void TopChats_ChoosingItemContainer(ListViewBase sender, ChoosingItemContainerEventArgs args)
        {
            if (args.ItemContainer == null)
            {
                args.ItemContainer = new TextListViewItem();
                args.ItemContainer.ContentTemplate = sender.ItemTemplate;
                args.ItemContainer.Style = sender.ItemContainerStyle;
                args.ItemContainer.ContextRequested += TopChat_ContextRequested;
            }

            args.IsContainerPrepared = true;
        }

        private void TopChats_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }

            var content = args.ItemContainer.ContentTemplateRoot as StackPanel;
            var chat = args.Item as Chat;

            var grid = content.Children[0] as Grid;

            var photo = grid.Children[0] as ProfilePicture;
            var title = content.Children[1] as TextBlock;

            photo.SetChat(ViewModel.ClientService, chat, 48);
            title.Text = ViewModel.ClientService.GetTitle(chat, true);

            var badge = grid.Children[1] as BadgeControl;
            badge.Visibility = chat.UnreadCount > 0 ? Visibility.Visible : Visibility.Collapsed;
            badge.Text = chat.UnreadCount.ToString();

            var user = ViewModel.ClientService.GetUser(chat);
            if (user != null)
            {
                var online = grid.Children[2] as Border;
                online.Visibility = user.Status is UserStatusOnline ? Visibility.Visible : Visibility.Collapsed;
            }

            args.Handled = true;
        }

        #endregion

        #region Context menu

        private void OnContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
            var result = ScrollingHost.ItemFromContainer(sender) as SearchResult;
            if (result != null)
            {
                if (result.Type == SearchResultType.Recent)
                {
                    var flyout = new MenuFlyout();
                    flyout.CreateFlyoutItem(ViewModel.RemoveRecentChat, result, Strings.DeleteFromRecent, Icons.Delete);
                    flyout.ShowAt(sender, args);
                }
                else
                {
                    // TODO: forward ContextRequested event to parent
                    ItemContextRequested?.Invoke(sender, new ItemContextRequestedEventArgs(result, args));
                }
            }
        }

        private void TopChat_ContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
            var chat = TopChats.ItemFromContainer(sender) as Chat;

            var flyout = new MenuFlyout();
            flyout.CreateFlyoutItem(ViewModel.RemoveTopChat, chat, Strings.Delete, Icons.Delete);
            flyout.ShowAt(sender, args);
        }

        #endregion

        private void OnItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is SearchResult result && result.Type == SearchResultType.RecentWebApps)
            {
                var user = result.User ?? ViewModel.ClientService.GetUser(result.Chat);
                if (user == null)
                {
                    return;
                }

                if (user.Type is UserTypeBot { HasMainWebApp: true })
                {
                    MessageHelper.NavigateToMainWebApp(ViewModel.ClientService, ViewModel.NavigationService, user, string.Empty);
                    ItemClick?.Invoke(this, null);
                    return;
                }
            }

            ItemClick?.Invoke(this, e);
        }

        #region Filters (not implemented yet)

        private void Search_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            //if (rpMasterTitlebar.SelectedIndex == 0 && e.Key == Windows.System.VirtualKey.Back)
            //{
            //    if (SearchField.SelectionStart == 0 && SearchField.SelectionLength == 0)
            //    {
            //        if (ViewModel.Chats.SearchFilters?.Count > 0)
            //        {
            //            e.Handled = true;
            //            ViewModel.Chats.SearchFilters.RemoveAt(ViewModel.Chats.SearchFilters.Count - 1);
            //            ViewModel.Chats.Search.UpdateQuery(SearchField.Text);
            //            return;
            //        }
            //    }
            //}
        }

        private void SearchFilters_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            //if (args.Item is ISearchChatsFilter filter)
            //{
            //    var content = args.ItemContainer.ContentTemplateRoot as StackPanel;
            //    if (content == null)
            //    {
            //        return;
            //    }

            //    var glyph = content.Children[0] as TextBlock;
            //    glyph.Text = filter.Glyph ?? string.Empty;

            //    var title = content.Children[1] as TextBlock;
            //    title.Text = filter.Text ?? string.Empty;
            //}
        }

        private void SearchFilters_ItemClick(object sender, ItemClickEventArgs e)
        {
            //if (e.ClickedItem is ISearchChatsFilter filter)
            //{
            //    ViewModel.Chats.SearchFilters.Add(filter);
            //    SearchField.Text = string.Empty;

            //    //ViewModel.Chats.Search = new SearchChatsCollection(ViewModel.ClientService, SearchField.Text, ViewModel.Chats.SearchFilters);
            //}
        }

        #endregion

        private void ClearRecentChats_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.ClearRecentChats();
        }
    }
}
