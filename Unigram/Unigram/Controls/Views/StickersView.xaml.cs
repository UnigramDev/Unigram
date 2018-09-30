using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Unigram.ViewModels;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using LinqToVisualTree;
using Unigram.Common;
using System.Threading.Tasks;
using Unigram.Views.Settings;
using Telegram.Td.Api;
using System.Diagnostics;
using Windows.Storage;
using Unigram.Native;
using System.Collections.Concurrent;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI;
using System.Numerics;
using Unigram.Services;
using Unigram.ViewModels.Delegates;

namespace Unigram.Controls.Views
{
    public sealed partial class StickersView : UserControl, IFileDelegate
    {
        public DialogViewModel ViewModel => DataContext as DialogViewModel;

        public Action<string> EmojiClick { get; set; }
        public Action<Sticker> StickerClick { get; set; }
        public Action<Animation> AnimationClick { get; set; }

        private FileContext<ViewModels.Dialogs.StickerViewModel> _stickers = new FileContext<ViewModels.Dialogs.StickerViewModel>();
        private FileContext<Animation> _animations = new FileContext<Animation>();

        private ScrollViewer stickersScroll;

        public StickersView()
        {
            InitializeComponent();

            var separator = ElementCompositionPreview.GetElementVisual(Separator);
            var shadow = separator.Compositor.CreateDropShadow();
            shadow.BlurRadius = 20;
            shadow.Opacity = 0.25f;
            //shadow.Offset = new Vector3(-20, 0, 0);
            shadow.Color = Colors.Black;

            var visual = separator.Compositor.CreateSpriteVisual();
            visual.Shadow = shadow;
            visual.Size = new Vector2(0, 0);
            visual.Offset = new Vector3(0, 0, 0);
            //visual.Clip = visual.Compositor.CreateInsetClip(-100, 0, 19, 0);

            ElementCompositionPreview.SetElementChildVisual(Separator, visual);

            Toolbar.SizeChanged += (s, args) =>
            {
                visual.Size = new Vector2((float)args.NewSize.Width, (float)args.NewSize.Height);
            };
        }

        private void OnDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
        {
            if (ViewModel != null) Bindings.Update();
            if (ViewModel == null) Bindings.StopTracking();
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            var scrollViewer = Stickers.GetScrollViewer();
            if (scrollViewer != null)
            {
                stickersScroll = scrollViewer;
                stickersScroll.ViewChanged += Stickers_ViewChanged;
            }
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            Bindings.StopTracking();
        }

        public async void UpdateFile(File file)
        {
            if (!file.Local.IsDownloadingCompleted)
            {
                return;
            }

            if (_stickers.TryGetValue(file.Id, out List<ViewModels.Dialogs.StickerViewModel> items) && items.Count > 0)
            {
                foreach (var item in items)
                {
                    item.UpdateFile(file);

                    var container = Stickers.ContainerFromItem(item) as SelectorItem;
                    if (container == null)
                    {
                        return;
                    }

                    var content = container.ContentTemplateRoot as Image;
                    content.Source = await PlaceholderHelper.GetWebpAsync(file.Local.Path);
                }
            }

            foreach (MosaicMediaRow line in GifsView.Items)
            {
                var any = false;
                foreach (var item in line)
                {
                    if (item.Item is Animation animation && animation.UpdateFile(file))
                    {
                        any = true;
                    }
                }

                if (!any)
                {
                    continue;
                }

                var container = GifsView.ContainerFromItem(line) as SelectorItem;
                if (container == null)
                {
                    continue;
                }

                var content = container.ContentTemplateRoot as MosaicRow;
                if (content == null)
                {
                    continue;
                }

                content.UpdateFile(line, file);
            }

            foreach (ViewModels.Dialogs.StickerSetViewModel stickerSet in Toolbar.Items)
            {
                if (stickerSet.Covers == null)
                {
                    continue;
                }

                var cover = stickerSet.Covers.FirstOrDefault();
                if (cover == null || cover.Thumbnail == null)
                {
                    continue;
                }

                var container = Toolbar.ContainerFromItem(stickerSet) as SelectorItem;
                if (container == null)
                {
                    continue;
                }

                var content = container.ContentTemplateRoot as Image;
                if (content == null)
                {
                    continue;
                }

                if (cover.UpdateFile(file))
                {
                    content.Source = await PlaceholderHelper.GetWebpAsync(file.Local.Path);
                }
            }

            //foreach (ViewModels.Dialogs.StickerViewModel item in Stickers.Items)
            //{
            //    if (item.UpdateFile(file) && file.Local.IsDownloadingCompleted)
            //    {
            //        var container = Stickers.ContainerFromItem(item) as SelectorItem;
            //        if (container == null)
            //        {
            //            continue;
            //        }

            //        var content = container.ContentTemplateRoot as Image;
            //        if (item.Thumbnail.Photo.Id == file.Id)
            //        {
            //            var temp = await StorageFile.GetFileFromPathAsync(file.Local.Path);
            //            var buffer = await FileIO.ReadBufferAsync(temp);

            //            content.Source = WebPImage.DecodeFromBuffer(buffer);
            //        }
            //    }
            //}
        }

        private void Mosaic_Click(object item)
        {
            if (item is Animation animation)
            {
                Animation_Click(null, animation);
            }
        }

        private void Animation_Click(object sender, Animation animation)
        {
            AnimationClick?.Invoke(animation);

            if (Window.Current.Bounds.Width >= 500)
            {
                Focus(FocusState.Programmatic);
            }
        }

        private void Emojis_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is string emoji)
            {
                EmojiClick?.Invoke(emoji);
            }
        }

        private void Emojis_Switch(object sender, EventArgs e)
        {
            Emojis.Visibility = Visibility.Collapsed;
            RootPanel.Visibility = Visibility.Visible;
        }

        private void Emojis_Click(object sender, RoutedEventArgs e)
        {
            if (Emojis == null)
                FindName(nameof(Emojis));

            RootPanel.Visibility = Visibility.Collapsed;
            Emojis.Visibility = Visibility.Visible;
        }

        private void Stickers_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is ViewModels.Dialogs.StickerViewModel sticker && sticker.StickerValue != null)
            {
                StickerClick?.Invoke(sticker.Get());
            }

            if (Window.Current.Bounds.Width >= 500)
            {
                Focus(FocusState.Programmatic);
            }
        }

        private async void Featured_ItemClick(object sender, ItemClickEventArgs e)
        {
            //await StickerSetView.Current.ShowAsync(((TLDocument)e.ClickedItem).StickerSet);
        }

        private void Stickers_Loaded(object sender, RoutedEventArgs e)
        {
            var scrollingHost = Stickers.Descendants<ScrollViewer>().FirstOrDefault() as ScrollViewer;
            if (scrollingHost != null)
            {
                // Syncronizes GridView with the toolbar ListView
                scrollingHost.ViewChanged += ScrollingHost_ViewChanged;
                ScrollingHost_ViewChanged(null, null);
            }
        }

        private void Pivot_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (Pivot.SelectedIndex != 2)
            {
                Toolbar.SelectedItem = null;
            }
            else
            {
                ScrollingHost_ViewChanged(null, null);
            }

            //if (Pivot.SelectedIndex == 0)
            //{
            //    var text = ViewModel.GetText();
            //    if (string.IsNullOrWhiteSpace(text))
            //    {
            //        ViewModel.SetText("@gif ");
            //        ViewModel.ResolveInlineBot("gif");
            //    }
            //}
        }

        private void Toolbar_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (Toolbar.SelectedItem != null)
            {
                Pivot.SelectedIndex = 2;
            }

            //Stickers.ScrollIntoView(((TLMessagesStickerSet)Toolbar.SelectedItem).Documents[0]);

            //Pivot.SelectedIndex = Math.Min(1, Toolbar.SelectedIndex);
            //Stickers.ScrollIntoView(ViewModel.StickerSets[Toolbar.SelectedIndex][0]);
        }

        private void Toolbar_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is ViewModels.Dialogs.StickerSetViewModel set && set.Stickers != null)
            {
                Stickers.ScrollIntoView(e.ClickedItem, ScrollIntoViewAlignment.Leading);
            }
        }

        public async void Refresh()
        {
            // TODO: memes

            await Task.Delay(100);
            Pivot_SelectionChanged(null, null);
        }

        private void ScrollingHost_ViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
        {
            var scrollingHost = Stickers.ItemsPanelRoot as ItemsWrapGrid;
            if (scrollingHost != null && Pivot.SelectedIndex == 2)
            {
                var first = Stickers.ContainerFromIndex(scrollingHost.FirstVisibleIndex);
                if (first != null)
                {
                    var header = Stickers.GroupHeaderContainerFromItemContainer(first) as GridViewHeaderItem;
                    if (header != null && header != Toolbar.SelectedItem)
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
            ViewModel.GroupStickersCommand.Execute(null);
        }

        private void Settings_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.NavigationService.Navigate(typeof(SettingsStickersPage));
        }

        private void Install_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.Stickers.InstallCommand.Execute(((Button)sender).DataContext);
        }

        private async void Stickers_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            var content = args.ItemContainer.ContentTemplateRoot as Image;

            if (args.InRecycleQueue)
            {
                content.Source = null;
                return;
            }

            var sticker = args.Item as ViewModels.Dialogs.StickerViewModel;

            if (sticker == null || sticker.Thumbnail == null)
            {
                content.Source = null;
                return;
            }

            //if (args.Phase < 2)
            //{
            //    content.Source = null;
            //    args.RegisterUpdateCallback(Stickers_ContainerContentChanging);
            //}
            //else
            if (args.Phase == 0)
            {
                Debug.WriteLine("Loading sticker " + sticker.StickerValue.Id + " for sticker set id " + sticker.SetId);

                var file = sticker.Thumbnail.Photo;
                if (file.Local.IsDownloadingCompleted)
                {
                    content.Source = await PlaceholderHelper.GetWebpAsync(file.Local.Path);
                }
                else if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive)
                {
                    DownloadFile(file.Id, sticker);
                }
            }
            else
            {
                throw new System.Exception("We should be in phase 0, but we are not.");
            }

            args.Handled = true;
        }

        private void Stickers_ViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
        {
            if (e.IsIntermediate)
            {
                return;
            }

            LoadVisibleStickers();
        }

        private async void LoadVisibleStickers()
        {
            var scrollingHost = Stickers.ItemsPanelRoot as ItemsWrapGrid;
            if (scrollingHost == null)
            {
                return;
            }

            if (scrollingHost.FirstVisibleIndex < 0)
            {
                return;
            }

            var lastSet = 0L;

            for (int i = scrollingHost.FirstVisibleIndex; i <= scrollingHost.LastVisibleIndex; i++)
            {
                if (i >= Stickers.Items.Count)
                {
                    return;
                }

                var first = Stickers.Items[i] as ViewModels.Dialogs.StickerViewModel;
                if (first == null || first.SetId == lastSet)
                {
                    continue;
                }

                lastSet = first.SetId;

                var fromItem = Stickers.ContainerFromItem(first);
                if (fromItem == null)
                {
                    continue;
                }

                var header = Stickers.GroupHeaderContainerFromItemContainer(fromItem) as GridViewHeaderItem;
                if (header == null)
                {
                    continue;
                }

                var group = header.Content as ViewModels.Dialogs.StickerSetViewModel;
                if (group == null || group.IsLoaded)
                {
                    continue;
                }

                group.IsLoaded = true;

                Debug.WriteLine("Loading sticker set " + group.Id);

                var response = await ViewModel.ProtoService.SendAsync(new GetStickerSet(group.Id));
                if (response is StickerSet full)
                {
                    group.Update(full);
                    //group.Stickers.RaiseCollectionChanged(new System.Collections.Specialized.NotifyCollectionChangedEventArgs(System.Collections.Specialized.NotifyCollectionChangedAction.Reset));

                    int j = 0;
                    foreach (var sticker in group.Stickers)
                    {
                        if (sticker.Thumbnail == null)
                        {
                            continue;
                        }

                        //group.Stickers.RaiseCollectionChanged(new System.Collections.Specialized.NotifyCollectionChangedEventArgs(System.Collections.Specialized.NotifyCollectionChangedAction.Move, sticker, j, j));
                        //j++;

                        var container = Stickers.ContainerFromItem(sticker) as SelectorItem;
                        if (container == null)
                        {
                            continue;
                        }

                        var content = container.ContentTemplateRoot as Image;

                        var file = sticker.Thumbnail.Photo;
                        if (file.Local.IsDownloadingCompleted)
                        {
                            content.Source = await PlaceholderHelper.GetWebpAsync(file.Local.Path);
                        }
                        else if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive)
                        {
                            DownloadFile(file.Id, sticker);
                        }
                    }
                }
            }
        }

        private void DownloadFile(int id, ViewModels.Dialogs.StickerViewModel sticker)
        {
            _stickers[id].Add(sticker);
            ViewModel.ProtoService.Send(new DownloadFile(id, 1));
        }

        private void DownloadFile(int id, Animation animation)
        {
            _animations[id][animation.AnimationValue.Id] = animation;
            ViewModel.ProtoService.Send(new DownloadFile(id, 1));
        }

        private void Animations_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }

            var content = args.ItemContainer.ContentTemplateRoot as MosaicRow;
            var position = args.Item as MosaicMediaRow;

            content.UpdateLine(ViewModel.ProtoService, position, Mosaic_Click);

            //var content = args.ItemContainer.ContentTemplateRoot as Border;
            //var position = args.Item as MosaicMediaPosition;

            //var animation = position.Item as Animation;
            //if (animation == null)
            //{
            //    return;
            //}

            //if (args.Phase < 2)
            //{
            //    args.RegisterUpdateCallback(Animations_ContainerContentChanging);
            //}
            //else
            //{
            //    var file = animation.Thumbnail.Photo;
            //    if (file.Local.IsDownloadingCompleted)
            //    {
            //        content.Background = new ImageBrush { ImageSource = new BitmapImage(new Uri("file:///" + file.Local.Path)), AlignmentX = AlignmentX.Center, AlignmentY = AlignmentY.Center, Stretch = Stretch.UniformToFill };
            //    }
            //    else if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive)
            //    {
            //        DownloadFile(file.Id, animation);
            //    }
            //}

            args.Handled = true;
        }

        private async void Toolbar_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }

            var content = args.ItemContainer.ContentTemplateRoot as Image;
            var sticker = args.Item as ViewModels.Dialogs.StickerSetViewModel;

            if (content == null || sticker == null || sticker.Covers == null)
            {
                return;
            }

            var cover = sticker.Covers.FirstOrDefault();
            if (cover == null || cover.Thumbnail == null)
            {
                content.Source = null;
                return;
            }

            var file = cover.Thumbnail.Photo;
            if (file.Local.IsDownloadingCompleted)
            {
                content.Source = await PlaceholderHelper.GetWebpAsync(file.Local.Path);
            }
            else if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive)
            {
                //DownloadFile(file.Id, cover);
                ViewModel.ProtoService.Send(new DownloadFile(file.Id, 1));
            }
        }
    }
}
