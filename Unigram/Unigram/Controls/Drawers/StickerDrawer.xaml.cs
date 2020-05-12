using LinqToVisualTree;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Converters;
using Unigram.Services;
using Unigram.ViewModels;
using Unigram.ViewModels.Delegates;
using Unigram.Views;
using Unigram.Views.Settings;
using Windows.Foundation.Metadata;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using StickerViewModel = Unigram.ViewModels.Dialogs.StickerViewModel;
using StickerSetViewModel = Unigram.ViewModels.Dialogs.StickerSetViewModel;
using Windows.UI.Xaml.Hosting;

namespace Unigram.Controls.Drawers
{
    public sealed partial class StickerDrawer : UserControl, IDrawer, IFileDelegate
    {
        public DialogViewModel ViewModel => DataContext as DialogViewModel;

        public Action<Sticker> StickerClick { get; set; }

        private readonly AnimatedListHandler<StickerViewModel> _handler;
        private readonly DispatcherTimer _throttler;

        private readonly AnimatedListHandler<StickerSetViewModel> _toolbarHandler;

        private FileContext<StickerViewModel> _stickers = new FileContext<StickerViewModel>();
        private bool _isActive;

        public StickerDrawer()
        {
            InitializeComponent();

            ElementCompositionPreview.GetElementVisual(this).Clip = Window.Current.Compositor.CreateInsetClip();

            _handler = new AnimatedListHandler<StickerViewModel>(Stickers);
            _handler.DownloadFile = DownloadFile;

            _throttler = new DispatcherTimer();
            _throttler.Interval = TimeSpan.FromMilliseconds(Constants.TypingTimeout);
            _throttler.Tick += (s, args) =>
            {
                _handler.LoadVisibleItems(false);
            };

            //_toolbarHandler = new AnimatedStickerHandler<StickerSetViewModel>(Toolbar);

            var shadow = DropShadowEx.Attach(Separator, 20, 0.25f);
            Separator.SizeChanged += (s, args) =>
            {
                shadow.Size = args.NewSize.ToVector2();
            };

            var observable = Observable.FromEventPattern<TextChangedEventArgs>(FieldStickers, "TextChanged");
            var throttled = observable.Throttle(TimeSpan.FromMilliseconds(Constants.TypingTimeout)).ObserveOnDispatcher().Subscribe(async x =>
            {
                var items = ViewModel.Stickers.SearchStickers;
                if (items != null && string.Equals(FieldStickers.Text, items.Query))
                {
                    await items.LoadMoreItemsAsync(1);
                    await items.LoadMoreItemsAsync(2);
                }
            });
        }

        public Services.Settings.StickersTab Tab => Services.Settings.StickersTab.Stickers;

        public void Activate()
        {
            _isActive = true;
            _handler.LoadVisibleItems(false);

            ScrollingHost_ViewChanged(null, null);
        }

        public void Deactivate()
        {
            _isActive = false;
            _handler.UnloadVisibleItems();
        }

        public void SetView(StickersPanelMode mode)
        {
        }

        private void OnDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
        {
            if (ViewModel != null) Bindings.Update();
            if (ViewModel == null) Bindings.StopTracking();
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            Bindings.StopTracking();
        }

        public void UpdateFile(File file)
        {
            if (_stickers.TryGetValue(file.Id, out List<StickerViewModel> items) && items.Count > 0)
            {
                foreach (var item in items)
                {
                    item.UpdateFile(file);

                    if (item.Thumbnail?.Photo.Id == file.Id)
                    {
                        var container = Stickers.ContainerFromItem(item) as SelectorItem;
                        if (container == null)
                        {
                            continue;
                        }

                        var content = container.ContentTemplateRoot as Grid;
                        var photo = content.Children[0] as Image;
                     }
                    else if (item.StickerValue.Id == file.Id)
                    {
                        _throttler.Stop();
                        _throttler.Start();
                    }
                }
            }

            foreach (StickerSetViewModel stickerSet in Toolbar.Items)
            {
                if (stickerSet.Thumbnail == null && stickerSet.Covers == null)
                {
                    continue;
                }

                var cover = stickerSet.Thumbnail ?? stickerSet.Covers.FirstOrDefault()?.Thumbnail;
                if (cover == null)
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
                    if (stickerSet.IsAnimated)
                    {
                        content.Source = PlaceholderHelper.GetLottieFrame(file.Local.Path, 0, 36, 36);
                    }
                    else
                    {
                        content.Source = PlaceholderHelper.GetWebPFrame(file.Local.Path);
                    }
                }
            }

            //foreach (StickerViewModel item in Stickers.Items)
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

        private void Stickers_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is StickerViewModel sticker && sticker.StickerValue != null)
            {
                StickerClick?.Invoke(sticker.Get());
            }
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

        private void Toolbar_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is StickerSetViewModel set && set.Stickers != null)
            {
                Stickers.ScrollIntoView(e.ClickedItem, ScrollIntoViewAlignment.Leading);
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
            var scrollingHost = Stickers.ItemsPanelRoot as ItemsWrapGrid;
            if (scrollingHost != null && _isActive)
            {
                var first = Stickers.ContainerFromIndex(scrollingHost.FirstVisibleIndex);
                if (first != null)
                {
                    var header = Stickers.GroupHeaderContainerFromItemContainer(first) as GridViewHeaderItem;
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

        private async void OnChoosingGroupHeaderContainer(ListViewBase sender, ChoosingGroupHeaderContainerEventArgs args)
        {
            if (args.GroupHeaderContainer == null)
            {
                args.GroupHeaderContainer = new GridViewHeaderItem();
                args.GroupHeaderContainer.Style = Stickers.GroupStyle[0].HeaderContainerStyle;
                args.GroupHeaderContainer.ContentTemplate = Stickers.GroupStyle[0].HeaderTemplate;
            }

            if (args.Group is StickerSetViewModel group && !group.IsLoaded)
            {
                group.IsLoaded = true;

                //Debug.WriteLine("Loading sticker set " + group.Id);

                var response = await ViewModel.ProtoService.SendAsync(new GetStickerSet(group.Id));
                if (response is StickerSet full)
                {
                    group.Update(full, true);
                }
            }
        }

        private void OnChoosingItemContainer(ListViewBase sender, ChoosingItemContainerEventArgs args)
        {
            if (args.ItemContainer == null)
            {
                args.ItemContainer = new GridViewItem();
                args.ItemContainer.Style = Stickers.ItemContainerStyle;
                args.ItemContainer.ContentTemplate = Stickers.ItemTemplate;
                args.ItemContainer.ContextRequested += Sticker_ContextRequested;
            }

            args.IsContainerPrepared = true;
        }

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            var content = args.ItemContainer.ContentTemplateRoot as Grid;
            var photo = content.Children[0] as Image;

            if (args.InRecycleQueue)
            {
                while (content.Children.Count > 1)
                {
                    content.Children.RemoveAt(1);
                }

                photo.Opacity = 1;
                photo.Source = null;
                return;
            }

            var sticker = args.Item as StickerViewModel;

            args.ItemContainer.Tag = args.Item;
            args.ItemContainer.Content = args.Item;
            content.Tag = args.Item;

            if (sticker == null || sticker.Thumbnail == null)
            {
                while (content.Children.Count > 1)
                {
                    content.Children[0].Opacity = 1;
                    content.Children.RemoveAt(1);
                }

                photo.Source = null;
                return;
            }

            if (args.Phase < 2)
            {
                while (content.Children.Count > 1)
                {
                    content.Children.RemoveAt(1);
                }

                photo.Opacity = 1;
                photo.Source = null;
                args.RegisterUpdateCallback(OnContainerContentChanging);

                var file = sticker.Thumbnail.Photo;
                if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive)
                {
                    DownloadFile(file.Id, sticker);
                }
            }
            else if (args.Phase == 2)
            {
                //Debug.WriteLine("Loading sticker " + sticker.StickerValue.Id + " for sticker set id " + sticker.SetId);

                var file = sticker.Thumbnail.Photo;
                if (file.Local.IsDownloadingCompleted)
                {
                    photo.Source = PlaceholderHelper.GetWebPFrame(file.Local.Path);
                }
                else if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive)
                {
                    DownloadFile(file.Id, sticker);
                }
            }

            args.Handled = true;
        }

        private void DownloadFile(int id, StickerViewModel sticker)
        {
            _stickers[id].Add(sticker);
            ViewModel.ProtoService.DownloadFile(id, 1);
        }

        private void Toolbar_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }

            if (args.Item is ViewModels.Dialogs.SupergroupStickerSetViewModel supergroup)
            {
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

                var file = cover.Photo;
                if (file.Local.IsDownloadingCompleted)
                {
                    if (sticker.IsAnimated)
                    {
                        photo.Source = PlaceholderHelper.GetLottieFrame(file.Local.Path, 0, 36, 36);
                    }
                    else
                    {
                        photo.Source = PlaceholderHelper.GetWebPFrame(file.Local.Path);
                    }
                }
                else if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive)
                {
                    //DownloadFile(file.Id, cover);
                    ViewModel.ProtoService.DownloadFile(file.Id, 1);
                }
            }
        }

        private void FieldStickers_TextChanged(object sender, TextChangedEventArgs e)
        {
            ViewModel.Stickers.FindStickers(FieldStickers.Text);
        }

        private void Sticker_ContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
            var element = sender as FrameworkElement;
            var sticker = element.Tag as StickerViewModel;

            var flyout = new MenuFlyout();
            flyout.CreateFlyoutItem(ViewModel.StickerViewCommand, sticker.Get(), Strings.Resources.ViewPackPreview, new FontIcon { Glyph = Icons.Stickers });

            if (ViewModel.ProtoService.IsStickerFavorite(sticker.StickerValue.Id))
            {
                flyout.CreateFlyoutItem(ViewModel.StickerUnfaveCommand, sticker.Get(), Strings.Resources.DeleteFromFavorites, new FontIcon { Glyph = Icons.Unfavorite });
            }
            else
            {
                flyout.CreateFlyoutItem(ViewModel.StickerFaveCommand, sticker.Get(), Strings.Resources.AddToFavorites, new FontIcon { Glyph = Icons.Favorite });
            }

            if (!ViewModel.IsSchedule)
            {
                var chat = ViewModel.Chat;
                if (chat == null)
                {
                    return;
                }

                var self = ViewModel.CacheService.IsSavedMessages(chat);

                flyout.CreateFlyoutSeparator();
                flyout.CreateFlyoutItem(new RelayCommand<Sticker>(anim => ViewModel.StickerSendExecute(anim, null, true)), sticker.Get(), Strings.Resources.SendWithoutSound, new FontIcon { Glyph = Icons.Mute });
                //flyout.CreateFlyoutItem(new RelayCommand<Sticker>(anim => ViewModel.StickerSendExecute(anim, true, null)), sticker.Get(), self ? Strings.Resources.SetReminder : Strings.Resources.ScheduleMessage, new FontIcon { Glyph = Icons.Schedule });
            }

            args.ShowAt(flyout, element);
        }
    }
}
