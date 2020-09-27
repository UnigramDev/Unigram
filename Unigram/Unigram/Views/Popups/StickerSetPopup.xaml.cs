using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Converters;
using Unigram.Services;
using Unigram.ViewModels;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Navigation;

namespace Unigram.Views.Popups
{
    public sealed partial class StickerSetPopup : ContentPopup, IHandle<UpdateFile>
    {
        public StickerSetViewModel ViewModel => DataContext as StickerSetViewModel;

        private AnimatedListHandler<Sticker> _handler;
        private ZoomableListHandler _zoomer;

        private StickerSetPopup()
        {
            InitializeComponent();
            DataContext = TLContainer.Current.Resolve<StickerSetViewModel>();

            _handler = new AnimatedListHandler<Sticker>(List);
            _handler.DownloadFile = (id, sticker) =>
            {
                ViewModel.ProtoService.DownloadFile(id, 1);
            };

            _zoomer = new ZoomableListHandler(List);
            _zoomer.Opening = _handler.UnloadVisibleItems;
            _zoomer.Closing = _handler.ThrottleVisibleItems;
            _zoomer.DownloadFile = fileId => ViewModel.ProtoService.DownloadFile(fileId, 32);

            SecondaryButtonText = Strings.Resources.Close;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            ViewModel.Aggregator.Subscribe(this);
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            ViewModel.Aggregator.Unsubscribe(this);
        }

        private void OnClosing(ContentDialog sender, ContentDialogClosingEventArgs args)
        {
            _handler.UnloadVisibleItems();
            _handler.DownloadFile = null;

            _zoomer.Opening = null;
            _zoomer.Closing = null;
            _zoomer.DownloadFile = null;
        }

        #region Show

        private static Dictionary<int, WeakReference<StickerSetPopup>> _windowContext = new Dictionary<int, WeakReference<StickerSetPopup>>();
        public static StickerSetPopup GetForCurrentView()
        {
            return new StickerSetPopup();

            var id = ApplicationView.GetApplicationViewIdForWindow(Window.Current.CoreWindow);
            if (_windowContext.TryGetValue(id, out WeakReference<StickerSetPopup> reference) && reference.TryGetTarget(out StickerSetPopup value))
            {
                return value;
            }

            var context = new StickerSetPopup();
            _windowContext[id] = new WeakReference<StickerSetPopup>(context);

            return context;
        }

        public ItemClickEventHandler ItemClick { get; set; }

        public Task<ContentDialogResult> ShowAsync(StickerSet parameter)
        {
            return ShowAsync(parameter, null);
        }

        public Task<ContentDialogResult> ShowAsync(StickerSet parameter, ItemClickEventHandler callback)
        {
            return ShowAsync(parameter.Id, callback);
        }

        public Task<ContentDialogResult> ShowAsync(long parameter)
        {
            return ShowAsync(parameter, null);
        }

        public Task<ContentDialogResult> ShowAsync(long parameter, ItemClickEventHandler callback)
        {
            ViewModel.IsLoading = true;
            ViewModel.StickerSet = new StickerSet();
            ViewModel.Items.Clear();

            RoutedEventHandler handler = null;
            handler = new RoutedEventHandler(async (s, args) =>
            {
                Loaded -= handler;
                ItemClick = callback;
                await ViewModel.OnNavigatedToAsync(parameter, NavigationMode.New, null);
            });

            Loaded += handler;
            return this.ShowQueuedAsync();
        }

        public Task<ContentDialogResult> ShowAsync(string parameter)
        {
            return ShowAsync(parameter, null);
        }

        public Task<ContentDialogResult> ShowAsync(string parameter, ItemClickEventHandler callback)
        {
            ViewModel.IsLoading = true;
            ViewModel.StickerSet = new StickerSet();
            ViewModel.Items.Clear();

            RoutedEventHandler handler = null;
            handler = new RoutedEventHandler(async (s, args) =>
            {
                Loaded -= handler;
                ItemClick = callback;
                await ViewModel.OnNavigatedToAsync(parameter, NavigationMode.New, null);
            });

            Loaded += handler;
            return this.ShowQueuedAsync();
        }

        #endregion

        #region Recycle

        private void OnChoosingItemContainer(ListViewBase sender, ChoosingItemContainerEventArgs args)
        {
            if (args.ItemContainer == null)
            {
                args.ItemContainer = new GridViewItem();
                args.ItemContainer.ContentTemplate = sender.ItemTemplate;
                args.ItemContainer.Style = sender.ItemContainerStyle;

                _zoomer.ElementPrepared(args.ItemContainer);
            }

            args.IsContainerPrepared = true;
        }

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }

            var content = args.ItemContainer.ContentTemplateRoot as Grid;
            var sticker = args.Item as Sticker;

            if (args.Phase == 0)
            {
                var title = content.Children[1] as TextBlock;
                title.Text = sticker.Emoji;
            }
            else if (args.Phase == 1)
            {
            }
            else if (args.Phase == 2)
            {
                var photo = content.Children[0] as Image;

                if (sticker == null || sticker.Thumbnail == null)
                {
                    return;
                }

                var file = sticker.Thumbnail.File;
                if (file.Local.IsDownloadingCompleted)
                {
                    photo.Source = PlaceholderHelper.GetWebPFrame(file.Local.Path);
                }
                else if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive)
                {
                    photo.Source = null;
                    ViewModel.ProtoService.DownloadFile(file.Id, 1);
                }
            }

            if (args.Phase < 2)
            {
                args.RegisterUpdateCallback(OnContainerContentChanging);
            }

            args.Handled = true;
        }

        #endregion

        #region Binding

        private string ConvertIsInstalled(bool installed, bool archived, bool official, bool masks)
        {
            if (ViewModel == null || ViewModel.StickerSet == null || ViewModel.StickerSet.Stickers == null)
            {
                return string.Empty;
            }

            if (installed && !archived)
            {
                return official
                    ? string.Format(masks ? Strings.Resources.StickersRemove : Strings.Resources.StickersRemove, ViewModel.StickerSet.Stickers.Count)
                    : string.Format(masks ? Strings.Resources.StickersRemove : Strings.Resources.StickersRemove, ViewModel.StickerSet.Stickers.Count);
            }

            return official || archived
                ? string.Format(masks ? Strings.Resources.AddMasks : Strings.Resources.AddStickers, ViewModel.StickerSet.Stickers.Count)
                : string.Format(masks ? Strings.Resources.AddMasks : Strings.Resources.AddStickers, ViewModel.StickerSet.Stickers.Count);
        }

        #endregion

        #region Handle

        public void Handle(UpdateFile update)
        {
            if (!update.File.Local.IsDownloadingCompleted)
            {
                return;
            }

            this.BeginOnUIThread(() => UpdateFile(update.File));
        }

        public void UpdateFile(File file)
        {
            foreach (Sticker sticker in List.Items)
            {
                if (sticker.UpdateFile(file) && file.Local.IsDownloadingCompleted)
                {
                    if (file.Id == sticker.Thumbnail?.File.Id)
                    {
                        var container = List.ContainerFromItem(sticker) as SelectorItem;
                        if (container == null)
                        {
                            continue;
                        }

                        var content = container.ContentTemplateRoot as Grid;
                        if (content == null)
                        {
                            continue;
                        }

                        var photo = content.Children[0] as Image;
                        photo.Source = PlaceholderHelper.GetWebPFrame(file.Local.Path);
                    }
                    else if (file.Id == sticker.StickerValue.Id)
                    {
                        _handler.ThrottleVisibleItems();
                    }
                }
            }

            _zoomer.UpdateFile(file);
        }

        #endregion

        private async void Share_Click(object sender, RoutedEventArgs e)
        {
            var stickerSet = ViewModel.StickerSet;
            if (stickerSet == null)
            {
                return;
            }

            var title = stickerSet.Title;
            var link = new Uri(MeUrlPrefixConverter.Convert(ViewModel.ProtoService, $"addstickers/{stickerSet.Name}"));

            Hide();
            await SharePopup.GetForCurrentView().ShowAsync(link, title);
        }

        private void List_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (ItemClick != null)
            {
                ItemClick.Invoke(this, e);
                Hide();
            }
        }
    }
}
