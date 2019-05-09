using LinqToVisualTree;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Unigram.Common;
using Unigram.Services;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace Unigram.Controls.Views
{
    public sealed partial class EmojisView : UserControl
    {
        public event EventHandler Switch;
        public event ItemClickEventHandler ItemClick;

        private bool _expanded;

        public EmojisView()
        {
            this.InitializeComponent();

            var shadow = DropShadowEx.Attach(Separator, 20, 0.25f);

            Toolbar.SizeChanged += (s, args) =>
            {
                shadow.Size = args.NewSize.ToVector2();
            };
        }

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

        public void SetView(bool widget)
        {
            VisualStateManager.GoToState(this, widget ? "FilledState" : "NarrowState", false);

            var microsoft = string.Equals(SettingsService.Current.Appearance.EmojiSetId, "microsoft");
            var tone = SettingsService.Current.Stickers.SkinTone;

            if (Toolbar.ItemsSource is List<EmojiGroup> groups)
            {
                if (groups.Count == Emoji.Items.Count && microsoft)
                {
                    var items = Emoji.Get(tone, false);
                    EmojisViewSource.Source = items;
                    Toolbar.ItemsSource = items;
                }
                else if (groups.Count == Emoji.Items.Count - 1 && !microsoft)
                {
                    var items = Emoji.Get(tone, true);
                    EmojisViewSource.Source = items;
                    Toolbar.ItemsSource = items;
                }
            }
            else
            {
                var items = Emoji.Get(tone, !microsoft);
                EmojisViewSource.Source = items;
                Toolbar.ItemsSource = items;
            }

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
        }

        private void Switch_Click(object sender, RoutedEventArgs e)
        {
            Switch?.Invoke(this, EventArgs.Empty);
        }

        private void SkinTone_Click(object sender, RoutedEventArgs e)
        {
            if (Grid.GetColumn(SkinFitz6) == 0)
            {
                UpdateSkinTone(SettingsService.Current.Stickers.SkinTone, true, true);
                return;
            }

            var radio = sender as RadioButton;
            if (radio.Content is int value && EmojisViewSource.Source is List<EmojiGroup> groups)
            {
                foreach (var group in groups)
                {
                    foreach (var item in group.Items.OfType<EmojiSkinData>())
                    {
                        item.SetValue((EmojiSkinTone)value);
                    }
                }

                SettingsService.Current.Stickers.SkinTone = (EmojiSkinTone)value;
                UpdateSkinTone((EmojiSkinTone)value, false, true);

                return;
                var items = Emoji.Get((EmojiSkinTone)value, true);

                EmojisViewSource.Source = items;
                Toolbar.ItemsSource = items;
            }
        }

        private void UpdateSkinTone(EmojiSkinTone selected, bool expand, bool animated)
        {
            Canvas.SetZIndex(SkinDefault, (int)selected < 0 ? 0 : (int)selected > 0 ? 0 : 1);
            Canvas.SetZIndex(SkinFitz12, (int)selected < 1 ? -1 : (int)selected > 1 ? 1 : 2);
            Canvas.SetZIndex(SkinFitz3, (int)selected < 2 ? -2 : (int)selected > 2 ? 2 : 3);
            Canvas.SetZIndex(SkinFitz4, (int)selected < 3 ? -3 : (int)selected > 3 ? 3 : 4);
            Canvas.SetZIndex(SkinFitz5, (int)selected < 4 ? -4 : (int)selected > 4 ? 4 : 5);
            Canvas.SetZIndex(SkinFitz6, (int)selected < 5 ? -5 : (int)selected > 5 ? 5 : 6);

            Grid.SetColumn(SkinDefault, expand ? 0 : 0);
            Grid.SetColumn(SkinFitz12, expand ? 1 : 0);
            Grid.SetColumn(SkinFitz3, expand ? 2 : 0);
            Grid.SetColumn(SkinFitz4, expand ? 3 : 0);
            Grid.SetColumn(SkinFitz5, expand ? 4 : 0);
            Grid.SetColumn(SkinFitz6, expand ? 5 : 0);
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

                var anim = visual.Compositor.CreateScalarKeyFrameAnimation();
                anim.InsertKeyFrame(0, expand ? from * -40 : from * 40);
                anim.InsertKeyFrame(1, 0);

                visual.StartAnimation("Offset.X", anim);
            }

            _expanded = expand;
        }

        private void OnChoosingItemContainer(ListViewBase sender, ChoosingItemContainerEventArgs args)
        {
            if (args.ItemContainer == null)
            {
                args.ItemContainer = new GridViewItem();
                args.ItemContainer.Style = List.ItemContainerStyle;
            }

            args.ItemContainer.ContentTemplate = Resources[args.Item is EmojiSkinData ? "EmojiSkinTemplate" : "EmojiTemplate"] as DataTemplate;
            args.IsContainerPrepared = true;
        }
    }
}
