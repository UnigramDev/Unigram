using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System;
using Telegram.Collections;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Controls.Cells;
using Telegram.Controls.Media;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Telegram.ViewModels.Stories;
using Telegram.Views.Create;

namespace Telegram.Views.Popups
{
    public sealed partial class ContactsPopup : ContentPopup
    {
        public ContactsViewModel ViewModel => DataContext as ContactsViewModel;

        public ContactsPopup()
        {
            InitializeComponent();
            InitializeSearch();

            Title = Strings.Contacts;
        }

        private void InitializeSearch()
        {
            var debouncer = new EventDebouncer<TextChangedEventArgs>(Constants.TypingTimeout, handler => SearchField.TextChanged += new TextChangedEventHandler(handler));
            debouncer.Invoked += async (s, args) =>
            {
                var items = ViewModel.Search;
                if (items != null && string.Equals(SearchField.Text, items.Query))
                {
                    await items.LoadMoreItemsAsync(1);
                    await items.LoadMoreItemsAsync(2);
                }
            };
        }

        private void OnChoosingItemContainer(ListViewBase sender, ChoosingItemContainerEventArgs args)
        {
            if (args.ItemContainer == null)
            {
                args.ItemContainer = new TextListViewItem();
                args.ItemContainer.Style = sender.ItemContainerStyle;
                args.ItemContainer.ContentTemplate = sender.ItemTemplate;
                args.ItemContainer.ContextRequested += OnContextRequested;
            }

            args.IsContainerPrepared = true;
        }

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                //var photo = content.Children[0] as ProfilePicture;
                //photo.Source = null;

                return;
            }

            var content = args.ItemContainer.ContentTemplateRoot as ProfileCell;

            if (args.Item is ActiveStoriesViewModel activeStories)
            {
                content.UpdateActiveStories(ViewModel.ClientService, activeStories, args, OnContainerContentChanging);
            }
            else if (args.Item is User user)
            {
                content.UpdateUser(ViewModel.ClientService, user, args, OnContainerContentChanging);
            }
        }

        private void DialogsSearchListView_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.ItemContainer.ContentTemplateRoot is ProfileCell content)
            {
                if (args.InRecycleQueue)
                {
                    content.RecycleSearchResult();
                }
                else
                {
                    content.UpdateSearchResult(ViewModel.ClientService, args, DialogsSearchListView_ContainerContentChanging);
                }
            }
        }

        private void OnContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
            var item = ScrollingHost.ItemFromContainer(sender);
            var user = item as User;

            if (item is SearchResult result)
            {
                user = result.User;
            }

            var flyout = new MenuFlyout();
            flyout.CreateFlyoutItem(ViewModel.SendMessage, user, Strings.SendMessage, Icons.ChatEmpty);
            flyout.CreateFlyoutItem(ViewModel.CreateSecretChat, user, Strings.StartEncryptedChat, Icons.Timer);
            flyout.CreateFlyoutItem(ViewModel.VoiceCall, user, Strings.Call, Icons.Call);
            flyout.CreateFlyoutItem(ViewModel.VideoCall, user, Strings.VideoCall, Icons.Video);
            flyout.ShowAt(sender, args);
        }

        private void OnItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is User user)
            {
                Hide();
                ViewModel.SendMessage(user);
            }
            else if (e.ClickedItem is SearchResult result)
            {
                Hide();

                if (result.Chat != null)
                {
                    ViewModel.NavigationService.NavigateToChat(result.Chat);
                }
                else if (result.User != null)
                {
                    ViewModel.SendMessage(result.User);
                }
            }
        }

        private void NewGroup_Click(object sender, RoutedEventArgs e)
        {
            Hide();
            _ = ViewModel.NavigationService.ShowPopupAsync(new NewGroupPopup());
        }

        private void NewContact_Click(object sender, RoutedEventArgs e)
        {
            Hide();
            _ = ViewModel.NavigationService.ShowPopupAsync(new NewContactPopup());
        }

        private void NewChannel_Click(object sender, RoutedEventArgs e)
        {
            Hide();
            _ = ViewModel.NavigationService.ShowPopupAsync(new NewChannelPopup());
        }

        #region Search

        private void Search_Click(object sender, RoutedEventArgs e)
        {
            if (SearchField.FocusState == FocusState.Keyboard && sender == SearchField)
            {
                return;
            }

            Search_TextChanged(null, null);
        }

        private void Search_LostFocus(object sender, RoutedEventArgs e)
        {
            SearchReset();
        }

        private async void Search_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (SearchField.FocusState == FocusState.Unfocused && string.IsNullOrWhiteSpace(SearchField.Text))
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(SearchField.Text))
            {
                SearchReset();
            }
            else
            {
                ContactsPanel.Visibility = Visibility.Collapsed;
                FindName(nameof(ContactsSearchListView));

                var items = ViewModel.Search = new SearchUsersCollection(ViewModel.ClientService, SearchField.Text);
                await items.LoadMoreItemsAsync(0);
            }
        }

        private void SearchReset()
        {
            SearchField.Text = string.Empty;

            if (ContactsPanel != null)
            {
                ContactsPanel.Visibility = Visibility.Visible;
            }

            ViewModel.Search = null;
        }

        #endregion

        private void ScrollingHeader_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (EmptyState != null)
            {
                EmptyState.Margin = new Thickness(0, e.NewSize.Height - 36, 0, 0);
            }
        }

        private void EmptyState_Loaded(object sender, RoutedEventArgs e)
        {
            EmptyState.Margin = new Thickness(0, ScrollingHeader.ActualHeight - 36, 0, 0);
        }
    }
}
