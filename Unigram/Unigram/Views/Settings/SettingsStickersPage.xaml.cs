using System;
using System.Collections.Generic;
using System.Linq;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Converters;
using Unigram.ViewModels.Settings;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;

namespace Unigram.Views.Settings
{
    public sealed partial class SettingsStickersPage : HostedPage
    {
        public SettingsStickersViewModel ViewModel => DataContext as SettingsStickersViewModel;

        private readonly Dictionary<string, DataTemplate> _typeToTemplateMapping = new Dictionary<string, DataTemplate>();
        private readonly Dictionary<string, HashSet<SelectorItem>> _typeToItemHashSetMapping = new Dictionary<string, HashSet<SelectorItem>>();

        private readonly AnimatedListHandler<StickerSetInfo> _handler;

        public SettingsStickersPage()
        {
            InitializeComponent();

            _handler = new AnimatedListHandler<StickerSetInfo>(List);

            _typeToItemHashSetMapping.Add("AnimatedItemTemplate", new HashSet<SelectorItem>());
            _typeToItemHashSetMapping.Add("VideoItemTemplate", new HashSet<SelectorItem>());
            _typeToItemHashSetMapping.Add("ItemTemplate", new HashSet<SelectorItem>());

            _typeToTemplateMapping.Add("AnimatedItemTemplate", Resources["AnimatedItemTemplate"] as DataTemplate);
            _typeToTemplateMapping.Add("VideoItemTemplate", Resources["VideoItemTemplate"] as DataTemplate);
            _typeToTemplateMapping.Add("ItemTemplate", Resources["ItemTemplate"] as DataTemplate);
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            ViewModel.Aggregator.Subscribe(this);
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            ViewModel.Aggregator.Unsubscribe(this);
        }

        private void FeaturedStickers_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(SettingsStickersPage), (int)StickersType.Trending, new SlideNavigationTransitionInfo { Effect = SlideNavigationTransitionEffect.FromRight });
        }

        private void ArchivedStickers_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(SettingsStickersPage), (int)StickersType.Archived, new SlideNavigationTransitionInfo { Effect = SlideNavigationTransitionEffect.FromRight });
        }

        private void ArchivedMasks_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(SettingsStickersPage), (int)StickersType.MasksArchived, new SlideNavigationTransitionInfo { Effect = SlideNavigationTransitionEffect.FromRight });
        }

        private void Masks_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(SettingsStickersPage), (int)StickersType.Masks, new SlideNavigationTransitionInfo { Effect = SlideNavigationTransitionEffect.FromRight });
        }

        private void Reaction_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(SettingsQuickReactionPage), null, infoOverride: new SlideNavigationTransitionInfo { Effect = SlideNavigationTransitionEffect.FromRight });
        }

        private void ListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            ViewModel.StickerSetOpenCommand.Execute(e.ClickedItem);
        }

        private void ListView_DragItemsCompleted(ListViewBase sender, DragItemsCompletedEventArgs args)
        {
            if (args.DropResult == Windows.ApplicationModel.DataTransfer.DataPackageOperation.Move)
            {
                ViewModel.ReorderCommand.Execute(args.Items.FirstOrDefault());
            }
        }

        #region Recycle

        private void OnChoosingItemContainer(ListViewBase sender, ChoosingItemContainerEventArgs args)
        {
            var stickerSet = args.Item as StickerSetInfo;
            var cover = stickerSet.GetThumbnail();

            var typeName = cover?.Type switch
            {
                StickerTypeAnimated => "AnimatedItemTemplate",
                StickerTypeVideo => "VideoItemTemplate",
                _ => "ItemTemplate"
            };
            var relevantHashSet = _typeToItemHashSetMapping[typeName];

            // args.ItemContainer is used to indicate whether the ListView is proposing an
            // ItemContainer (ListViewItem) to use. If args.Itemcontainer != null, then there was a
            // recycled ItemContainer available to be reused.
            if (args.ItemContainer != null)
            {
                if (args.ItemContainer.Tag.Equals(typeName))
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
                    var item = new ListViewItem();
                    item.ContentTemplate = _typeToTemplateMapping[typeName];
                    item.Style = sender.ItemContainerStyle;
                    item.Tag = typeName;
                    item.ContextRequested += StickerSet_ContextRequested;
                    args.ItemContainer = item;
                }
            }

            // Indicate to XAML that we picked a container for it
            args.IsContainerPrepared = true;
        }

        private async void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }

            var content = args.ItemContainer.ContentTemplateRoot as Grid;
            var stickerSet = args.Item as StickerSetInfo;

            var title = content.Children[1] as TextBlock;
            title.Text = stickerSet.Title;

            var subtitle = content.Children[2] as TextBlock;
            subtitle.Text = Locale.Declension("Stickers", stickerSet.Size);

            var cover = stickerSet.GetThumbnail();
            if (cover == null)
            {
                return;
            }

            var file = cover.StickerValue;
            if (file.Local.IsDownloadingCompleted)
            {
                if (content.Children[0] is Image photo)
                {
                    photo.Source = await PlaceholderHelper.GetWebPFrameAsync(file.Local.Path, 48);
                    ElementCompositionPreview.SetElementChildVisual(content.Children[0], null);
                }
                else if (args.Phase == 0 && content.Children[0] is LottieView lottie)
                {
                    lottie.Source = UriEx.ToLocal(file.Local.Path);
                }
                else if (args.Phase == 0 && content.Children[0] is AnimationView animation)
                {
                    animation.Source = new LocalVideoSource(file);
                }
            }
            else
            {
                if (content.Children[0] is Image photo)
                {
                    photo.Source = null;
                }
                else if (args.Phase == 0 && content.Children[0] is LottieView lottie)
                {
                    lottie.Source = null;
                }
                else if (args.Phase == 0 && content.Children[0] is AnimationView animation)
                {
                    animation.Source = null;
                }

                CompositionPathParser.ParseThumbnail(cover, out ShapeVisual visual, false);
                ElementCompositionPreview.SetElementChildVisual(content.Children[0], visual);

                UpdateManager.Subscribe(content, ViewModel.ProtoService, file, UpdateFile, true);

                if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive)
                {
                    ViewModel.ProtoService.DownloadFile(file.Id, 1);
                }
            }

            args.Handled = true;
        }

        #endregion

        #region Binding

        private bool IsType(StickersType x, StickersType y)
        {
            return x == y;
        }

        #endregion

        #region Handle

        private async void UpdateFile(object target, File file)
        {
            var content = target as Grid;
            if (content == null)
            {
                return;
            }

            if (content.Children[0] is Image photo)
            {
                photo.Source = await PlaceholderHelper.GetWebPFrameAsync(file.Local.Path, 48);
                ElementCompositionPreview.SetElementChildVisual(content.Children[0], null);
            }
            else if (content.Children[0] is LottieView lottie)
            {
                lottie.Source = UriEx.ToLocal(file.Local.Path);
                _handler.ThrottleVisibleItems();
            }
            else if (content.Children[0] is AnimationView animation)
            {
                animation.Source = new LocalVideoSource(file);
                _handler.ThrottleVisibleItems();
            }
        }

        #endregion

        #region Context menu

        private void StickerSet_ContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
            if (ViewModel.Type is not StickersType.Installed and not StickersType.Masks)
            {
                return;
            }

            var flyout = new MenuFlyout();

            var element = sender as FrameworkElement;
            var stickerSet = List.ItemFromContainer(element) as StickerSetInfo;

            if (stickerSet == null || stickerSet.Id == 0)
            {
                return;
            }

            if (stickerSet.IsOfficial)
            {
                flyout.CreateFlyoutItem(ViewModel.StickerSetHideCommand, stickerSet, Strings.Resources.StickersHide, new FontIcon { Glyph = Icons.Archive });
            }
            else
            {
                flyout.CreateFlyoutItem(ViewModel.StickerSetHideCommand, stickerSet, Strings.Resources.StickersHide, new FontIcon { Glyph = Icons.Archive });
                flyout.CreateFlyoutItem(ViewModel.StickerSetRemoveCommand, stickerSet, Strings.Resources.StickersRemove, new FontIcon { Glyph = Icons.Delete });
                //CreateFlyoutItem(ref flyout, ViewModel.StickerSetShareCommand, stickerSet, Strings.Resources.StickersShare);
                //CreateFlyoutItem(ref flyout, ViewModel.StickerSetCopyCommand, stickerSet, Strings.Resources.StickersCopy);
            }

            args.ShowAt(flyout, element);
        }

        #endregion
    }
}
