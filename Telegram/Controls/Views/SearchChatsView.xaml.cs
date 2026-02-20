//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Telegram.Collections;
using Telegram.Common;
using Telegram.Controls.Cells;
using Telegram.Controls.Media;
using Telegram.Navigation;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Telegram.Views;
using Telegram.Views.Profile;
using Windows.Foundation;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;

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

    public sealed partial class SearchChatsView : UserControl, INavigablePage
    {
        private SearchChatsViewModel _viewModel;
        public SearchChatsViewModel ViewModel => _viewModel ??= DataContext as SearchChatsViewModel;

        public SearchChatsView()
        {
            InitializeComponent();
        }

        public Thickness PaddingImpl
        {
            get => TopChats.Padding;
            set
            {
                ItemsHost.Padding = value;
                TopChats.Padding = value;
                TopChats.Margin = new Thickness(-value.Left, 0, -value.Right, 0);
            }
        }

        public void Activate()
        {
            ViewModel.Activate();

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

            ItemsHost.ForEach<ProfileCell, SearchResult>((content, result) =>
            {
                var badge = content.Content as BadgeControl;
                if (badge != null)
                {
                    if (result.Chat != null)
                    {
                        var muted = ViewModel.ClientService.Notifications.IsMuted(result.Chat);
                        badge.IsUnmuted = !muted;
                        badge.Text = result.Chat.UnreadCount > 0 ? result.Chat.UnreadCount.ToString() : string.Empty;
                        badge.Visibility = result.Chat.UnreadCount > 0 || result.Chat.IsMarkedAsUnread
                            ? Visibility.Visible
                            : Visibility.Collapsed;
                    }
                    else
                    {
                        badge.Visibility = Visibility.Collapsed;
                    }
                }
            });
        }

        public void Deactivate()
        {
            ViewModel.Deactivate();
        }

        public event ItemClickEventHandler ItemClick;

        public void RaiseItemClick(ItemClickEventArgs e)
        {
            ItemClick?.Invoke(this, e);
        }

        public event TypedEventHandler<UIElement, ItemContextRequestedEventArgs> ItemContextRequested;

        public ListView Root => ItemsHost;

        public bool AreTabsVisible
        {
            get => ChatFolders.Visibility == Visibility.Visible;
            set => ChatFolders.Visibility = value
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        #region Recycle

        private readonly Dictionary<string, HashSet<SelectorItem>> _typeToItemHashSetMapping = new()
        {
            { "HeaderTemplate", new HashSet<SelectorItem>() },
            { "ProfileTemplate", new HashSet<SelectorItem>() },
            { "MessageTemplate", new HashSet<SelectorItem>() },
        };

        private void OnChoosingItemContainer(ListViewBase sender, ChoosingItemContainerEventArgs args)
        {
            var typeName = args.Item switch
            {
                IKeyedCollection => "HeaderTemplate",
                SearchResult => "ProfileTemplate",
                Message => "MessageTemplate",
                _ => null
            };

            var relevantHashSet = _typeToItemHashSetMapping[typeName];

            // args.ItemContainer is used to indicate whether the ListView is proposing an
            // ItemContainer (ListViewItem) to use. If args.Itemcontainer != null, then there was a
            // recycled ItemContainer available to be reused.
            if (args.ItemContainer is SearchListViewItem container)
            {
                if (container.TypeName.Equals(typeName))
                {
                    // Suggestion matches what we want, so remove it from the recycle queue
                    relevantHashSet.Remove(args.ItemContainer);
                }
                else
                {
                    // The ItemContainer's datatemplate does not match the needed
                    // datatemplate.
                    // Don't remove it from the recycle queue, since XAML will resuggest it later
                    args.ItemContainer = null;
                }
            }

            // If there was no suggested container or XAML's suggestion was a miss, pick one up from the recycle queue
            // or create a new one
            if (args.ItemContainer == null)
            {
                // See if we can fetch from the correct list.
                if (relevantHashSet.Count > 0)
                {
                    // Unfortunately have to resort to LINQ here. There's no efficient way of getting an arbitrary
                    // item from a hashset without knowing the item. Queue isn't usable for this scenario
                    // because you can't remove a specific element (which is needed in the block above).
                    args.ItemContainer = relevantHashSet.First();
                    relevantHashSet.Remove(args.ItemContainer);
                }
                else
                {
                    // There aren't any (recycled) ItemContainers available. So a new one
                    // needs to be created.
                    var item = new SearchListViewItem(typeName);
                    item.ContentTemplate = Resources[typeName] as DataTemplate;
                    item.Style = args.Item is IKeyedCollection ? Resources["HeaderListViewItemStyle"] as Style : sender.ItemContainerStyle;
                    item.ContextRequested += OnContextRequested;
                    args.ItemContainer = item;
                }
            }

            // Indicate to XAML that we picked a container for it
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

                if (args.ItemContainer is SearchListViewItem container)
                {
                    // XAML has indicated that the item is no longer being shown, so add it to the recycle queue
                    _typeToItemHashSetMapping[container.TypeName].Add(args.ItemContainer);
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

                var badge = content.Content as BadgeControl;
                if (badge != null)
                {
                    if (result.Chat != null)
                    {
                        var muted = ViewModel.ClientService.Notifications.IsMuted(result.Chat);
                        badge.IsUnmuted = !muted;
                        badge.Text = result.Chat.UnreadCount > 0 ? result.Chat.UnreadCount.ToString() : string.Empty;
                        badge.Visibility = result.Chat.UnreadCount > 0 || result.Chat.IsMarkedAsUnread
                            ? Visibility.Visible
                            : Visibility.Collapsed;
                    }
                    else
                    {
                        badge.Visibility = Visibility.Collapsed;
                    }
                }
            }
            else if (args.Item is Message message)
            {
                if (args.Phase == 0)
                {
                    args.RegisterUpdateCallback(2, OnContainerContentChanging);
                }
                else
                {
                    var content = args.ItemContainer.ContentTemplateRoot as ChatCell;
                    if (content == null)
                    {
                        return;
                    }

                    content.UpdateMessage(ViewModel.ClientService, message, false);
                }
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

            photo.Source = ProfilePictureSource.Chat(ViewModel.ClientService, chat);
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
            var result = ItemsHost.ItemFromContainer(sender) as SearchResult;
            if (result != null)
            {
                if (result.Type == SearchResultType.Recent && ViewModel.SelectedTab == 0)
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
                    MessageHelper.NavigateToMainWebApp(ViewModel.ClientService, ViewModel.NavigationService, user, string.Empty, new WebAppOpenModeFullSize());
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

        private void EmptyState_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is TextBlock textBlock)
            {
                textBlock.Text = string.Format(Strings.NoResultFoundFor2, ViewModel.Query);
            }
        }

        private int _prevSelectedIndex;

        private void ChatFolders_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ChatFolders.SelectedItem is SearchChatsTabItem page /*&& page.Type != MediaFrame.Content?.GetType()*/)
            {
                NavigationTransitionInfo transition = _prevSelectedIndex == -1
                ? new SuppressNavigationTransitionInfo()
                : new SlideNavigationTransitionInfo
                {
                    Effect = _prevSelectedIndex < ChatFolders.SelectedIndex
                        ? SlideNavigationTransitionEffect.FromRight
                        : SlideNavigationTransitionEffect.FromLeft
                };

                _prevSelectedIndex = ChatFolders.SelectedIndex;
                MediaFrame.Navigate(page.Type, null, transition);
                ShowHideSearch(page.Type == typeof(BlankPage));
            }
        }

        public void OnBackRequested(BackRequestedRoutedEventArgs args)
        {
            if (MediaFrame.Content is INavigablePage tabPage)
            {
                tabPage.OnBackRequested(args);
            }
        }

        private bool _searchCollapsed;

        private void ShowHideSearch(bool show)
        {
            if (_searchCollapsed != show)
            {
                return;
            }

            _searchCollapsed = !show;
            SearchRoot.Visibility = Visibility.Visible;
            SearchRoot.IsHitTestVisible = false;

            MediaRoot.Visibility = Visibility.Visible;
            MediaRoot.IsHitTestVisible = false;

            var effect = show
                ? SlideNavigationTransitionEffect.FromLeft
                : SlideNavigationTransitionEffect.FromRight;

            // Ported from https://github.com/microsoft/microsoft-ui-xaml/blob/d37afef65a0fc3219ba6b349301d685099fb129d/src/dxaml/phone/lib/ThemeTransitions.cpp#L1543
            float translationExitOffset = 150;
            float translationEntranceOffset = -200;
            var inControlPoint1 = new Vector2(0.1f, 0.9f);
            var inControlPoint2 = new Vector2(0.2f, 1.0f);
            var outControlPoint1 = new Vector2(0.7f, 0.0f);
            var outControlPoint2 = new Vector2(1.0f, .5f);
            uint outDuration = 150;
            uint inDuration = 300;
            float reverseTranslationFactor = effect == SlideNavigationTransitionEffect.FromLeft ? 1 : -1;

            ElementCompositionPreview.SetIsTranslationEnabled(SearchRoot, true);
            var visual = ElementComposition.GetElementVisual(SearchRoot);

            var compositor = BootStrapper.Current.Compositor;

            var opacity = compositor.CreateScalarKeyFrameAnimation();
            var translation = compositor.CreateScalarKeyFrameAnimation();

            if (show)
            {
                var easing = compositor.CreateCubicBezierEasingFunction(inControlPoint1, inControlPoint2);

                opacity.InsertKeyFrame(0, 0);
                opacity.InsertKeyFrame(1, 1);
                opacity.Duration = TimeSpan.FromMilliseconds(outDuration);

                translation.InsertKeyFrame(0, translationEntranceOffset * reverseTranslationFactor);
                translation.InsertKeyFrame(1, 0, easing);
                translation.Duration = TimeSpan.FromMilliseconds(outDuration + inDuration);

                opacity.DelayTime = TimeSpan.FromMilliseconds(outDuration);
                opacity.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;

                translation.DelayTime = TimeSpan.FromMilliseconds(outDuration);
                translation.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;
            }
            else
            {
                var easing = compositor.CreateCubicBezierEasingFunction(outControlPoint1, outControlPoint2);

                opacity.InsertKeyFrame(0, 1);
                opacity.InsertKeyFrame(1, 0);
                opacity.Duration = TimeSpan.FromMilliseconds(outDuration);

                translation.InsertKeyFrame(0, 0);
                translation.InsertKeyFrame(1, translationExitOffset * reverseTranslationFactor, easing);
                translation.Duration = TimeSpan.FromMilliseconds(outDuration);
            }

            var batch = compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
            batch.Completed += (s, args) =>
            {
                if (_searchCollapsed)
                {
                    SearchRoot.Visibility = Visibility.Collapsed;
                    MediaRoot.IsHitTestVisible = true;
                }
                else
                {
                    MediaRoot.Visibility = Visibility.Collapsed;
                    SearchRoot.IsHitTestVisible = true;
                }
            };

            visual.StartAnimation("Opacity", opacity);
            visual.StartAnimation("Translation.X", translation);

            batch.End();
        }

        #region Media

        private long _itemsSourceToken;
        private long _selectionModeToken;

        private void OnNavigating(object sender, NavigatingCancelEventArgs e)
        {
            if (MediaFrame.Content is ProfileTabPage tabPage)
            {
                tabPage.ScrollingHost.UnregisterPropertyChangedCallback(ItemsControl.ItemsSourceProperty, ref _itemsSourceToken);
                tabPage.ScrollingHost.UnregisterPropertyChangedCallback(ListViewBase.SelectionModeProperty, ref _selectionModeToken);
            }
        }

        private void OnNavigated(object sender, NavigationEventArgs e)
        {
            if (e.Content is not ProfileTabPage tabPage)
            {
                return;
            }

            if (tabPage is SearchPostsTabPage)
            {
                tabPage.DataContext = ViewModel.Posts;
            }

            if (tabPage.ScrollingHost.ItemsSource != null)
            {
                LoadMore(tabPage.ScrollingHost);
            }
            else
            {
                tabPage.ScrollingHost.RegisterPropertyChangedCallback(ItemsControl.ItemsSourceProperty, OnItemsSourceChanged, ref _itemsSourceToken);
            }

            tabPage.ScrollingHost.RegisterPropertyChangedCallback(ListViewBase.SelectionModeProperty, OnSelectionModeChanged, ref _selectionModeToken);
        }

        private void OnItemsSourceChanged(DependencyObject sender, DependencyProperty dp)
        {
            if (MediaFrame.Content is not ProfileTabPage tabPage || tabPage.ScrollingHost is not ListViewBase scrollingHost)
            {
                return;
            }

            LoadMore(scrollingHost);
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            MediaFrame.MinHeight = e.NewSize.Height;

            if (MediaFrame.Content is not ProfileTabPage tabPage || tabPage.ScrollingHost is not ListViewBase scrollingHost)
            {
                return;
            }

            LoadMore(scrollingHost);
        }

        private void OnViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
        {
            //BackButton.RequestedTheme = ScrollingHost.VerticalOffset < ProfileHeader.ActualHeight - 16
            //    ? ProfileHeader.HeaderTheme
            //    : ElementTheme.Default;

            //if (ProfileHeader.Visibility == Visibility.Visible)
            //{
            //    ProfileHeader.ViewChanged(ScrollingHost.VerticalOffset);
            //}

            if (MediaFrame.Content is not ProfileTabPage tabPage || tabPage.ScrollingHost is not ListViewBase scrollingHost)
            {
                return;
            }

            LoadMore(scrollingHost);

            var index = scrollingHost.ItemsPanelRoot switch
            {
                ItemsStackPanel stackPanel => stackPanel.FirstVisibleIndex,
                ItemsWrapGrid wrapGrid => wrapGrid.FirstVisibleIndex,
                _ => -1
            };

            if (index < 0 || index >= scrollingHost.Items.Count)
            {
                return;
            }

            //var container = scrollingHost.Items[index];
            //if (container is MessageWithOwner message)
            //{
            //    DateHeaderLabel.Text = Formatter.Date(message.Date, Strings.formatterMonthYear);
            //}
            //else if (container is StoryViewModel story)
            //{
            //    DateHeaderLabel.Text = Formatter.Date(story.Date, Strings.formatterMonthYear);
            //}
            //else
            //{
            //    return;
            //}

            //_dateHeaderTimer.Stop();
            //_dateHeaderTimer.Start();
            //ShowHideDateHeader(ScrollingHost.VerticalOffset > ProfileHeader.ActualHeight, true);
        }

        private bool _loadingMore;

        private async void LoadMore(ListViewBase scrollingHost)
        {
            if (_loadingMore)
            {
                return;
            }

            _loadingMore = true;

            uint loadedMore = 0;
            int lastCacheIndex = scrollingHost.ItemsPanelRoot switch
            {
                ItemsStackPanel stackPanel => stackPanel.LastCacheIndex,
                ItemsWrapGrid wrapGrid => wrapGrid.LastCacheIndex,
                _ => -1
            };

            var needsMore = lastCacheIndex == scrollingHost.Items.Count - 1;
            needsMore |= scrollingHost.ActualHeight < ScrollingHost.ActualHeight;

            if (needsMore && scrollingHost.ItemsSource is ISupportIncrementalLoading supportIncrementalLoading && supportIncrementalLoading.HasMoreItems)
            {
                var result = await supportIncrementalLoading.LoadMoreItemsAsync(50);
                loadedMore = result.Count;
            }

            _loadingMore = false;

            if (loadedMore > 0)
            {
                LoadMore(scrollingHost);
            }
        }

        #endregion

        #region Selection

        private string ConvertSelection(int count)
        {
            return Locale.Declension(Strings.R.messages, count);
        }

        private void OnSelectionModeChanged(DependencyObject sender, DependencyProperty dp)
        {
            if (sender is ListViewBase selector)
            {
                ShowHideManagePanel(selector.SelectionMode == ListViewSelectionMode.Multiple);
            }
        }

        private bool _manageCollapsed = true;

        private void ShowHideManagePanel(bool show)
        {
            if (_manageCollapsed != show)
            {
                return;
            }

            _manageCollapsed = !show;
            ManagePanel.Visibility = Visibility.Visible;

            var manage = ElementComposition.GetElementVisual(ManagePanel);
            ElementCompositionPreview.SetIsTranslationEnabled(ManagePanel, true);
            manage.Opacity = show ? 0 : 1;

            var batch = manage.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
            batch.Completed += (s, args) =>
            {
                ManagePanel.Visibility = _manageCollapsed
                    ? Visibility.Collapsed
                    : Visibility.Visible;
            };

            var offset1 = manage.Compositor.CreateVector3KeyFrameAnimation();
            offset1.InsertKeyFrame(show ? 0 : 1, new Vector3(0, 48, 0));
            offset1.InsertKeyFrame(show ? 1 : 0, new Vector3(0, 0, 0));

            var opacity1 = manage.Compositor.CreateScalarKeyFrameAnimation();
            opacity1.InsertKeyFrame(show ? 0 : 1, 0);
            opacity1.InsertKeyFrame(show ? 1 : 0, 1);

            manage.StartAnimation("Translation", offset1);
            manage.StartAnimation("Opacity", opacity1);

            batch.End();
        }

        #endregion
    }

    public partial class SearchListViewItem : TextListViewItem
    {
        public string TypeName { get; }

        public SearchListViewItem(string typeName)
        {
            TypeName = typeName;
        }
    }
}
