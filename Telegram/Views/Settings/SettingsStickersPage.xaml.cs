//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Collections.Generic;
using System.Linq;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Converters;
using Telegram.Td.Api;
using Telegram.ViewModels.Settings;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;

namespace Telegram.Views.Settings
{
    public sealed partial class SettingsStickersPage : HostedPage
    {
        public SettingsStickersViewModel ViewModel => DataContext as SettingsStickersViewModel;

        private readonly Dictionary<string, DataTemplate> _typeToTemplateMapping = new Dictionary<string, DataTemplate>();
        private readonly Dictionary<string, HashSet<SelectorItem>> _typeToItemHashSetMapping = new Dictionary<string, HashSet<SelectorItem>>();

        private readonly AnimatedListHandler _handler;

        public SettingsStickersPage()
        {
            InitializeComponent();

            // TODO: this might need to change depending on context
            _handler = new AnimatedListHandler(ScrollingHost, AnimatedListType.Stickers);

            _typeToItemHashSetMapping.Add("AnimatedItemTemplate", new HashSet<SelectorItem>());
            _typeToItemHashSetMapping.Add("VideoItemTemplate", new HashSet<SelectorItem>());
            _typeToItemHashSetMapping.Add("ItemTemplate", new HashSet<SelectorItem>());

            _typeToTemplateMapping.Add("AnimatedItemTemplate", Resources["AnimatedItemTemplate"] as DataTemplate);
            _typeToTemplateMapping.Add("VideoItemTemplate", Resources["VideoItemTemplate"] as DataTemplate);
            _typeToTemplateMapping.Add("ItemTemplate", Resources["ItemTemplate"] as DataTemplate);
        }

        private void ListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            ViewModel.Open(e.ClickedItem as StickerSetInfo);
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

            var typeName = cover?.Format switch
            {
                StickerFormatTgs => "AnimatedItemTemplate",
                StickerFormatWebm => "VideoItemTemplate",
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
                    var item = new TableListViewItem();
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
            subtitle.Text = Locale.Declension(Strings.R.Stickers, stickerSet.Size);

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
                    photo.Source = await PlaceholderHelper.GetWebPFrameAsync(file.Local.Path, 36);
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

                UpdateManager.Subscribe(content, ViewModel.ClientService, file, UpdateFile, true);

                if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive)
                {
                    ViewModel.ClientService.DownloadFile(file.Id, 1);
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
            if (ViewModel.Type is not StickersType.Installed and not StickersType.Masks and not StickersType.Emoji)
            {
                return;
            }

            var flyout = new MenuFlyout();

            var element = sender as FrameworkElement;
            var stickerSet = ScrollingHost.ItemFromContainer(element) as StickerSetInfo;

            if (stickerSet == null || stickerSet.Id == 0)
            {
                return;
            }

            if (stickerSet.IsOfficial)
            {
                flyout.CreateFlyoutItem(ViewModel.Archive, stickerSet, Strings.StickersHide, Icons.Archive);
            }
            else
            {
                flyout.CreateFlyoutItem(ViewModel.Archive, stickerSet, Strings.StickersHide, Icons.Archive);
                flyout.CreateFlyoutItem(ViewModel.Remove, stickerSet, Strings.StickersRemove, Icons.Delete);
                //CreateFlyoutItem(ref flyout, ViewModel.StickerSetShareCommand, stickerSet, Strings.StickersShare);
                //CreateFlyoutItem(ref flyout, ViewModel.StickerSetCopyCommand, stickerSet, Strings.StickersCopy);
            }

            args.ShowAt(flyout, element);
        }

        #endregion
    }
}
