using Telegram.Common;
using Telegram.Controls;
using Telegram.Controls.Cells;
using Telegram.Controls.Media;
using Telegram.Streams;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;

namespace Telegram.Views.Stories.Popups
{
    public sealed partial class StoryInteractionsPopup : ContentPopup
    {
        public StoryInteractionsViewModel ViewModel => DataContext as StoryInteractionsViewModel;

        public StoryInteractionsPopup()
        {
            InitializeComponent();

            //Title = Strings.Reactions;
            SecondaryButtonText = Strings.Close;
        }

        public override void OnNavigatedTo()
        {
            if (ViewModel.Story.CanGetViewers && !ViewModel.Story.HasExpiredViewers)
            {
                VerticalContentAlignment = VerticalAlignment.Stretch;
                PrimaryButtonText = string.Empty;
            }
            else
            {
                FindName(nameof(ExpiredRoot));

                VerticalContentAlignment = VerticalAlignment.Center;

                if (ViewModel.IsPremiumAvailable)
                {
                    PrimaryButtonText = Strings.LearnMore;
                    PremiumHint.Visibility = Visibility.Visible;
                }
                else
                {
                    PrimaryButtonText = string.Empty;
                    PremiumHint.Visibility = Visibility.Collapsed;
                }
            }
        }

        // 446.667,
        //  48.6667

        private void OnChoosingItemContainer(ListViewBase sender, ChoosingItemContainerEventArgs args)
        {
            if (args.ItemContainer == null)
            {
                args.ItemContainer = new TextListViewItem();
                args.ItemContainer.Style = ScrollingHost.ItemContainerStyle;
                args.ItemContainer.ContentTemplate = ScrollingHost.ItemTemplate;
                args.ItemContainer.ContextRequested += OnContextRequested;
            }

            args.IsContainerPrepared = true;
        }

        private void OnContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
            var element = sender as FrameworkElement;
            var viewer = ScrollingHost.ItemFromContainer(sender) as StoryViewer;

            if (viewer == null || !ViewModel.ClientService.TryGetUser(viewer.UserId, out User user))
            {
                return;
            }

            var flyout = new MenuFlyout();

            if (viewer.BlockList is null)
            {
                flyout.CreateFlyoutItem(ViewModel.HideStories, viewer, string.Format(Strings.StoryHideFrom, user.FirstName), Icons.StoriesOff);
            }
            else if (viewer.BlockList is BlockListStories)
            {
                flyout.CreateFlyoutItem(ViewModel.ShowStories, viewer, string.Format(Strings.StoryShowTo, user.FirstName), Icons.Stories);
            }

            if (user.IsContact)
            {
                flyout.CreateFlyoutItem(DeleteContact, viewer, Strings.DeleteContact, Icons.Delete, dangerous: true);
            }
            else if (viewer.BlockList is not BlockListMain)
            {
                flyout.CreateFlyoutItem(BlockUser, viewer, Strings.BlockUser, Icons.HandRight, dangerous: true);
            }
            else if (viewer.BlockList is BlockListMain)
            {
                flyout.CreateFlyoutItem(ViewModel.UnblockUser, viewer, Strings.Unblock, Icons.HandRight);
            }

            args.ShowAt(flyout, element);
        }

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }
            else if (args.ItemContainer.ContentTemplateRoot is Grid content)
            {
                var cell = content.Children[0] as ProfileCell;
                var animated = content.Children[1] as CustomEmojiIcon;

                if (args.Item is StoryViewer viewer)
                {
                    cell.UpdateStoryViewer(ViewModel.ClientService, args, OnContainerContentChanging);
                    animated.Source = viewer.ChosenReactionType != null
                        ? new ReactionFileSource(ViewModel.ClientService, viewer.ChosenReactionType)
                        : null;
                }
            }
        }

        private void OnItemClick(object sender, ItemClickEventArgs e)
        {
            Hide();
            ViewModel.OpenChat(e.ClickedItem);
        }

        private void DeleteContact(StoryViewer viewer)
        {
            ViewModel.DeleteContact(viewer, ScrollingHost.ContainerFromItem(viewer));
        }

        private void BlockUser(StoryViewer viewer)
        {
            ViewModel.BlockUser(viewer, ScrollingHost.ContainerFromItem(viewer));
        }
    }
}
