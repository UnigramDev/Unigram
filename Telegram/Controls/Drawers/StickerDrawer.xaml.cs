//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using LinqToVisualTree;
using System;
using System.Collections.Generic;
using System.Linq;
using Telegram.Common;
using Telegram.Streams;
using Telegram.Td.Api;
using Telegram.ViewModels.Drawers;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;

namespace Telegram.Controls.Drawers
{
    public sealed partial class StickerDrawer : UserControl, IDrawer
    {
        public StickerDrawerViewModel ViewModel => DataContext as StickerDrawerViewModel;

        public Action<Sticker, bool> ItemClick { get; set; }
        public event TypedEventHandler<UIElement, ItemContextRequestedEventArgs<Sticker>> ItemContextRequested;
        public event EventHandler ChoosingItem;
        public event EventHandler SettingsClick;

        private readonly AnimatedListHandler _handler;
        private readonly ZoomableListHandler _zoomer;

        private readonly AnimatedListHandler _toolbarHandler;

        private readonly Dictionary<StickerViewModel, Grid> _itemIdToContent = new();
        private long _selectedSetId;

        private bool _isActive;

        public StickerDrawer()
        {
            InitializeComponent();

            ElementCompositionPreview.GetElementVisual(this).Clip = Window.Current.Compositor.CreateInsetClip();

            _handler = new AnimatedListHandler(List, AnimatedListType.Stickers);
            _toolbarHandler = new AnimatedListHandler(Toolbar, AnimatedListType.Stickers);

            _zoomer = new ZoomableListHandler(List);
            _zoomer.Opening = _handler.UnloadVisibleItems;
            _zoomer.Closing = _handler.ThrottleVisibleItems;
            _zoomer.DownloadFile = fileId => ViewModel.ClientService.DownloadFile(fileId, 32);
            _zoomer.SessionId = () => ViewModel.ClientService.SessionId;

            var header = DropShadowEx.Attach(Separator);
            header.Clip = header.Compositor.CreateInsetClip(0, 40, 0, -40);

            //var debouncer = new EventDebouncer<TextChangedEventArgs>(Constants.TypingTimeout, handler => FieldStickers.TextChanged += new TextChangedEventHandler(handler));
            //debouncer.Invoked += async (s, args) =>
            //{
            //    var items = ViewModel.SearchStickers;
            //    if (items != null && string.Equals(FieldStickers.Text, items.Query))
            //    {
            //        await items.LoadMoreItemsAsync(1);
            //        await items.LoadMoreItemsAsync(2);
            //    }
            //};
        }

        public Services.Settings.StickersTab Tab => Services.Settings.StickersTab.Stickers;

        public Thickness ScrollingHostPadding
        {
            get => List.Padding;
            set => List.Padding = new Thickness(2, value.Top, 2, value.Bottom);
        }

        public ListViewBase ScrollingHost => List;

        public void Activate(Chat chat, EmojiSearchType type = EmojiSearchType.Default)
        {
            _isActive = true;
            _handler.ThrottleVisibleItems();
            _toolbarHandler.ThrottleVisibleItems();

            SearchField.SetType(ViewModel.ClientService, type);
            ViewModel.Update(chat);
        }

        public void Deactivate()
        {
            _itemIdToContent.Clear();

            _isActive = false;
            _handler.UnloadItems();
            _toolbarHandler.UnloadItems();

            // This is called only right before XamlMarkupHelper.UnloadObject
            // so we can safely clean up any kind of anything from here.
            _zoomer.Release();
            Bindings.StopTracking();
        }

        public void LoadVisibleItems()
        {
            if (_isActive)
            {
                _handler.LoadVisibleItems();
                _toolbarHandler.LoadVisibleItems();
            }
        }

        public void UnloadVisibleItems()
        {
            _handler.UnloadVisibleItems();
            _toolbarHandler.UnloadVisibleItems();
        }

        private void Stickers_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is StickerViewModel sticker && sticker.StickerValue != null)
            {
                var container = List.ContainerFromItem(e.ClickedItem);

                var groupContainer = List.GroupHeaderContainerFromItemContainer(container) as GridViewHeaderItem;
                if (groupContainer == null)
                {
                    ItemClick?.Invoke(sticker, false);
                    return;
                }

                var stickerSet = groupContainer.Content as StickerSetViewModel;
                if (stickerSet != null)
                {
                    ItemClick?.Invoke(sticker, stickerSet.Id != 0);
                }
            }
        }

        private void Stickers_Loaded(object sender, RoutedEventArgs e)
        {
            var scrollingHost = List.Descendants<ScrollViewer>().FirstOrDefault();
            if (scrollingHost != null)
            {
                scrollingHost.VerticalSnapPointsType = SnapPointsType.None;

                // Syncronizes GridView with the toolbar ListView
                scrollingHost.ViewChanged += ScrollingHost_ViewChanged;
                ScrollingHost_ViewChanged(null, null);
            }
        }

        private void Toolbar_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is StickerSetViewModel set && set.Stickers != null)
            {
                List.ScrollIntoView(e.ClickedItem, ScrollIntoViewAlignment.Leading);
            }
        }

        private void ScrollingHost_ViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
        {
            var scrollingHost = List.ItemsPanelRoot as ItemsWrapGrid;
            if (scrollingHost != null && _isActive && scrollingHost.FirstVisibleIndex >= 0)
            {
                //var item = List.Items[scrollingHost.FirstVisibleIndex];
                //if (item is StickerViewModel sticker && sticker.SetId != _selectedSetId)
                //{
                //    _selectedSetId = sticker.SetId;

                //    if (ViewModel != null && ViewModel.TryGetInstalledSet(sticker.SetId, out var stickerSet))
                //    {
                //        Toolbar.SelectedItem = stickerSet;
                //        Toolbar.ScrollIntoView(stickerSet);
                //    }
                //}

                var first = List.ContainerFromIndex(scrollingHost.FirstVisibleIndex);
                if (first != null)
                {
                    var header = List.GroupHeaderContainerFromItemContainer(first) as GridViewHeaderItem;
                    if (header != null && header.Content != Toolbar.SelectedItem)
                    {
                        Toolbar.SelectedItem = header.Content;
                    }
                }
            }

            if (sender is ScrollViewer scrollViewer && scrollViewer.VerticalOffset > 0 && e.IsIntermediate)
            {
                ChoosingItem?.Invoke(this, EventArgs.Empty);
            }
        }

        private void Settings_Click(object sender, RoutedEventArgs e)
        {
            SettingsClick?.Invoke(this, EventArgs.Empty);
        }

        private async void OnChoosingGroupHeaderContainer(ListViewBase sender, ChoosingGroupHeaderContainerEventArgs args)
        {
            if (args.GroupHeaderContainer == null)
            {
                args.GroupHeaderContainer = new GridViewHeaderItem();
                args.GroupHeaderContainer.Style = List.GroupStyle[0].HeaderContainerStyle;
                args.GroupHeaderContainer.ContentTemplate = List.GroupStyle[0].HeaderTemplate;
            }

            if (args.Group is StickerSetViewModel group && !group.IsLoaded)
            {
                group.IsLoaded = true;

                //Debug.WriteLine("Loading sticker set " + group.Id);

                var response = await ViewModel.ClientService.SendAsync(new GetStickerSet(group.Id));
                if (response is StickerSet full)
                {
                    group.Update(full, false);

                    //return;

                    foreach (var sticker in group.Stickers)
                    {
                        if (_itemIdToContent.TryGetValue(sticker, out Grid content))
                        {
                            var animation = content.Children[0] as AnimatedImage;
                            animation.Source = new DelayedFileSource(ViewModel.ClientService, sticker);
                        }
                    }
                }
            }
        }

        private void OnChoosingItemContainer(ListViewBase sender, ChoosingItemContainerEventArgs args)
        {
            if (args.ItemContainer == null)
            {
                var item = new GridViewItem();
                item.ContentTemplate = sender.ItemTemplate;
                item.Style = sender.ItemContainerStyle;
                item.ContextRequested += OnContextRequested;
                args.ItemContainer = item;

                _zoomer.ElementPrepared(args.ItemContainer);
            }

            args.IsContainerPrepared = true;
        }

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            var content = args.ItemContainer.ContentTemplateRoot as Grid;
            var sticker = args.Item as StickerViewModel;

            if (args.InRecycleQueue || sticker == null)
            {
                if (sticker != null)
                {
                    _itemIdToContent.Remove(sticker);
                }

                return;
            }

            _itemIdToContent[sticker] = content;

            if (sticker?.StickerValue != null)
            {
                var animation = content.Children[0] as AnimatedImage;
                animation.Source = new DelayedFileSource(ViewModel.ClientService, sticker);
            }
            else
            {
                var animation = content.Children[0] as AnimatedImage;
                animation.Source = null;
            }

            args.Handled = true;
        }

        private void Toolbar_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }
            else if (args.Item is SupergroupStickerSetViewModel supergroup)
            {
                Automation.SetToolTip(args.ItemContainer, supergroup.Title);

                var chat = ViewModel.ClientService.GetChat(supergroup.ChatId);
                if (chat == null)
                {
                    return;
                }

                var content = args.ItemContainer.ContentTemplateRoot as Border;
                if (content?.Child is not ProfilePicture photo)
                {
                    return;
                }

                photo.SetChat(ViewModel.ClientService, chat, 24);
                args.Handled = true;
            }
            else if (args.Item is StickerSetViewModel sticker)
            {
                Automation.SetToolTip(args.ItemContainer, sticker.Title);

                var content = args.ItemContainer.ContentTemplateRoot as Grid;

                if (content == null || sticker == null || (sticker.Thumbnail == null && sticker.Covers == null))
                {
                    return;
                }

                var cover = sticker.GetThumbnail();
                if (cover != null)
                {
                    var animation = content.Children[0] as AnimatedImage;
                    animation.Source = new DelayedFileSource(ViewModel.ClientService, cover);
                }
                else
                {
                    var animation = content.Children[0] as AnimatedImage;
                    animation.Source = null;
                }

                args.Handled = true;
            }
        }

        private void SearchField_TextChanged(object sender, TextChangedEventArgs e)
        {
            ViewModel.Search(SearchField.Text, false);
        }

        private void SearchField_CategorySelected(object sender, EmojiCategorySelectedEventArgs e)
        {
            ViewModel.Search(string.Join(" ", e.Category.Emojis), true);
        }

        private void OnContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
            var sticker = List.ItemFromContainer(sender) as StickerViewModel;
            if (sticker == null)
            {
                return;
            }

            ItemContextRequested?.Invoke(sender, new ItemContextRequestedEventArgs<Sticker>(sticker, args));
        }

        private void Player_Ready(object sender, EventArgs e)
        {
            _handler.ThrottleVisibleItems();
        }

        private void Toolbar_Ready(object sender, EventArgs e)
        {
            _toolbarHandler.ThrottleVisibleItems();
        }

        private void Toolbar_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (Toolbar.SelectedItem != null)
            {
                _ = Toolbar.ScrollToItem2(Toolbar.SelectedItem, VerticalAlignment.Center);
            }
        }
    }
}
