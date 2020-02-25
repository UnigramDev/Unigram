using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Unigram.Views;
using Unigram.ViewModels;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using LinqToVisualTree;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Composition;
using System.Diagnostics;
using Windows.UI.ViewManagement;
using Windows.Foundation.Metadata;
using Windows.UI;
using Template10.Utils;
using Unigram.Converters;
using Telegram.Td.Api;
using Windows.Storage;
using Unigram.Native;
using Unigram.Common;
using Unigram.Services;
using Unigram.ViewModels.Delegates;
using System.Threading.Tasks;

namespace Unigram.Controls.Views
{
    public sealed partial class StickerSetView : TLContentDialog, IHandle<UpdateFile>
    {
        public StickerSetViewModel ViewModel => DataContext as StickerSetViewModel;

        private StickerSetView()
        {
            InitializeComponent();
            DataContext = TLContainer.Current.Resolve<StickerSetViewModel>();

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

        #region Show

        private static Dictionary<int, WeakReference<StickerSetView>> _windowContext = new Dictionary<int, WeakReference<StickerSetView>>();
        public static StickerSetView GetForCurrentView()
        {
            return new StickerSetView();

            var id = ApplicationView.GetApplicationViewIdForWindow(Window.Current.CoreWindow);
            if (_windowContext.TryGetValue(id, out WeakReference<StickerSetView> reference) && reference.TryGetTarget(out StickerSetView value))
            {
                return value;
            }

            var context = new StickerSetView();
            _windowContext[id] = new WeakReference<StickerSetView>(context);

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

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }

            var content = args.ItemContainer.ContentTemplateRoot as Grid;
            var sticker = args.Item as Sticker;

            content.Tag = args.ItemContainer.Tag = new ViewModels.Dialogs.StickerViewModel(ViewModel.ProtoService, ViewModel.Aggregator, sticker);

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

                var file = sticker.Thumbnail.Photo;
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
                if (sticker.UpdateFile(file) && file.Id == sticker.Thumbnail?.Photo.Id)
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
            }
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
            await ShareView.GetForCurrentView().ShowAsync(link, title);
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
