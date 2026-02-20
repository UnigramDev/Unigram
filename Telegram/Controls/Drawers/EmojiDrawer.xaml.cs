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
using Telegram.Converters;
using Telegram.Navigation;
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
    public partial class TopicsEmojiDrawer : EmojiDrawer
    {
        public TopicsEmojiDrawer()
            : base(EmojiDrawerMode.Topics)
        {

        }
    }

    public partial class ChatPhotoEmojiDrawer : EmojiDrawer
    {
        public ChatPhotoEmojiDrawer()
            : base(EmojiDrawerMode.ChatPhoto)
        {

        }
    }

    public partial class EmojiDrawerItemClickEventArgs : EventArgs
    {
        public EmojiDrawerItemClickEventArgs(object clickedItem)
        {
            ClickedItem = clickedItem;
        }

        public Sticker Sticker { get; }

        public object ClickedItem { get; }
    }

    public partial class EmojiDrawer : UserControl, IDrawer
    {
        public EmojiDrawerViewModel ViewModel => DataContext as EmojiDrawerViewModel;

        public event EventHandler<EmojiDrawerItemClickEventArgs> ItemClick;
        public event TypedEventHandler<UIElement, ItemContextRequestedEventArgs<StickerViewModel>> ItemContextRequested;

        private EmojiDrawerMode _mode;

        private bool _isActive;

        private readonly AnimatedListHandler _handler;
        private readonly ZoomableListHandler _zoomer;

        private readonly AnimatedListHandler _toolbarHandler;

        private readonly EventDebouncer<TextChangedEventArgs> _typing;

        private readonly Dictionary<StickerViewModel, Grid> _itemIdToContent = new();
        private long _selectedSetId;

        public EmojiDrawer()
            : this(EmojiDrawerMode.Chat)
        {

        }

        public EmojiDrawer(EmojiDrawerMode mode)
        {
            InitializeComponent();

            this.CreateInsetClip();

            var header = VisualUtilities.DropShadow(Separator);
            header.Clip = header.Compositor.CreateInsetClip(0, 40, 0, -40);

            _handler = new AnimatedListHandler(List, AnimatedListType.Emoji);
            _toolbarHandler = new AnimatedListHandler(Toolbar2, AnimatedListType.Emoji);

            _zoomer = new ZoomableListHandler(List);
            _zoomer.Opening = _handler.Suspend;
            _zoomer.Closing = _handler.Resume;

            _typeToItemHashSetMapping.Add("EmojiSkinTemplate", new HashSet<SelectorItem>());
            _typeToItemHashSetMapping.Add("EmojiTemplate", new HashSet<SelectorItem>());
            _typeToItemHashSetMapping.Add("ItemTemplate", new HashSet<SelectorItem>());
            _typeToItemHashSetMapping.Add("MoreTemplate", new HashSet<SelectorItem>());

            _mode = mode;

            if (mode == EmojiDrawerMode.Topics)
            {
                TopicIconRoot.Visibility = Visibility.Visible;
            }
            else if (mode is EmojiDrawerMode.EmojiStatus or EmojiDrawerMode.ChatEmojiStatus)
            {
                EmojiStatusIconRoot.Visibility = Visibility.Visible;
            }

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

                    var trigger = new FixedGridViewTrigger { ItemLength = 36 };
                    trigger.Activated += FluidGridViewTrigger_Activated;

                    FluidGridView.GetTriggers(List).Clear();
                    FluidGridView.GetTriggers(List).Add(trigger);
                }
            }
            else
            {
                UpdateView();
            }

            _typing = new EventDebouncer<TextChangedEventArgs>(Constants.TypingTimeout, handler => SearchField.TextChanged += new TextChangedEventHandler(handler));
            _typing.Invoked += (s, args) =>
            {
                if (string.IsNullOrWhiteSpace(SearchField.Text))
                {
                    List.ItemsSource = EmojiCollection.View;
                }
                else if (ViewModel != null)
                {
                    List.ItemsSource = new SearchEmojiCollection(ViewModel.ClientService, SearchField.Text, _mode);
                }
            };
        }

        public void HideNavigation()
        {
            ToolbarContainer.Visibility = Visibility.Collapsed;
            SearchField.Visibility = Visibility.Collapsed;
            List.Padding = new Thickness(8, 8, 0, 0);
        }

        public void UpdateTopicIcon(string name, int color)
        {
            var brush = ForumTopicCell.GetIconGradient(new ForumTopicIcon(color, 0));

            TopicIconPath.Fill = brush;
            TopicIconPath.Stroke = new SolidColorBrush(brush.GradientStops[1].Color);
            TopicIconText.Text = InitialNameStringConverter.Convert(name);
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
            set => List.Padding = new Thickness(value.Left, value.Top, value.Right, value.Bottom);
        }

        public ListViewBase ScrollingHost => List;

        public void Activate(Chat chat, EmojiSearchType type = EmojiSearchType.Default)
        {
            _isActive = true;
            _handler.Resume();
            _toolbarHandler.ThrottleVisibleItems();

            if (ViewModel.IsPremium)
            {
                SearchField.SetType(ViewModel.ClientService, _mode switch
                {
                    EmojiDrawerMode.ChatPhoto => EmojiSearchType.ChatPhoto,
                    EmojiDrawerMode.UserPhoto => EmojiSearchType.ChatPhoto,
                    EmojiDrawerMode.EmojiStatus => EmojiSearchType.EmojiStatus,
                    EmojiDrawerMode.ChatEmojiStatus => EmojiSearchType.EmojiStatus,
                    EmojiDrawerMode.Reactions => EmojiSearchType.Combined,
                    _ => EmojiSearchType.Default
                });
            }

            ViewModel.OpenChat(chat);
            ViewModel.Update();
        }

        public void Deactivate()
        {
            _itemIdToContent.Clear();

            _isActive = false;
            _handler.UnloadItems();
            _toolbarHandler.UnloadItems();

            _typing.Cancel();

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

        public void ThrottleVisibleItems()
        {
            if (_isActive)
            {
                _handler.ThrottleVisibleItems();
                _toolbarHandler.ThrottleVisibleItems();
            }
        }

        public void UnloadVisibleItems()
        {
            _handler.UnloadVisibleItems();
            _toolbarHandler.UnloadVisibleItems();
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            var scrollingHost = List.GetChild<ScrollViewer>();
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

            UpdateToolbar();
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

        public void InsertEmoji(EmojiSkinData emoji)
        {
            SettingsService.Current.Emoji.SetEmojiSkinTone(emoji);
            SettingsService.Current.Emoji.AddRecentEmoji(emoji);
            ItemClick?.Invoke(this, new EmojiDrawerItemClickEventArgs(emoji));
        }

        private async void ListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is EmojiData data)
            {
                if (data is EmojiSkinData skin && !SettingsService.Current.Emoji.HasSkinTone(skin))
                {
                    var container = ScrollingHost.ContainerFromItem(e.ClickedItem);
                    if (container != null)
                    {
                        var flyout = new Flyout
                        {
                            FlyoutPresenterStyle = BootStrapper.Current.Resources["CommandFlyoutPresenterStyle"] as Style,
                        };

                        flyout.Content = new EmojiSkinFlyout(this, flyout, skin);
                        flyout.ShowAt(container as UIElement, FlyoutPlacementMode.Top);
                        return;
                    }
                }

                SettingsService.Current.Emoji.AddRecentEmoji(data);
                ItemClick?.Invoke(this, new EmojiDrawerItemClickEventArgs(e.ClickedItem));
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

                            foreach (var item in group.Stickers)
                            {
                                if (_itemIdToContent.TryGetValue(item, out Grid content))
                                {
                                    var animation = content.Children[0] as AnimatedImage;
                                    animation.Source = new DelayedFileSource(ViewModel.ClientService, item);
                                }
                            }
                        }
                    }
                }
                else
                {
                    if (sticker.FullType is StickerFullTypeCustomEmoji customEmoji)
                    {
                        SettingsService.Current.Emoji.AddRecentEmoji(sticker.Emoji, customEmoji.CustomEmojiId);
                    }

                    ItemClick?.Invoke(this, new EmojiDrawerItemClickEventArgs(e.ClickedItem));
                }
            }
        }

        private async void SearchField_CategorySelected(object sender, EmojiCategorySelectedEventArgs e)
        {
            if (e.Category.Source is EmojiCategorySourceSearch search)
            {
                List.ItemsSource = await Emoji.SearchAsync(ViewModel.ClientService, search.Emojis);
            }
        }

        private bool _emojiCollapsed = false;

        private void UpdateToolbar(bool collapse = false)
        {
            if (_mode is not EmojiDrawerMode.ChatPhoto and not EmojiDrawerMode.UserPhoto and not EmojiDrawerMode.Chat)
            {
                return;
            }

            if (Toolbar2.ItemsPanelRoot == null)
            {
                return;
            }

            var collapsed = Toolbar.SelectedItem == null;
            if (collapsed != _emojiCollapsed || collapse)
            {
                _emojiCollapsed = collapsed;

                var show = !_emojiCollapsed;

                var toolbar = ElementComposition.GetElementVisual(Toolbar3);
                var pill = ElementComposition.GetElementVisual(ToolbarPill);
                var panel = ElementComposition.GetElementVisual(Toolbar2.ItemsPanelRoot);

                ElementCompositionPreview.SetIsTranslationEnabled(Toolbar2.ItemsPanelRoot, true);

                var clip = toolbar.Compositor.CreateInsetClip();
                var offset = 144 - 32;

                var ellipse = toolbar.Compositor.CreateRoundedRectangleGeometry();
                ellipse.CornerRadius = new Vector2(4);

                pill.Clip = toolbar.Compositor.CreateGeometricClip(ellipse);
                toolbar.Clip = clip;
                Toolbar3.Width = 144;

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
                    Toolbar3.Width = show ? 144 : 32;
                };

                clip.StartAnimation("RightInset", animClip);
                panel.StartAnimation("Translation.X", animOffset);
                ellipse.StartAnimation("Size", animSize);
                pill.StartAnimation("Opacity", animOpacity);

                batch.End();
            }
        }

        #region Recycle

        private readonly Dictionary<string, HashSet<SelectorItem>> _typeToItemHashSetMapping = new();

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

                    _zoomer.ElementPrepared(args.ItemContainer);
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

                    foreach (var sticker in group.Stickers)
                    {
                        if (sticker.StickerValue != null && _itemIdToContent.TryGetValue(sticker, out Grid content))
                        {
                            var animation = content.Children[0] as AnimatedImage;
                            animation.Source = new DelayedFileSource(ViewModel.ClientService, sticker);
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

                    if (false && _mode == EmojiDrawerMode.Reactions && args.ItemIndex > 5 && args.ItemIndex < 8 * 6)
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

                        var visual = ElementComposition.GetElementVisual(content);
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
                if (content?.Children[0] is FontIcon icon)
                {
                    icon.Glyph = sticker.Name switch
                    {
                        "tg/recentlyUsed" => Icons.EmojiRecents,
                        "tg/collectibles" => Icons.Diamond,
                        _ => string.Empty
                    };
                }
                else if (content?.Children[0] is AnimatedImage animated)
                {
                    animated.Source = DelayedFileSource.FromStickerSetInfo(ViewModel.ClientService, sticker);
                }

                args.Handled = true;
            }
        }

        private void OnContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
            var item = List.ItemFromContainer(sender);
            if (item is StickerViewModel sticker)
            {
                ItemContextRequested?.Invoke(sender, new ItemContextRequestedEventArgs<StickerViewModel>(sticker, args));
            }
            else if (item is EmojiSkinData emoji)
            {
                var flyout = new Flyout
                {
                    FlyoutPresenterStyle = BootStrapper.Current.Resources["CommandFlyoutPresenterStyle"] as Style,
                };

                flyout.Content = new EmojiSkinFlyout(this, flyout, emoji);
                flyout.ShowAt(sender, FlyoutPlacementMode.Top);
            }
        }

        private void Player_Ready(object sender, System.EventArgs e)
        {
            _handler.ThrottleVisibleItems();
        }

        private void Toolbar_Ready(object sender, System.EventArgs e)
        {
            _toolbarHandler.ThrottleVisibleItems();
        }

        private void FluidGridViewTrigger_Activated(object sender, double e)
        {
            DefaultIcon.Width = e;
            DefaultIcon.Height = e;
            DefaultIcon.Margin = new Thickness(0, 0, 0, -e);
        }
    }

    public partial class EmojiGridViewItem : GridViewItem
    {
        private readonly string _typeName;

        public EmojiGridViewItem(string typeName)
        {
            _typeName = typeName;
        }

        public string TypeName => _typeName;
    }
}
