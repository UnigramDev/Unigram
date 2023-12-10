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
using System.Numerics;
using Telegram.Common;
using Telegram.Services;
using Telegram.Services.Settings;
using Telegram.Streams;
using Telegram.Td.Api;
using Telegram.ViewModels.Drawers;
using Windows.Foundation;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using StickerSetViewModel = Telegram.ViewModels.Drawers.StickerSetViewModel;

namespace Telegram.Controls.Drawers
{
    public class TopicsEmojiDrawer : EmojiDrawer
    {
        public TopicsEmojiDrawer()
            : base(EmojiDrawerMode.CustomEmojis)
        {

        }
    }

    public class ChatPhotoEmojiDrawer : EmojiDrawer
    {
        public ChatPhotoEmojiDrawer()
            : base(EmojiDrawerMode.ChatPhoto)
        {

        }
    }

    public partial class EmojiDrawer : UserControl, IDrawer
    {
        public EmojiDrawerViewModel ViewModel => DataContext as EmojiDrawerViewModel;

        public event ItemClickEventHandler ItemClick;
        public event TypedEventHandler<UIElement, ItemContextRequestedEventArgs<Sticker>> ItemContextRequested;

        private bool _needUpdate;

        private EmojiDrawerMode _mode;

        private EmojiSkinTone _selected;
        private bool _expanded;

        private bool _isActive;

        private readonly AnimatedListHandler _handler;
        private readonly AnimatedListHandler _toolbarHandler;

        private readonly Dictionary<StickerViewModel, Grid> _itemIdToContent = new();
        private long _selectedSetId;

        public EmojiDrawer()
            : this(EmojiDrawerMode.Chat)
        {

        }

        public EmojiDrawer(EmojiDrawerMode mode)
        {
            InitializeComponent();

            ElementCompositionPreview.GetElementVisual(this).Clip = Window.Current.Compositor.CreateInsetClip();

            var header = DropShadowEx.Attach(Separator);
            header.Clip = header.Compositor.CreateInsetClip(0, 40, 0, -40);

            _handler = new AnimatedListHandler(List, AnimatedListType.Emoji);
            _toolbarHandler = new AnimatedListHandler(Toolbar2, AnimatedListType.Emoji);

            _typeToItemHashSetMapping.Add("EmojiSkinTemplate", new HashSet<SelectorItem>());
            _typeToItemHashSetMapping.Add("EmojiTemplate", new HashSet<SelectorItem>());
            _typeToItemHashSetMapping.Add("ItemTemplate", new HashSet<SelectorItem>());
            _typeToItemHashSetMapping.Add("MoreTemplate", new HashSet<SelectorItem>());

            _mode = mode;

            if (mode != EmojiDrawerMode.Chat)
            {
                SearchField.Margin = new Thickness(0, 8, 8, 8);
                Toolbar3.Visibility = Visibility.Collapsed;
                Toolbar2.Header = null;

                if (mode is not EmojiDrawerMode.ChatPhoto and not EmojiDrawerMode.UserPhoto)
                {
                    List.Padding = new Thickness(8, 0, 0, 0);
                    List.ItemContainerStyle.Setters.Add(new Setter(MarginProperty, new Thickness(0, 0, 4, 4)));
                    List.GroupStyle[0].HeaderContainerStyle.Setters.Add(new Setter(PaddingProperty, new Thickness(0, 0, 8, 0)));

                    FluidGridView.GetTriggers(List).Clear();
                    FluidGridView.GetTriggers(List).Add(new FixedGridViewTrigger { ItemLength = 36 });
                }
            }
            else
            {
                UpdateView();
            }

            var debouncer = new EventDebouncer<TextChangedEventArgs>(Constants.TypingTimeout, handler => SearchField.TextChanged += new TextChangedEventHandler(handler));
            debouncer.Invoked += async (s, args) =>
            {
                if (string.IsNullOrWhiteSpace(SearchField.Text))
                {
                    List.ItemsSource = EmojiCollection.View;
                }
                else
                {
                    List.ItemsSource = await Emoji.SearchAsync(ViewModel.ClientService, SearchField.Text, _selected, _mode);
                }
            };
        }

        public bool IsShadowVisible
        {
            get => Separator.Visibility == Visibility.Visible;
            set => Separator.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
        }

        public StickersTab Tab => StickersTab.Emoji;

        public Thickness ScrollingHostPadding
        {
            get => List.Padding;
            set => List.Padding = new Thickness(0, value.Top, 0, value.Bottom);
        }

        public ListViewBase ScrollingHost => List;

        public void Activate(Chat chat, EmojiSearchType type = EmojiSearchType.Default)
        {
            _isActive = true;
            _handler.ThrottleVisibleItems();
            _toolbarHandler.ThrottleVisibleItems();

            SearchField.SetType(ViewModel.ClientService, _mode switch
            {
                EmojiDrawerMode.ChatPhoto => EmojiSearchType.ChatPhoto,
                EmojiDrawerMode.UserPhoto => EmojiSearchType.ChatPhoto,
                EmojiDrawerMode.CustomEmojis => EmojiSearchType.EmojiStatus,
                _ => EmojiSearchType.Default
            });

            if (_mode == EmojiDrawerMode.ChatPhoto)
            {
                ViewModel.UpdateChatPhoto();
            }
            else if (_mode == EmojiDrawerMode.Background)
            {
                ViewModel.UpdateBackground();
            }
            else
            {
                ViewModel.Update();
            }
        }

        public void Deactivate()
        {
            _itemIdToContent.Clear();

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
            if (_mode is not EmojiDrawerMode.ChatPhoto and not EmojiDrawerMode.UserPhoto and not EmojiDrawerMode.Chat)
            {
                return;
            }

            var microsoft = string.Equals(SettingsService.Current.Appearance.EmojiSet, "microsoft");
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
            if (scrollingHost != null && _isActive && scrollingHost.FirstVisibleIndex >= 0)
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
                        }
                        else
                        {
                            Toolbar2.SelectedItem = header.Content;
                            Toolbar.SelectedItem = null;
                        }

                        UpdateToolbar();
                    }
                }
            }
        }

        private void Toolbar_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is GridView toolbar)
            {
                if (toolbar.SelectedItem != null)
                {
                    if (sender == Toolbar2 && Toolbar.SelectedItem != null)
                    {
                        toolbar.ScrollToTop();
                    }
                    else
                    {
                        _ = toolbar.ScrollToItem2(toolbar.SelectedItem, VerticalAlignment.Center);
                    }
                }
                else
                {
                    toolbar.ScrollToTop();
                }
            }
        }

        private void Toolbar_ItemClick(object sender, ItemClickEventArgs e)
        {
            List.ScrollIntoView(e.ClickedItem, ScrollIntoViewAlignment.Leading);
        }

        private async void ListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is EmojiData data)
            {
                _needUpdate = true;

                SettingsService.Current.Emoji.AddRecentEmoji(data.Value);
                SettingsService.Current.Emoji.SortRecentEmoji();
                SettingsService.Current.Emoji.SaveRecentEmoji();

                ItemClick?.Invoke(this, e);
            }
            else if (e.ClickedItem is StickerViewModel sticker)
            {
                if (sticker is MoreStickerViewModel)
                {
                    var groupContainer = List.GroupHeaderContainerFromItemContainer(List.ContainerFromItem(sticker)) as GridViewHeaderItem;
                    if (groupContainer.Content is StickerSetViewModel group)
                    {
                        var response = await ViewModel.ClientService.SendAsync(new GetStickerSet(group.Id));
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

                                UpdateContainerContent(sticker, container.ContentTemplateRoot as Grid, false);
                            }
                        }
                    }
                }
                else
                {
                    if (sticker.FullType is StickerFullTypeCustomEmoji customEmoji)
                    {
                        SettingsService.Current.Emoji.AddRecentEmoji($"{sticker.Emoji};{customEmoji.CustomEmojiId}");
                        SettingsService.Current.Emoji.SortRecentEmoji();
                        SettingsService.Current.Emoji.SaveRecentEmoji();
                    }

                    ItemClick?.Invoke(this, e);
                }
            }
        }

        private async void SearchField_CategorySelected(object sender, EmojiCategorySelectedEventArgs e)
        {
            List.ItemsSource = await Emoji.SearchAsync(ViewModel.ClientService, e.Category.Emojis);
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
            if (_mode is not EmojiDrawerMode.ChatPhoto and not EmojiDrawerMode.UserPhoto and not EmojiDrawerMode.Chat)
            {
                return;
            }

            if (Toolbar.SelectedItem == null != _emojiCollapsed || collapse)
            {
                _emojiCollapsed = Toolbar.SelectedItem == null;

                var show = !_emojiCollapsed;

                var toolbar = ElementCompositionPreview.GetElementVisual(Toolbar3);
                var pill = ElementCompositionPreview.GetElementVisual(ToolbarPill);
                var panel = ElementCompositionPreview.GetElementVisual(Toolbar2.ItemsPanelRoot);

                ElementCompositionPreview.SetIsTranslationEnabled(Toolbar2.ItemsPanelRoot, true);

                var clip = toolbar.Compositor.CreateInsetClip();
                var offset = 144 - 32;

                var ellipse = toolbar.Compositor.CreateRoundedRectangleGeometry();
                ellipse.CornerRadius = new Vector2(4);

                pill.Clip = toolbar.Compositor.CreateGeometricClip(ellipse);
                toolbar.Clip = clip;
                Toolbar3.Width = 144 + 36;

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
                    Toolbar3.Width = show ? 144 + 36 : 32 + 36;
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
                var child = VisualTreeHelper.GetChild(elements[i], 0) as UIElement;
                if (child == null)
                {
                    continue;
                }

                var visual = ElementCompositionPreview.GetElementVisual(child);

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
                    ? "ItemTemplate"
                    : args.Item is EmojiSkinData ? "EmojiSkinTemplate" : "EmojiTemplate";

            var relevantHashSet = _typeToItemHashSetMapping[typeName];

            // args.ItemContainer is used to indicate whether the ListView is proposing an
            // ItemContainer (ListViewItem) to use. If args.Itemcontainer != null, then there was a
            // recycled ItemContainer available to be reused.
            if (args.ItemContainer is EmojiGridViewItem container)
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
                    var item = new EmojiGridViewItem(typeName);
                    item.ContentTemplate = Resources[typeName] as DataTemplate;
                    item.Style = List.ItemContainerStyle;
                    item.ContextRequested += OnContextRequested;
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

                var response = await ViewModel.ClientService.SendAsync(new GetStickerSet(group.Id));
                if (response is StickerSet full)
                {
                    group.Update(full, false);

                    //return;

                    foreach (var sticker in group.Stickers)
                    {
                        if (_itemIdToContent.TryGetValue(sticker, out Grid content))
                        {
                            UpdateContainerContent(sticker, content, false);
                        }
                    }
                }
            }
        }

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            var content = args.ItemContainer.ContentTemplateRoot as Grid;
            var sticker = args.Item as StickerViewModel;

            if (args.InRecycleQueue)
            {
                if (sticker != null)
                {
                    _itemIdToContent.Remove(sticker);
                }

                if (args.ItemContainer is EmojiGridViewItem container)
                {
                    // XAML has indicated that the item is no longer being shown, so add it to the recycle queue
                    var tag = container.TypeName;
                    var added = _typeToItemHashSetMapping[tag].Add(args.ItemContainer);
                }

                return;
            }
            else if (sticker != null)
            {
                _itemIdToContent[sticker] = content;

                if (content.Children[0] is TextBlock textBlock && sticker is MoreStickerViewModel more)
                {
                    textBlock.Text = $"+{more.TotalCount}";
                }
                else
                {
                    UpdateContainerContent(sticker, content, false);

                    if (_mode == EmojiDrawerMode.Reactions && args.ItemIndex > 5 && args.ItemIndex < 8 * 6)
                    {
                        var x1 = 4;
                        var y1 = 0;
                        var x2 = (int)(args.ItemIndex % 8);
                        var y2 = (int)(args.ItemIndex / 8d);

                        if (y2 >= 2)
                        {
                            y2++;
                        }

                        var xd = Math.Abs(x1 - x2);
                        var yd = Math.Abs(y1 - y2);

                        var distance = xd + yd - 1;
                        distance = yd;

                        var visual = ElementCompositionPreview.GetElementVisual(content);
                        var scale = visual.Compositor.CreateVector3KeyFrameAnimation();
                        scale.InsertKeyFrame(0, Vector3.Zero);
                        scale.InsertKeyFrame(1, Vector3.One);
                        scale.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;
                        scale.DelayTime = TimeSpan.FromMilliseconds(33 * distance);
                        scale.Duration = Constants.FastAnimation;

                        var opacity = visual.Compositor.CreateScalarKeyFrameAnimation();
                        opacity.InsertKeyFrame(0, 0);
                        opacity.InsertKeyFrame(1, 1);
                        opacity.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;
                        opacity.DelayTime = TimeSpan.FromMilliseconds(33 * distance);
                        opacity.Duration = Constants.FastAnimation;

                        visual.CenterPoint = new Vector3(16, 0, 0);
                        visual.StartAnimation("Opacity", opacity);
                        visual.StartAnimation("Scale", scale);
                    }
                }

                args.Handled = true;
            }
        }

        private void UpdateContainerContent(Sticker sticker, Grid content, bool toolbar)
        {
            var file = sticker?.StickerValue;
            if (file == null)
            {
                return;
            }

            var animated = content.Children[0] as AnimatedImage;
            animated.Source = new DelayedFileSource(ViewModel.ClientService, sticker)
            {
                NeedsRepainting = sticker.FullType is StickerFullTypeCustomEmoji customEmoji
                    && customEmoji.NeedsRepainting
            };

            //if (toolbar)
            //{
            //    content.Padding = new Thickness(4);
            //}
            //else
            //{
            //    content.Padding = new Thickness(_mode == EmojiDrawerMode.Reactions ? 0 : 8);
            //}
        }

        #endregion

        private void Toolbar_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
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
                if (cover == null)
                {
                    return;
                }

                UpdateContainerContent(cover, content, true);
                args.Handled = true;
            }
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

        private void Player_Ready(object sender, System.EventArgs e)
        {
            _handler.ThrottleVisibleItems();
        }

        private void Toolbar_Ready(object sender, System.EventArgs e)
        {
            _toolbarHandler.ThrottleVisibleItems();
        }
    }

    public class EmojiGridViewItem : GridViewItem
    {
        private readonly string _typeName;

        public EmojiGridViewItem(string typeName)
        {
            _typeName = typeName;
        }

        public string TypeName => _typeName;
    }
}
