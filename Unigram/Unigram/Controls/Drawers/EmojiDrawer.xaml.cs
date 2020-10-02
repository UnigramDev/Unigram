using LinqToVisualTree;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Unigram.Common;
using Unigram.Services;
using Unigram.Services.Settings;
using Unigram.ViewModels;
using Unigram.Views;
using Windows.UI.Text.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media;

namespace Unigram.Controls.Drawers
{
    public sealed partial class EmojiDrawer : UserControl, IDrawer
    {
        public TLViewModelBase ViewModel => DataContext as TLViewModelBase;

        public event ItemClickEventHandler ItemClick;

        private bool _needUpdate;

        private EmojiSkinTone _selected;
        private bool _expanded;

        public EmojiDrawer()
        {
            this.InitializeComponent();

            ElementCompositionPreview.GetElementVisual(this).Clip = Window.Current.Compositor.CreateInsetClip();

            var shadow = DropShadowEx.Attach(Separator, 20, 0.25f);
            shadow.RelativeSizeAdjustment = Vector2.One;

            _typeToItemHashSetMapping["EmojiSkinTemplate"] = new HashSet<SelectorItem>();
            _typeToItemHashSetMapping["EmojiTemplate"] = new HashSet<SelectorItem>();
        }

        public StickersTab Tab => StickersTab.Emoji;

        public void Activate() { }

        public void Deactivate() { }

        public void LoadVisibleItems() { }

        public void UnloadVisibleItems() { }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            var scrollingHost = List.Descendants<ScrollViewer>().FirstOrDefault() as ScrollViewer;
            if (scrollingHost != null)
            {
                // Syncronizes GridView with the toolbar ListView
                scrollingHost.ViewChanged += ScrollingHost_ViewChanged;
                ScrollingHost_ViewChanged(null, null);
            }
        }

        public void SetView(StickersPanelMode mode)
        {
            VisualStateManager.GoToState(this, mode == StickersPanelMode.Overlay
                ? "FilledState"
                : mode == StickersPanelMode.Sidebar
                ? "SidebarState"
                : "NarrowState", false);

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
                var items = Emoji.Get(tone, !microsoft);
                EmojiCollection.Source = items;
                Toolbar.ItemsSource = items;
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
                        Toolbar.SelectedItem = header.Content;
                        Toolbar.ScrollIntoView(header.Content);
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
            if (e.ClickedItem is EmojiGroup group)
            {
                List.ScrollIntoView(e.ClickedItem, ScrollIntoViewAlignment.Leading);
            }
        }

        private void ListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            ItemClick?.Invoke(this, e);

            if (e.ClickedItem is EmojiData data)
            {
                _needUpdate = true;

                SettingsService.Current.Emoji.AddRecentEmoji(data.Value);
                SettingsService.Current.Emoji.SortRecentEmoji();
                SettingsService.Current.Emoji.SaveRecentEmoji();
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
                EmojiCollection.Source = await Emoji.SearchAsync(ViewModel.ProtoService, FieldEmoji.Text, _selected, CoreTextServicesManager.GetForCurrentView().InputLanguage.LanguageTag);
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
            if (radio.Content is int value && EmojiCollection.Source is List<EmojiGroup> groups)
            {
                foreach (var group in groups)
                {
                    foreach (var item in group.Items.OfType<EmojiSkinData>())
                    {
                        item.SetValue((EmojiSkinTone)value);
                    }
                }

                if (EmojiCollection.Source != Toolbar.ItemsSource && Toolbar.ItemsSource is List<EmojiGroup> toolbar)
                {
                    foreach (var group in toolbar)
                    {
                        foreach (var item in group.Items.OfType<EmojiSkinData>())
                        {
                            item.SetValue((EmojiSkinTone)value);
                        }
                    }
                }

                SettingsService.Current.Stickers.SkinTone = (EmojiSkinTone)value;
                UpdateSkinTone((EmojiSkinTone)value, false, true);
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

        private Dictionary<string, HashSet<SelectorItem>> _typeToItemHashSetMapping = new Dictionary<string, HashSet<SelectorItem>>();

        private void OnChoosingItemContainer(ListViewBase sender, ChoosingItemContainerEventArgs args)
        {
            var typeName = args.Item is EmojiSkinData ? "EmojiSkinTemplate" : "EmojiTemplate";
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
                    var item = new GridViewItem { ContentTemplate = Resources[typeName] as DataTemplate, Tag = typeName };
                    item.Style = List.ItemContainerStyle;
                    args.ItemContainer = item;
                }
            }

            // Indicate to XAML that we picked a container for it
            args.IsContainerPrepared = true;
        }

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue == true)
            {
                // XAML has indicated that the item is no longer being shown, so add it to the recycle queue
                var tag = args.ItemContainer.Tag as string;
                var added = _typeToItemHashSetMapping[tag].Add(args.ItemContainer);
            }
        }

        #endregion
    }
}
