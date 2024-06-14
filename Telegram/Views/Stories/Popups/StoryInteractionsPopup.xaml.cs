using Microsoft.Graphics.Canvas.Geometry;
using System;
using System.Collections.Generic;
using System.Numerics;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Controls.Cells;
using Telegram.Controls.Media;
using Telegram.Streams;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;

namespace Telegram.Views.Stories.Popups
{
    public sealed partial class StoryInteractionsPopup : ContentPopup
    {
        public StoryInteractionsViewModel ViewModel => DataContext as StoryInteractionsViewModel;

        public StoryInteractionsPopup()
        {
            InitializeComponent();

            SecondaryButtonText = Strings.Close;
        }

        private void OnOpened(ContentDialog sender, ContentDialogOpenedEventArgs args)
        {
            ShowHideSkeleton();

            if (ScrollingHost.ItemsPanelRoot != null)
            {
                ScrollingHost.ItemsPanelRoot.MinHeight = ScrollingHost.ActualHeight - 24;
            }
        }

        public override void OnNavigatedTo()
        {
            var story = ViewModel.Story;
            if (story?.InteractionInfo != null && story.CanGetInteractions && (story.ClientService.IsPremium || story.InteractionInfo.ReactionCount > 0))
            {
                ViewModel.Items.CollectionChanged += OnCollectionChanged;

                var premium = story.ClientService.IsPremium;
                var count = story.HasExpiredViewers && !premium
                    ? story.InteractionInfo.ReactionCount 
                    : story.InteractionInfo.ViewCount;

                if (count >= 9)
                {
                    VerticalContentAlignment = VerticalAlignment.Stretch;
                    ScrollingHost.Height = double.NaN;
                }
                else
                {
                    VerticalContentAlignment = VerticalAlignment.Center;
                    ScrollingHost.Height = count * 48 + 24;
                }

                PrimaryButtonText = string.Empty;

                SearchField.Visibility = premium && story.InteractionInfo.ViewCount >= 15
                    ? Visibility.Visible
                    : Visibility.Collapsed;

                SortBy.Visibility =  premium && story.InteractionInfo.ReactionCount >= 10
                    ? Visibility.Visible
                    : Visibility.Collapsed;

                if (story.PrivacySettings is not StoryPrivacySettingsEveryone || story.InteractionInfo.ViewCount < 20 || !premium)
                {
                    Navigation.Visibility = Visibility.Collapsed;
                    Title = premium
                        ? Locale.Declension(Strings.R.Views, story.InteractionInfo.ViewCount)
                        : Locale.Declension(Strings.R.Likes, story.InteractionInfo.ReactionCount);

                    Padding = new Thickness(0, 24, 0, 0);
                    SortBy.Margin = new Thickness(0, -36, 24, 12);

                    ExpiredFooter.Visibility = count < story.InteractionInfo.ViewCount
                        ? Visibility.Visible
                        : Visibility.Collapsed;
                }
                else
                {
                    Navigation.Visibility = Visibility.Visible;
                    Title = string.Empty;

                    Padding = new Thickness(0);
                    SortBy.Margin = new Thickness(0, 0, 24, 0);

                    ExpiredFooter.Visibility = Visibility.Collapsed;
                }
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

                Navigation.Visibility = Visibility.Collapsed;
                Title = story.InteractionInfo != null
                    ? Locale.Declension(Strings.R.Views, story.InteractionInfo.ViewCount)
                    : Strings.StatisticViews;

                Padding = new Thickness(0);

                ExpiredFooter.Visibility = Visibility.Collapsed;
            }
        }

        private void OnCollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            ShowHideSkeleton();
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
            var interaction = ScrollingHost.ItemFromContainer(sender) as StoryInteraction;
            if (interaction == null || !ViewModel.ClientService.TryGetUser(interaction.ActorId, out User user))
            {
                return;
            }

            var flyout = new MenuFlyout();

            if (interaction.BlockList is null)
            {
                flyout.CreateFlyoutItem(ViewModel.HideStories, interaction, string.Format(Strings.StoryHideFrom, user.FirstName), Icons.StoriesOff);
            }
            else if (interaction.BlockList is BlockListStories)
            {
                flyout.CreateFlyoutItem(ViewModel.ShowStories, interaction, string.Format(Strings.StoryShowTo, user.FirstName), Icons.Stories);
            }

            if (user.IsContact)
            {
                flyout.CreateFlyoutItem(DeleteContact, interaction, Strings.DeleteContact, Icons.Delete, destructive: true);
            }
            else if (interaction.BlockList is not BlockListMain)
            {
                flyout.CreateFlyoutItem(BlockUser, interaction, Strings.BlockUser, Icons.HandRight, destructive: true);
            }
            else if (interaction.BlockList is BlockListMain)
            {
                flyout.CreateFlyoutItem(ViewModel.UnblockUser, interaction, Strings.Unblock, Icons.HandRight);
            }

            flyout.ShowAt(sender, args);
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

                if (args.Item is StoryInteraction interaction)
                {
                    cell.UpdateStoryViewer(ViewModel.ClientService, args, OnContainerContentChanging);
                    animated.Source = interaction.Type is StoryInteractionTypeView view && view.ChosenReactionType != null
                        ? new ReactionFileSource(ViewModel.ClientService, view.ChosenReactionType)
                        : null;
                }
            }
        }

        private void OnItemClick(object sender, ItemClickEventArgs e)
        {
            Hide();
            ViewModel.OpenChat(e.ClickedItem);
        }

        private void DeleteContact(StoryInteraction interaction)
        {
            ViewModel.DeleteContact(interaction, ScrollingHost.ContainerFromItem(interaction));
        }

        private void BlockUser(StoryInteraction interaction)
        {
            ViewModel.BlockUser(interaction, ScrollingHost.ContainerFromItem(interaction));
        }

        private void SortBy_ContextRequested(object sender, RoutedEventArgs e)
        {
            var flyout = new MenuFlyout();

            flyout.CreateFlyoutItem(ViewModel.SortByReaction, Strings.SortByReactions, ViewModel.SortBy == StoryInteractionsSortBy.Reaction ? Icons.HeartFilled : Icons.Heart);
            flyout.CreateFlyoutItem(ViewModel.SortByTime, Strings.SortByTime, ViewModel.SortBy == StoryInteractionsSortBy.Time ? Icons.ClockFilled : Icons.Clock);

            flyout.CreateFlyoutSeparator();
            flyout.Items.Add(new MenuFlyoutLabel
            {
                Padding = new Thickness(12, 4, 12, 4),
                MaxWidth = 178,
                Text = Strings.StoryViewsSortDescription
            });

            flyout.ShowAt(sender as FrameworkElement, Windows.UI.Xaml.Controls.Primitives.FlyoutPlacementMode.BottomEdgeAlignedRight);
        }

        private bool _skeletonCollapsed = true;

        private void ShowHideSkeleton()
        {
            if (_skeletonCollapsed && ViewModel.Items.Count == 0 && ViewModel.Story != null && ScrollingHost.ItemsPanelRoot != null)
            {
                _skeletonCollapsed = false;
                ShowSkeleton();
            }
            else if (_skeletonCollapsed is false && ViewModel.Items.Count > 0 && ScrollingHost.ItemsPanelRoot != null)
            {
                _skeletonCollapsed = true;

                var visual = ElementCompositionPreview.GetElementChildVisual(ScrollingHost.ItemsPanelRoot);
                var animation = visual.Compositor.CreateScalarKeyFrameAnimation();
                animation.InsertKeyFrame(0, 1);
                animation.InsertKeyFrame(1, 0);

                visual.StartAnimation("Opacity", animation);
            }
        }

        private void ShowSkeleton()
        {
            if (ViewModel?.Story?.InteractionInfo == null)
            {
                return;
            }

            var size = ScrollingHost.ActualSize;
            var itemHeight = 6 + 36 + 6;

            var rows = Math.Min(ViewModel.Story.InteractionInfo.ViewCount, Math.Ceiling(size.Y / itemHeight));
            var shapes = new List<CanvasGeometry>();

            var maxWidth = (int)Math.Min(size.X - 32 - 12 - 12 - 48 - 12, 280);
            var random = new Random();

            for (int i = 0; i < rows; i++)
            {
                var y = itemHeight * i;

                shapes.Add(CanvasGeometry.CreateEllipse(null, 12 + 18, y + 6 + 18, 18, 18));
                shapes.Add(CanvasGeometry.CreateRoundedRectangle(null, 12 + 36 + 8, y + 6, random.Next(80, maxWidth), 18, 4, 4));
                shapes.Add(CanvasGeometry.CreateRoundedRectangle(null, 12 + 36 + 8, y + 6 + 18 + 4, random.Next(80, maxWidth), 14, 4, 4));
            }

            var compositor = Window.Current.Compositor;

            var geometries = shapes.ToArray();
            var path = compositor.CreatePathGeometry(new CompositionPath(CanvasGeometry.CreateGroup(null, geometries, CanvasFilledRegionDetermination.Winding)));

            var transparent = Color.FromArgb(0x00, 0xFF, 0xFF, 0xFF);
            var foregroundColor = Color.FromArgb(0x0F, 0xFF, 0xFF, 0xFF);
            var backgroundColor = Color.FromArgb(0x0F, 0xFF, 0xFF, 0xFF);

            var gradient = compositor.CreateLinearGradientBrush();
            gradient.StartPoint = new Vector2(0, 0);
            gradient.EndPoint = new Vector2(1, 0);
            gradient.ColorStops.Add(compositor.CreateColorGradientStop(0.0f, transparent));
            gradient.ColorStops.Add(compositor.CreateColorGradientStop(0.5f, foregroundColor));
            gradient.ColorStops.Add(compositor.CreateColorGradientStop(1.0f, transparent));

            var background = compositor.CreateRectangleGeometry();
            background.Size = size;
            var backgroundShape = compositor.CreateSpriteShape(background);
            backgroundShape.FillBrush = compositor.CreateColorBrush(backgroundColor);

            var foreground = compositor.CreateRectangleGeometry();
            foreground.Size = size;
            var foregroundShape = compositor.CreateSpriteShape(foreground);
            foregroundShape.FillBrush = gradient;

            var clip = compositor.CreateGeometricClip(path);
            var visual = compositor.CreateShapeVisual();
            visual.Clip = clip;
            visual.Shapes.Add(backgroundShape);
            visual.Shapes.Add(foregroundShape);
            visual.RelativeSizeAdjustment = Vector2.One;

            var animation = compositor.CreateVector2KeyFrameAnimation();
            animation.InsertKeyFrame(0, new Vector2(-size.X, 0));
            animation.InsertKeyFrame(1, new Vector2(size.X, 0));
            animation.IterationBehavior = AnimationIterationBehavior.Forever;
            animation.Duration = TimeSpan.FromSeconds(1);

            foregroundShape.StartAnimation("Offset", animation);

            ElementCompositionPreview.SetElementChildVisual(ScrollingHost.ItemsPanelRoot, visual);
        }

        private string ConvertSortBy(StoryInteractionsSortBy sortBy)
        {
            return sortBy switch
            {
                StoryInteractionsSortBy.Reaction => Icons.Heart,
                StoryInteractionsSortBy.Time or _ => Icons.Clock
            };
        }
    }
}
