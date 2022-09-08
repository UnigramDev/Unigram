using LinqToVisualTree;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Services;
using Unigram.Services.Settings;
using Unigram.ViewModels.Drawers;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media;
using StickerSetViewModel = Unigram.ViewModels.Drawers.StickerSetViewModel;

namespace Unigram.Controls.Drawers
{
    public sealed partial class EmojiDrawer : UserControl, IDrawer
    {
        public EmojiDrawerViewModel ViewModel => DataContext as EmojiDrawerViewModel;

        public event ItemClickEventHandler ItemClick;

        private bool _needUpdate;

        private EmojiDrawerMode _mode;

        private EmojiSkinTone _selected;
        private bool _expanded;

        private bool _isActive;

        private readonly AnimatedListHandler _handler;
        private readonly AnimatedListHandler _toolbarHandler;

        public EmojiDrawer()
            : this(EmojiDrawerMode.Chat)
        {

        }

        public EmojiDrawer(EmojiDrawerMode mode)
        {
            InitializeComponent();

            ElementCompositionPreview.GetElementVisual(this).Clip = Window.Current.Compositor.CreateInsetClip();

            var header = DropShadowEx.Attach(Separator);
            header.Clip = header.Compositor.CreateInsetClip(0, 36, 0, -36);

            _handler = new AnimatedListHandler(List);
            _toolbarHandler = new AnimatedListHandler(Toolbar2);

            _typeToItemHashSetMapping["EmojiSkinTemplate"] = new HashSet<SelectorItem>();
            _typeToItemHashSetMapping["EmojiTemplate"] = new HashSet<SelectorItem>();
            _typeToItemHashSetMapping.Add("AnimatedItemTemplate", new HashSet<SelectorItem>());
            _typeToItemHashSetMapping.Add("VideoItemTemplate", new HashSet<SelectorItem>());
            _typeToItemHashSetMapping.Add("ItemTemplate", new HashSet<SelectorItem>());
            _typeToItemHashSetMapping.Add("MoreTemplate", new HashSet<SelectorItem>());

            _mode = mode;

            if (mode == EmojiDrawerMode.Reactions)
            {
                FieldEmoji.Visibility = Visibility.Collapsed;
                Toolbar3.Visibility = Visibility.Collapsed;
                Toolbar2.Header = null;

                List.Padding = new Thickness(8, 0, 0, 0);
                List.ItemContainerStyle.Setters.Add(new Setter(MarginProperty, new Thickness(0, 0, 4, 4)));
                List.GroupStyle[0].HeaderContainerStyle.Setters.Add(new Setter(PaddingProperty, new Thickness(0, 0, 0, 6)));

                FluidGridView.GetTriggers(List).Clear();
                FluidGridView.GetTriggers(List).Add(new FixedGridViewTrigger { ItemLength = 28 });
            }
            else
            {
                UpdateView();
            }
        }

        public StickersTab Tab => StickersTab.Emoji;

        public void Activate(Chat chat)
        {
            _isActive = true;
            _handler.ThrottleVisibleItems();
            _toolbarHandler.ThrottleVisibleItems();

            if (chat != null)
            {
                ViewModel.Update();
            }
        }

        public void Deactivate()
        {
            _isActive = false;
            _handler.UnloadItems();
            _toolbarHandler.UnloadItems();

            // This is called only right before XamlMarkupHelper.UnloadObject
            // so we can safely clean up any kind of anything from here.
            Bindings.StopTracking();
        }

        public void LoadVisibleItems()
        {
            if (_isActive)
            {
                _handler.LoadVisibleItems(false);
                _toolbarHandler.LoadVisibleItems(false);
            }
        }

        public void UnloadVisibleItems()
        {
            _handler.UnloadVisibleItems();
            _toolbarHandler.UnloadVisibleItems();
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            var scrollingHost = List.Descendants<ScrollViewer>().FirstOrDefault();
            if (scrollingHost != null)
            {
                scrollingHost.VerticalSnapPointsType = SnapPointsType.None;

                // Syncronizes GridView with the toolbar ListView
                scrollingHost.ViewChanged += ScrollingHost_ViewChanged;
                ScrollingHost_ViewChanged(null, null);
            }

            UpdateToolbar(true);
        }

        public void UpdateView()
        {
            if (_mode == EmojiDrawerMode.Reactions)
            {
                return;
            }

            var microsoft = string.Equals(SettingsService.Current.Appearance.EmojiSet.Id, "microsoft");
            var tone = SettingsService.Current.Stickers.SkinTone;

            if (Toolbar.ItemsSource is List<EmojiGroup> groups)
            {
                if (groups.Count == Emoji.GroupsCount && microsoft)
                {
                    _needUpdate = true;
                }
                else if (groups.Count == Emoji.GroupsCount - 1 && !microsoft)
                {
                    _needUpdate = true;
                }
            }
            else
            {
                _needUpdate = true;
            }

            if (_needUpdate)
            {
                //var items = Emoji.Get(tone, !microsoft);
                //EmojiCollection.Source = items;
                //Toolbar.ItemsSource = items;
            }

            _needUpdate = false;
            UpdateSkinTone(tone, false, false);
        }

        private void ScrollingHost_ViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
        {
            var scrollingHost = List.ItemsPanelRoot as ItemsWrapGrid;
            if (scrollingHost != null)
            {
                var first = List.ContainerFromIndex(scrollingHost.FirstVisibleIndex);
                if (first != null)
                {
                    var header = List.GroupHeaderContainerFromItemContainer(first) as GridViewHeaderItem;
                    if (header != null && header != Toolbar.SelectedItem)
                    {
                        if (header.Content is EmojiGroup)
                        {
                            Toolbar2.SelectedItem = null;
                            Toolbar.SelectedItem = header.Content;
                            Toolbar.ScrollIntoView(header.Content);
                        }
                        else
                        {
                            Toolbar.SelectedItem = null;
                            Toolbar2.SelectedItem = header.Content;
                            Toolbar2.ScrollIntoView(header.Content);
                        }

                        UpdateToolbar();
                    }
                }
            }
        }

        private void Toolbar_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            //Pivot.SelectedIndex = Toolbar.SelectedIndex;
        }

        private void Toolbar_ItemClick(object sender, ItemClickEventArgs e)
        {
            List.ScrollIntoView(e.ClickedItem, ScrollIntoViewAlignment.Leading);
        }

        private async void ListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            ItemClick?.Invoke(this, e);

            if (e.ClickedItem is EmojiData data)
            {
                _needUpdate = true;

                SettingsService.Current.Emoji.AddRecentEmoji(data.Value);
                SettingsService.Current.Emoji.SortRecentEmoji();
                SettingsService.Current.Emoji.SaveRecentEmoji();
            }
            else if (e.ClickedItem is StickerViewModel sticker)
            {
                if (sticker is MoreStickerViewModel)
                {
                    var groupContainer = List.GroupHeaderContainerFromItemContainer(List.ContainerFromItem(sticker)) as GridViewHeaderItem;
                    if (groupContainer.Content is StickerSetViewModel group)
                    {
                        var response = await ViewModel.ProtoService.SendAsync(new GetStickerSet(group.Id));
                        if (response is StickerSet full)
                        {
                            group.Update(full, false);

                            //return;

                            foreach (var item in group.Stickers)
                            {
                                var container = List?.ContainerFromItem(item) as SelectorItem;
                                if (container == null)
                                {
                                    continue;
                                }

                                UpdateContainerContent(sticker, container.ContentTemplateRoot as Grid, UpdateSticker);
                            }
                        }
                    }
                }
                else
                {
                    SettingsService.Current.Emoji.AddRecentEmoji($"{sticker.Emoji};{sticker.CustomEmojiId}");
                    SettingsService.Current.Emoji.SortRecentEmoji();
                    SettingsService.Current.Emoji.SaveRecentEmoji();
                }
            }
        }

        private async void FieldEmoji_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(FieldEmoji.Text))
            {
                EmojiCollection.Source = Toolbar.ItemsSource;
            }
            else
            {
                EmojiCollection.Source = await Emoji.SearchAsync(ViewModel.ProtoService, FieldEmoji.Text, _selected);
            }
        }

        private void SkinTone_Click(object sender, RoutedEventArgs e)
        {
            if (!_expanded)
            {
                UpdateSkinTone(SettingsService.Current.Stickers.SkinTone, true, true);
                return;
            }

            var radio = sender as RadioButton;
            if (radio.Content is int value && ViewModel.Items.Count > 0)
            {
                if (ViewModel.Items[0] is RecentEmoji recent)
                {
                    foreach (var item in recent.Stickers.OfType<EmojiSkinData>())
                    {
                        item.SetValue((EmojiSkinTone)value);
                    }
                }

                foreach (var group in ViewModel.StandardSets)
                {
                    foreach (var item in group.Stickers.OfType<EmojiSkinData>())
                    {
                        item.SetValue((EmojiSkinTone)value);
                    }
                }

                SettingsService.Current.Stickers.SkinTone = (EmojiSkinTone)value;
                UpdateSkinTone((EmojiSkinTone)value, false, true);
            }
        }

        private bool _emojiCollapsed = false;

        private void UpdateToolbar(bool collapse = false)
        {
            if (_mode == EmojiDrawerMode.Reactions)
            {
                return;
            }

            if (Toolbar.SelectedItem == null != _emojiCollapsed || collapse)
            {
                _emojiCollapsed = Toolbar.SelectedItem == null;

                var show = !_emojiCollapsed;

                var toolbar = ElementCompositionPreview.GetElementVisual(Toolbar);
                var pill = ElementCompositionPreview.GetElementVisual(ToolbarPill);
                var panel = ElementCompositionPreview.GetElementVisual(Toolbar2.ItemsPanelRoot);

                ElementCompositionPreview.SetIsTranslationEnabled(Toolbar2.ItemsPanelRoot, true);

                var clip = toolbar.Compositor.CreateInsetClip();
                var offset = 144 - 32;

                var ellipse = toolbar.Compositor.CreateRoundedRectangleGeometry();
                ellipse.CornerRadius = new Vector2(4);

                pill.Clip = toolbar.Compositor.CreateGeometricClip(ellipse);
                toolbar.Clip = clip;
                Toolbar.Width = 144;

                var animClip = toolbar.Compositor.CreateScalarKeyFrameAnimation();
                animClip.InsertKeyFrame(show ? 1 : 0, 0);
                animClip.InsertKeyFrame(show ? 0 : 1, offset);

                var animOffset = toolbar.Compositor.CreateScalarKeyFrameAnimation();
                animOffset.InsertKeyFrame(show ? 0 : 1, -offset);
                animOffset.InsertKeyFrame(show ? 1 : 0, 0);

                var animSize = toolbar.Compositor.CreateVector2KeyFrameAnimation();
                animSize.InsertKeyFrame(show ? 0 : 1, new Vector2(32, 32));
                animSize.InsertKeyFrame(show ? 1 : 0, new Vector2(32 + offset, 32));

                var animOpacity = toolbar.Compositor.CreateScalarKeyFrameAnimation();
                animOpacity.InsertKeyFrame(show ? 0 : 1, 0);
                animOpacity.InsertKeyFrame(show ? 1 : 0, 1);

                var batch = toolbar.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
                batch.Completed += (s, args) =>
                {
                    panel.Properties.InsertVector3("Translation", Vector3.Zero);

                    toolbar.Clip = null;
                    Toolbar.Width = show ? 144 : 32;
                    Toolbar.ScrollIntoView(Toolbar2.SelectedItem == null && Toolbar.Items.Count > 0
                        ? Toolbar.Items[0]
                        : Toolbar.Items.LastOrDefault());
                };

                clip.StartAnimation("RightInset", animClip);
                panel.StartAnimation("Translation.X", animOffset);
                ellipse.StartAnimation("Size", animSize);
                pill.StartAnimation("Opacity", animOpacity);

                batch.End();
            }
        }

        private void UpdateSkinTone(EmojiSkinTone selected, bool expand, bool animated)
        {
            Canvas.SetZIndex(SkinDefault, (int)selected == 0 ? 6 : 5);
            Canvas.SetZIndex(SkinFitz12, (int)selected == 1 ? 6 : 4);
            Canvas.SetZIndex(SkinFitz3, (int)selected == 2 ? 6 : 3);
            Canvas.SetZIndex(SkinFitz4, (int)selected == 3 ? 6 : 2);
            Canvas.SetZIndex(SkinFitz5, (int)selected == 4 ? 6 : 1);
            Canvas.SetZIndex(SkinFitz6, (int)selected == 5 ? 6 : 0);

            Grid.SetColumn(SkinDefault, expand ? (int)selected < 0 ? 0 : (int)selected > 0 ? 1 : 0 : 0);
            Grid.SetColumn(SkinFitz12, expand ? (int)selected < 1 ? 1 : (int)selected > 1 ? 2 : 0 : 0);
            Grid.SetColumn(SkinFitz3, expand ? (int)selected < 2 ? 2 : (int)selected > 2 ? 3 : 0 : 0);
            Grid.SetColumn(SkinFitz4, expand ? (int)selected < 3 ? 3 : (int)selected > 3 ? 4 : 0 : 0);
            Grid.SetColumn(SkinFitz5, expand ? (int)selected < 4 ? 4 : (int)selected > 4 ? 5 : 0 : 0);
            Grid.SetColumn(SkinFitz6, expand ? (int)selected < 5 ? 5 : (int)selected > 5 ? 5 : 0 : 0);
            Grid.SetColumn(Toolbar, expand ? 6 : 1);
            Grid.SetColumn(ToolbarPill, expand ? 6 : 1);

            SkinDefault.IsEnabled = expand || selected == EmojiSkinTone.Default;
            SkinFitz12.IsEnabled = expand || selected == EmojiSkinTone.Fitz12;
            SkinFitz3.IsEnabled = expand || selected == EmojiSkinTone.Fitz3;
            SkinFitz4.IsEnabled = expand || selected == EmojiSkinTone.Fitz4;
            SkinFitz5.IsEnabled = expand || selected == EmojiSkinTone.Fitz5;
            SkinFitz6.IsEnabled = expand || selected == EmojiSkinTone.Fitz6;

            SkinDefault.IsChecked = selected == EmojiSkinTone.Default;
            SkinFitz12.IsChecked = selected == EmojiSkinTone.Fitz12;
            SkinFitz3.IsChecked = selected == EmojiSkinTone.Fitz3;
            SkinFitz4.IsChecked = selected == EmojiSkinTone.Fitz4;
            SkinFitz5.IsChecked = selected == EmojiSkinTone.Fitz5;
            SkinFitz6.IsChecked = selected == EmojiSkinTone.Fitz6;

            if (_expanded == expand || !animated)
            {
                _selected = selected;
                _expanded = expand;
                return;
            }

            var elements = new UIElement[] { SkinDefault, SkinFitz12, SkinFitz3, SkinFitz4, SkinFitz5, SkinFitz6, Toolbar };

            for (int i = 0; i < elements.Length; i++)
            {
                var visual = ElementCompositionPreview.GetElementVisual(VisualTreeHelper.GetChild(elements[i], 0) as UIElement);

                var from = i;
                if (elements[i] == Toolbar)
                {
                    from--;
                }
                else
                {
                    from = (int)_selected < i ? i : (int)_selected > i ? i + 1 : 0;
                }

                var anim = visual.Compositor.CreateScalarKeyFrameAnimation();
                anim.InsertKeyFrame(0, expand ? from * -40 : from * 40);
                anim.InsertKeyFrame(1, 0);

                visual.StartAnimation("Offset.X", anim);
            }

            _selected = selected;
            _expanded = expand;
        }

        #region Recycle

        private readonly Dictionary<string, HashSet<SelectorItem>> _typeToItemHashSetMapping = new Dictionary<string, HashSet<SelectorItem>>();

        private void OnChoosingItemContainer(ListViewBase sender, ChoosingItemContainerEventArgs args)
        {
            var typeName = args.Item is MoreStickerViewModel
                ? "MoreTemplate"
                : args.Item is StickerViewModel sticker
                    ? sticker.Format switch
                    {
                        StickerFormatTgs => "AnimatedItemTemplate",
                        StickerFormatWebm => "VideoItemTemplate",
                        _ => "ItemTemplate"
                    }
                    : args.Item is EmojiSkinData ? "EmojiSkinTemplate" : "EmojiTemplate";

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
                    // Not sure if this belongs here
                    var tag = args.ItemContainer.Tag as string;
                    var added = _typeToItemHashSetMapping[tag].Add(args.ItemContainer);

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
                    var item = new GridViewItem { ContentTemplate = Resources[typeName] as DataTemplate, Tag = typeName };
                    item.Style = List.ItemContainerStyle;
                    args.ItemContainer = item;
                }
            }

            // Indicate to XAML that we picked a container for it
            args.IsContainerPrepared = true;
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
                        var container = List?.ContainerFromItem(sticker) as SelectorItem;
                        if (container == null)
                        {
                            continue;
                        }

                        UpdateContainerContent(sticker, container.ContentTemplateRoot as Grid, UpdateSticker);
                    }
                }
            }
        }

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue == true)
            {
                // XAML has indicated that the item is no longer being shown, so add it to the recycle queue
                var tag = args.ItemContainer.Tag as string;
                var added = _typeToItemHashSetMapping[tag].Add(args.ItemContainer);
            }

            if (args.Item is StickerViewModel sticker)
            {
                var content = args.ItemContainer.ContentTemplateRoot as Grid;

                if (args.InRecycleQueue)
                {
                    ClearContainerContent(content);
                    return;
                }

                if (content.Children[0] is TextBlock textBlock && sticker is MoreStickerViewModel more)
                {
                    textBlock.Text = $"+{more.TotalCount}";
                }
                else
                {
                    UpdateContainerContent(sticker, content, UpdateSticker, args);
                }

                args.Handled = true;
            }
        }

        private Color _currentReplacement = Colors.Black;
        private readonly Dictionary<int, int> _colorReplacements = new()
        {
            { 0xFFFFFF, 0x000000 }
        };

        private IReadOnlyDictionary<int, int> GetColorReplacements(long setId)
        {
            if (setId == ViewModel.ProtoService.Options.ThemedEmojiStatusesStickerSetId)
            {
                if (_currentReplacement != Theme.Accent)
                {
                    _currentReplacement = Theme.Accent;
                    _colorReplacements[0xFFFFFF] = Theme.Accent.ToValue();
                }

                return _colorReplacements;
            }

            return null;
        }

        private async void UpdateContainerContent(Sticker sticker, Grid content, UpdateHandler<File> handler, ContainerContentChangingEventArgs args = null)
        {
            var file = sticker?.StickerValue;
            if (file == null)
            {
                return;
            }

            //if (toolbar)
            //{
            //    content.Padding = new Thickness(4);
            //}
            //else
            //{
            //    content.Padding = new Thickness(_mode == EmojiDrawerMode.Reactions ? 0 : 8);
            //}

            if (content.Tag is not null
                || content.Tag is Sticker prev && prev?.StickerValue.Id == file.Id)
            {
                return;
            }

            if (args != null && args.Phase < 2)
            {
                args.RegisterUpdateCallback(2, OnContainerContentChanging);
                return;
            }

            if (file.Local.IsDownloadingCompleted)
            {
                if (content.Children[0] is Border border && border.Child is Image photo)
                {
                    photo.Source = await PlaceholderHelper.GetWebPFrameAsync(file.Local.Path, 68);
                    ElementCompositionPreview.SetElementChildVisual(content.Children[0], null);
                }
                else if (content.Children[0] is LottieView lottie)
                {
                    lottie.ColorReplacements = GetColorReplacements(sticker.SetId);
                    lottie.Source = UriEx.ToLocal(file.Local.Path);
                }
                else if (content.Children[0] is AnimationView video)
                {
                    video.Source = new LocalVideoSource(file);
                }

                content.Tag = sticker;
                UpdateManager.Unsubscribe(content);
            }
            else
            {
                ClearContainerContent(content);

                CompositionPathParser.ParseThumbnail(sticker, out ShapeVisual visual, false);
                ElementCompositionPreview.SetElementChildVisual(content.Children[0], visual);

                UpdateManager.Subscribe(content, ViewModel.ProtoService, file, handler, true);

                if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive /*&& args.Phase == 0*/)
                {
                    ViewModel.ProtoService.DownloadFile(file.Id, 1);
                }
            }
        }

        private async void UpdateSticker(object target, File file)
        {
            var content = target as Grid;
            if (content == null)
            {
                return;
            }

            if (content.Children[0] is Border border && border.Child is Image photo)
            {
                photo.Source = await PlaceholderHelper.GetWebPFrameAsync(file.Local.Path, 68);
                ElementCompositionPreview.SetElementChildVisual(content.Children[0], null);
            }
            else if (content.Children[0] is LottieView lottie)
            {
                lottie.Source = UriEx.ToLocal(file.Local.Path);
                _handler.ThrottleVisibleItems();
            }
            else if (content.Children[0] is AnimationView video)
            {
                video.Source = new LocalVideoSource(file);
                _handler.ThrottleVisibleItems();
            }
        }

        private void ClearContainerContent(Grid content)
        {
            content.Tag = null;

            if (content.Children[0] is Border border && border.Child is Image photo)
            {
                photo.Source = null;
            }
            else if (content.Children[0] is LottieView lottie)
            {
                lottie.Source = null;
            }
            else if (content.Children[0] is AnimationView video)
            {
                video.Source = null;
            }
        }

        #endregion

        private void Toolbar_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }

            if (args.Item is StickerSetViewModel sticker)
            {
                Automation.SetToolTip(args.ItemContainer, sticker.Title);

                var content = args.ItemContainer.ContentTemplateRoot as Grid;

                if (content == null || sticker == null || (sticker.Thumbnail == null && sticker.Covers == null))
                {
                    return;
                }

                var cover = sticker.GetThumbnail();
                if (cover == null)
                {
                    ClearContainerContent(content);
                    return;
                }

                UpdateContainerContent(cover, content, UpdateStickerSet);
            }
        }

        private async void UpdateStickerSet(object target, File file)
        {
            var content = target as Grid;
            if (content == null)
            {
                return;
            }

            if (content.Children[0] is Border border && border.Child is Image photo)
            {
                photo.Source = await PlaceholderHelper.GetWebPFrameAsync(file.Local.Path, 68);
                ElementCompositionPreview.SetElementChildVisual(content.Children[0], null);
            }
            else if (content.Children[0] is LottieView lottie)
            {
                lottie.Source = UriEx.ToLocal(file.Local.Path);
                _toolbarHandler.ThrottleVisibleItems();
            }
            else if (content.Children[0] is AnimationView video)
            {
                video.Source = new LocalVideoSource(file);
                _toolbarHandler.ThrottleVisibleItems();
            }
        }
    }
}
