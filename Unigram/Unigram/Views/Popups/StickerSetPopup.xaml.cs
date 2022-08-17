using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Converters;
using Unigram.Navigation;
using Unigram.ViewModels;
using Windows.UI.Composition;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Navigation;

namespace Unigram.Views.Popups
{
    public sealed partial class StickerSetPopup : ContentPopup
    {
        public StickerSetViewModel ViewModel => DataContext as StickerSetViewModel;

        private readonly Dictionary<string, DataTemplate> _typeToTemplateMapping = new Dictionary<string, DataTemplate>();
        private readonly Dictionary<string, HashSet<SelectorItem>> _typeToItemHashSetMapping = new Dictionary<string, HashSet<SelectorItem>>();

        private readonly AnimatedListHandler _handler;
        private readonly ZoomableListHandler _zoomer;

        private StickerSetPopup()
        {
            InitializeComponent();
            DataContext = TLContainer.Current.Resolve<StickerSetViewModel>();

            _handler = new AnimatedListHandler(List);

            _zoomer = new ZoomableListHandler(List);
            _zoomer.Opening = _handler.UnloadVisibleItems;
            _zoomer.Closing = _handler.ThrottleVisibleItems;
            _zoomer.DownloadFile = fileId => ViewModel.ProtoService.DownloadFile(fileId, 32);
            _zoomer.SessionId = () => ViewModel.ProtoService.SessionId;

            _typeToItemHashSetMapping.Add("AnimatedItemTemplate", new HashSet<SelectorItem>());
            _typeToItemHashSetMapping.Add("VideoItemTemplate", new HashSet<SelectorItem>());
            _typeToItemHashSetMapping.Add("ItemTemplate", new HashSet<SelectorItem>());

            _typeToTemplateMapping.Add("AnimatedItemTemplate", Resources["AnimatedItemTemplate"] as DataTemplate);
            _typeToTemplateMapping.Add("VideoItemTemplate", Resources["VideoItemTemplate"] as DataTemplate);
            _typeToTemplateMapping.Add("ItemTemplate", Resources["ItemTemplate"] as DataTemplate);

            SecondaryButtonText = Strings.Resources.Close;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
        }

        private void OnClosing(ContentDialog sender, ContentDialogClosingEventArgs args)
        {
            _handler.UnloadItems();
            _zoomer.Release();
        }

        #region Show

        private static readonly Dictionary<int, WeakReference<StickerSetPopup>> _windowContext = new Dictionary<int, WeakReference<StickerSetPopup>>();
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
                await ViewModel.NavigatedToAsync(parameter, NavigationMode.New, null);
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
                await ViewModel.NavigatedToAsync(parameter, NavigationMode.New, null);
            });

            Loaded += handler;
            return this.ShowQueuedAsync();
        }

        #endregion

        #region Recycle

        private void OnChoosingItemContainer(ListViewBase sender, ChoosingItemContainerEventArgs args)
        {
            var typeName = args.Item is Sticker sticker ? sticker.Format switch
            {
                StickerFormatTgs => "AnimatedItemTemplate",
                StickerFormatWebm => "VideoItemTemplate",
                _ => "ItemTemplate"
            } : "ItemTemplate";
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
                    args.ItemContainer = item;

                    _zoomer.ElementPrepared(args.ItemContainer);
                }
            }

            // Indicate to XAML that we picked a container for it
            args.IsContainerPrepared = true;
        }

        private async void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                var tag = args.ItemContainer.Tag as string;
                var added = _typeToItemHashSetMapping[tag].Add(args.ItemContainer);
                return;
            }

            var content = args.ItemContainer.ContentTemplateRoot as Grid;
            var sticker = args.Item as Sticker;

            if (content.Children.Count > 1 && content.Children[1] is Border panel && panel.Child is TextBlock premium)
            {
                if (sticker.PremiumAnimation != null && ViewModel.CacheService.IsPremiumAvailable)
                {
                    premium.Text = ViewModel.CacheService.IsPremium ? Icons.Premium16 : Icons.LockClosed16;
                    panel.HorizontalAlignment = ViewModel.CacheService.IsPremium ? HorizontalAlignment.Right : HorizontalAlignment.Center;
                    panel.Visibility = Visibility.Visible;
                }
                else
                {
                    panel.Visibility = Visibility.Collapsed;
                }
            }

            var file = sticker.StickerValue;
            if (file.Local.IsFileExisting())
            {
                if (content.Children[0] is Border border && border.Child is Image photo)
                {
                    photo.Source = await PlaceholderHelper.GetWebPFrameAsync(file.Local.Path, 60);
                    ElementCompositionPreview.SetElementChildVisual(content.Children[0], null);
                }
                else if (args.Phase == 0 && content.Children[0] is LottieView lottie)
                {
                    lottie.Source = UriEx.ToLocal(file.Local.Path);
                }
                else if (args.Phase == 0 && content.Children[0] is AnimationView video)
                {
                    video.Source = new LocalVideoSource(file);
                }

                UpdateManager.Unsubscribe(content);
            }
            else
            {
                if (content.Children[0] is Border border && border.Child is Image photo)
                {
                    photo.Source = null;
                }
                else if (args.Phase == 0 && content.Children[0] is LottieView lottie)
                {
                    lottie.Source = null;
                }
                else if (args.Phase == 0 && content.Children[0] is AnimationView video)
                {
                    video.Source = null;
                }

                CompositionPathParser.ParseThumbnail(sticker, out ShapeVisual visual, false);
                ElementCompositionPreview.SetElementChildVisual(content.Children[0], visual);

                UpdateManager.Subscribe(content, ViewModel.ProtoService, file, UpdateFile, true);

                if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive)
                {
                    ViewModel.ProtoService.DownloadFile(file.Id, 1);
                }
            }

            args.Handled = true;
        }

        #endregion

        #region Binding

        private int ConvertItemsPerRow(StickerType type)
        {
            return type is StickerTypeCustomEmoji ? 7 : 5;
        }

        private string ConvertIsInstalled(bool installed, bool archived, bool official, StickerType type)
        {
            if (ViewModel == null || ViewModel.StickerSet == null || ViewModel.StickerSet.Stickers == null)
            {
                return string.Empty;
            }

            var masks = type is StickerTypeMask;

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

        private Style ConvertIsInstalledStyle(bool installed, bool archived, bool official)
        {
            if (ViewModel == null || ViewModel.StickerSet == null || ViewModel.StickerSet.Stickers == null)
            {
                return BootStrapper.Current.Resources["AccentButtonStyle"] as Style;
            }

            if (installed && !archived)
            {
                return BootStrapper.Current.Resources["DangerButtonStyle"] as Style;
            }

            return BootStrapper.Current.Resources["AccentButtonStyle"] as Style;
        }

        #endregion

        #region Handle

        private async void UpdateFile(object target, File file)
        {
            var content = target as Grid;
            if (content == null)
            {
                return;
            }

            if (content.Children[0] is Border border && border.Child is Image photo)
            {
                photo.Source = await PlaceholderHelper.GetWebPFrameAsync(file.Local.Path, 60);
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
