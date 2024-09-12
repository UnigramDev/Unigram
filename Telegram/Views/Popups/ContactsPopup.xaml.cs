using System;
using System.Numerics;
using Telegram.Collections;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Controls.Cells;
using Telegram.Controls.Media;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Telegram.ViewModels.Stories;
using Telegram.Views.BasicGroups;
using Telegram.Views.Channels;
using Telegram.Views.Users;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;

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
            ViewModel.NavigationService.Navigate(typeof(BasicGroupCreateStep1Page));
        }

        private void NewContact_Click(object sender, RoutedEventArgs e)
        {
            Hide();
            ViewModel.NavigationService.Navigate(typeof(UserCreatePage));
        }

        private void NewChannel_Click(object sender, RoutedEventArgs e)
        {
            Hide();
            ViewModel.NavigationService.Navigate(typeof(ChannelCreateStep1Page));
        }

        #region Search

        private bool _searchCollapsed = true;

        private void ShowHideSearch(bool show)
        {
            if (_searchCollapsed != show)
            {
                return;
            }

            _searchCollapsed = !show;

            FindName(nameof(ContactsSearchListView));
            ContactsPanel.Visibility = Visibility.Visible;
            ContactsSearchListView.Visibility = Visibility.Visible;

            if (show)
            {
                //DialogsSearchPanel.Update();
                //SearchField.ControlledList = DialogsSearchPanel.Root;
                //Stories.Collapse();
            }

            var chats = ElementComposition.GetElementVisual(ContactsPanel);
            var panel = ElementComposition.GetElementVisual(ContactsSearchListView);

            chats.CenterPoint = panel.CenterPoint = new Vector3(ContactsPanel.ActualSize / 2, 0);

            var batch = panel.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
            batch.Completed += (s, args) =>
            {
                ContactsPanel.Visibility = _searchCollapsed ? Visibility.Visible : Visibility.Collapsed;
                ContactsSearchListView.Visibility = _searchCollapsed ? Visibility.Collapsed : Visibility.Visible;
            };

            var scale1 = panel.Compositor.CreateVector3KeyFrameAnimation();
            scale1.InsertKeyFrame(show ? 0 : 1, new Vector3(1.05f, 1.05f, 1));
            scale1.InsertKeyFrame(show ? 1 : 0, new Vector3(1));
            scale1.Duration = TimeSpan.FromMilliseconds(200);

            var scale2 = panel.Compositor.CreateVector3KeyFrameAnimation();
            scale2.InsertKeyFrame(show ? 0 : 1, new Vector3(1));
            scale2.InsertKeyFrame(show ? 1 : 0, new Vector3(0.95f, 0.95f, 1));
            scale2.Duration = TimeSpan.FromMilliseconds(200);

            var opacity1 = panel.Compositor.CreateScalarKeyFrameAnimation();
            opacity1.InsertKeyFrame(show ? 0 : 1, 0);
            opacity1.InsertKeyFrame(show ? 1 : 0, 1);
            opacity1.Duration = TimeSpan.FromMilliseconds(200);

            var opacity2 = panel.Compositor.CreateScalarKeyFrameAnimation();
            opacity2.InsertKeyFrame(show ? 0 : 1, 1);
            opacity2.InsertKeyFrame(show ? 1 : 0, 0);
            opacity2.Duration = TimeSpan.FromMilliseconds(200);

            panel.StartAnimation("Scale", scale1);
            panel.StartAnimation("Opacity", opacity1);

            chats.StartAnimation("Scale", scale2);
            chats.StartAnimation("Opacity", opacity2);

            batch.End();
        }

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
            //DialogsPanel.Visibility = Visibility.Visible;
            ShowHideSearch(false);

            SearchField.Text = string.Empty;

            if (ContactsPanel != null)
            {
                ContactsPanel.Visibility = Visibility.Visible;
            }

            ViewModel.Search = null;
        }

        #endregion
    }
}
