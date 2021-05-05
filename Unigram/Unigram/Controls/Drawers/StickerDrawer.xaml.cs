using LinqToVisualTree;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Services;
using Unigram.ViewModels.Delegates;
using Unigram.ViewModels.Drawers;
using Unigram.Views;
using Windows.Foundation;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;
using StickerDrawerViewModel = Unigram.ViewModels.Drawers.StickerDrawerViewModel;
using StickerSetViewModel = Unigram.ViewModels.Drawers.StickerSetViewModel;
using StickerViewModel = Unigram.ViewModels.Drawers.StickerViewModel;

namespace Unigram.Controls.Drawers
{
    public sealed partial class StickerDrawer : UserControl, IDrawer, IFileDelegate
    {
        public StickerDrawerViewModel ViewModel => DataContext as StickerDrawerViewModel;

        public Action<Sticker> ItemClick { get; set; }
        public event TypedEventHandler<UIElement, ItemContextRequestedEventArgs<Sticker>> ItemContextRequested;

        private readonly AnimatedListHandler<StickerViewModel> _handler;
        private readonly ZoomableListHandler _zoomer;

        private readonly AnimatedListHandler<StickerSetViewModel> _toolbarHandler;

        private readonly FileContext<StickerViewModel> _stickers = new FileContext<StickerViewModel>();
        private readonly FileContext<StickerSetViewModel> _stickerSets = new FileContext<StickerSetViewModel>();

        private readonly Dictionary<string, DataTemplate> _typeToTemplateMapping = new Dictionary<string, DataTemplate>();
        private readonly Dictionary<string, HashSet<SelectorItem>> _typeToItemHashSetMapping = new Dictionary<string, HashSet<SelectorItem>>();

        private bool _isActive;

        public StickerDrawer()
        {
            InitializeComponent();

            ElementCompositionPreview.GetElementVisual(this).Clip = Window.Current.Compositor.CreateInsetClip();

            _handler = new AnimatedListHandler<StickerViewModel>(List);

            _zoomer = new ZoomableListHandler(List);
            _zoomer.Opening = _handler.UnloadVisibleItems;
            _zoomer.Closing = _handler.ThrottleVisibleItems;
            _zoomer.DownloadFile = fileId => ViewModel.ProtoService.DownloadFile(fileId, 32);

            _typeToItemHashSetMapping.Add("AnimatedItemTemplate", new HashSet<SelectorItem>());
            _typeToItemHashSetMapping.Add("ItemTemplate", new HashSet<SelectorItem>());

            _typeToTemplateMapping.Add("AnimatedItemTemplate", Resources["AnimatedItemTemplate"] as DataTemplate);
            _typeToTemplateMapping.Add("ItemTemplate", Resources["ItemTemplate"] as DataTemplate);

            //_toolbarHandler = new AnimatedStickerHandler<StickerSetViewModel>(Toolbar);

            DropShadowEx.Attach(Separator);

            var debouncer = new EventDebouncer<TextChangedEventArgs>(Constants.TypingTimeout, handler => FieldStickers.TextChanged += new TextChangedEventHandler(handler));
            debouncer.Invoked += async (s, args) =>
            {
                var items = ViewModel.SearchStickers;
                if (items != null && string.Equals(FieldStickers.Text, items.Query))
                {
                    await items.LoadMoreItemsAsync(1);
                    await items.LoadMoreItemsAsync(2);
                }
            };
        }

        public Services.Settings.StickersTab Tab => Services.Settings.StickersTab.Stickers;

        public void Activate()
        {
            _isActive = true;
            _handler.ThrottleVisibleItems();
        }

        public void Deactivate()
        {
            _isActive = false;
            _handler.UnloadVisibleItems();
        }

        public void LoadVisibleItems()
        {
            if (_isActive)
            {
                _handler.LoadVisibleItems(false);
            }
        }

        public void UnloadVisibleItems()
        {
            _handler.UnloadVisibleItems();
        }

        public void SetView(StickersPanelMode mode)
        {
        }

        public async void UpdateFile(File file)
        {
            if (_stickers.TryGetValue(file.Id, out List<StickerViewModel> items) && items.Count > 0)
            {
                foreach (var sticker in items.ToImmutableHashSet())
                {
                    sticker.UpdateFile(file);

                    var container = List.ContainerFromItem(sticker) as SelectorItem;
                    if (container == null)
                    {
                        continue;
                    }

                    var content = container.ContentTemplateRoot as Border;
                    if (content == null)
                    {
                        continue;
                    }

                    if (content.Child is Border border && border.Child is Image photo)
                    {
                        photo.Source = await PlaceholderHelper.GetWebPFrameAsync(file.Local.Path, 68);
                        ElementCompositionPreview.SetElementChildVisual(content.Child, null);
                    }
                    else if (content.Child is LottieView lottie)
                    {
                        lottie.Source = UriEx.ToLocal(file.Local.Path);
                        _handler.ThrottleVisibleItems();
                    }
                }
            }

            if (_stickerSets.TryGetValue(file.Id, out List<StickerSetViewModel> sets) && sets.Count > 0)
            {
                foreach (var item in sets.ToImmutableHashSet())
                {
                    var cover = item.Thumbnail ?? item.Covers.FirstOrDefault()?.Thumbnail;
                    if (cover == null)
                    {
                        continue;
                    }

                    cover.UpdateFile(file);

                    var container = Toolbar.ContainerFromItem(item) as SelectorItem;
                    if (container == null)
                    {
                        continue;
                    }

                    var content = container.ContentTemplateRoot as Grid;
                    var photo = content?.Children[0] as Image;

                    if (content == null)
                    {
                        continue;
                    }

                    if (item.IsAnimated)
                    {
                        photo.Source = PlaceholderHelper.GetLottieFrame(file.Local.Path, 0, 36, 36);
                    }
                    else
                    {
                        photo.Source = PlaceholderHelper.GetWebPFrame(file.Local.Path, 36);
                    }
                }
            }

            _zoomer.UpdateFile(file);
        }

        private void Stickers_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is StickerViewModel sticker && sticker.StickerValue != null)
            {
                ItemClick?.Invoke(sticker);
            }
        }

        private void Stickers_Loaded(object sender, RoutedEventArgs e)
        {
            var scrollingHost = List.Descendants<ScrollViewer>().FirstOrDefault();
            if (scrollingHost != null)
            {
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

        public async void Refresh()
        {
            // TODO: memes

            await Task.Delay(100);
            //Pivot_SelectionChanged(null, null);
        }

        private void ScrollingHost_ViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
        {
            var scrollingHost = List.ItemsPanelRoot as ItemsWrapGrid;
            if (scrollingHost != null && _isActive)
            {
                var first = List.ContainerFromIndex(scrollingHost.FirstVisibleIndex);
                if (first != null)
                {
                    var header = List.GroupHeaderContainerFromItemContainer(first) as GridViewHeaderItem;
                    if (header != null && header.Content != Toolbar.SelectedItem)
                    {
                        Toolbar.SelectedItem = header.Content;
                        Toolbar.ScrollIntoView(header.Content);
                    }
                }
            }
        }

        public bool ToggleActiveView()
        {
            //if (Pivot.SelectedIndex == 2 && !SemanticStickers.IsZoomedInViewActive && SemanticStickers.CanChangeViews)
            //{
            //    SemanticStickers.ToggleActiveView();
            //    return true;
            //}

            return false;
        }

        private void GroupStickers_Click(object sender, RoutedEventArgs e)
        {
            //ViewModel.GroupStickersCommand.Execute(null);
        }

        private void Settings_Click(object sender, RoutedEventArgs e)
        {
            //ViewModel.NavigationService.Navigate(typeof(SettingsStickersPage));
        }

        private void Install_Click(object sender, RoutedEventArgs e)
        {
            //ViewModel.InstallCommand.Execute(((Button)sender).DataContext);
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

                var response = await ViewModel.ProtoService.SendAsync(new GetStickerSet(group.Id));
                if (response is StickerSet full)
                {
                    group.Update(full, false);

                    //return;

                    foreach (var sticker in group.Stickers)
                    {
                        var container = List.ContainerFromItem(sticker) as SelectorItem;
                        if (container == null)
                        {
                            continue;
                        }

                        UpdateContainerContent(sticker, container.ContentTemplateRoot as Border);
                    }
                }
            }
        }

        private void OnChoosingItemContainer(ListViewBase sender, ChoosingItemContainerEventArgs args)
        {
            var typeName = args.Item is StickerViewModel sticker && sticker.IsAnimated ? "AnimatedItemTemplate" : "ItemTemplate";
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
                    var item = new GridViewItem();
                    item.ContentTemplate = _typeToTemplateMapping[typeName];
                    item.Style = sender.ItemContainerStyle;
                    item.Tag = typeName;
                    item.ContextRequested += OnContextRequested;
                    args.ItemContainer = item;

                    _zoomer.ElementPrepared(args.ItemContainer);
                }
            }

            // Indicate to XAML that we picked a container for it
            args.IsContainerPrepared = true;

        }

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            var content = args.ItemContainer.ContentTemplateRoot as Border;
            var sticker = args.Item as StickerViewModel;

            if (args.InRecycleQueue)
            {
                if (content.Child is Border border && border.Child is Image photo)
                {
                    photo.Source = null;
                }
                else if (content.Child is LottieView lottie)
                {
                    lottie.Source = null;
                }

                return;
            }

            UpdateContainerContent(sticker, content);
            args.Handled = true;
        }

        private async void UpdateContainerContent(StickerViewModel sticker, Border content)
        {
            var file = sticker.StickerValue;
            if (file == null)
            {
                return;
            }

            if (file.Local.IsDownloadingCompleted)
            {
                if (content.Child is Border border && border.Child is Image photo)
                {
                    photo.Source = await PlaceholderHelper.GetWebPFrameAsync(file.Local.Path, 68);
                    ElementCompositionPreview.SetElementChildVisual(content.Child, null);
                }
                else if (content.Child is LottieView lottie)
                {
                    lottie.Source = UriEx.ToLocal(file.Local.Path);
                }
            }
            else if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive /*&& args.Phase == 0*/)
            {
                if (content.Child is Image photo)
                {
                    photo.Source = null;
                }
                else if (content.Child is LottieView lottie)
                {
                    lottie.Source = null;
                }

                if (ApiInfo.CanUseDirectComposition)
                {
                    CompositionPathParser.ParseThumbnail(sticker.Outline, out ShapeVisual visual, false);
                    ElementCompositionPreview.SetElementChildVisual(content.Child, visual);
                }

                DownloadFile(_stickers, file.Id, sticker);
            }
        }

        private void Toolbar_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }

            if (args.Item is SupergroupStickerSetViewModel supergroup)
            {
                Automation.SetToolTip(args.ItemContainer, supergroup.Title);

                var chat = ViewModel.CacheService.GetChat(supergroup.ChatId);
                if (chat == null)
                {
                    return;
                }

                var content = args.ItemContainer.ContentTemplateRoot as ProfilePicture;
                if (content == null)
                {
                    return;
                }

                content.Source = PlaceholderHelper.GetChat(ViewModel.ProtoService, chat, 36);
            }
            else if (args.Item is StickerSetViewModel sticker)
            {
                Automation.SetToolTip(args.ItemContainer, sticker.Title);

                var content = args.ItemContainer.ContentTemplateRoot as Grid;
                var photo = content?.Children[0] as Image;

                if (content == null || sticker == null || (sticker.Thumbnail == null && sticker.Covers == null))
                {
                    return;
                }

                var cover = sticker.Thumbnail ?? sticker.Covers.FirstOrDefault()?.Thumbnail;
                if (cover == null)
                {
                    photo.Source = null;
                    return;
                }

                var file = cover.File;
                if (file.Local.IsDownloadingCompleted)
                {
                    if (sticker.IsAnimated)
                    {
                        photo.Source = PlaceholderHelper.GetLottieFrame(file.Local.Path, 0, 36, 36);
                    }
                    else
                    {
                        photo.Source = PlaceholderHelper.GetWebPFrame(file.Local.Path, 36);
                    }
                }
                else if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive)
                {
                    photo.Source = null;
                    DownloadFile(_stickerSets, file.Id, sticker);
                }
            }
        }

        private void DownloadFile<T>(FileContext<T> context, int id, T sticker)
        {
            context[id].Add(sticker);
            ViewModel.ProtoService.DownloadFile(id, 1);
        }

        private void FieldStickers_TextChanged(object sender, TextChangedEventArgs e)
        {
            ViewModel.FindStickers(FieldStickers.Text);
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
    }
}
