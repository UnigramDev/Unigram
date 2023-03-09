//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Converters;
using Telegram.Navigation;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Navigation;

namespace Telegram.Views.Popups
{
    public sealed partial class StickersPopup : ContentPopup
    {
        public StickersViewModel ViewModel => DataContext as StickersViewModel;

        private readonly Dictionary<string, DataTemplate> _typeToTemplateMapping = new Dictionary<string, DataTemplate>();
        private readonly Dictionary<string, HashSet<SelectorItem>> _typeToItemHashSetMapping = new Dictionary<string, HashSet<SelectorItem>>();

        private readonly AnimatedListHandler _handler;
        private readonly ZoomableListHandler _zoomer;

        private StickersPopup()
        {
            InitializeComponent();
            DataContext = TLContainer.Current.Resolve<StickersViewModel>();

            _handler = new AnimatedListHandler(List);

            _zoomer = new ZoomableListHandler(List);
            _zoomer.Opening = _handler.UnloadVisibleItems;
            _zoomer.Closing = _handler.ThrottleVisibleItems;
            _zoomer.DownloadFile = fileId => ViewModel.ClientService.DownloadFile(fileId, 32);
            _zoomer.SessionId = () => ViewModel.ClientService.SessionId;

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

        public Action<Sticker> ItemClick { get; set; }

        public static Task<ContentDialogResult> ShowAsync(StickerSet parameter)
        {
            return ShowAsyncInternal(parameter, null);
        }

        public static Task<ContentDialogResult> ShowAsync(StickerSet parameter, Action<Sticker> callback)
        {
            return ShowAsyncInternal(parameter, callback);
        }

        public static Task<ContentDialogResult> ShowAsync(HashSet<long> parameter)
        {
            return ShowAsyncInternal(parameter, null);
        }

        public static Task<ContentDialogResult> ShowAsync(HashSet<long> parameter, Action<Sticker> callback)
        {
            return ShowAsyncInternal(parameter, callback);
        }

        public static Task<ContentDialogResult> ShowAsync(long parameter)
        {
            return ShowAsyncInternal(parameter, null);
        }

        public static Task<ContentDialogResult> ShowAsync(long parameter, Action<Sticker> callback)
        {
            return ShowAsyncInternal(parameter, callback);
        }

        public static Task<ContentDialogResult> ShowAsync(InputFileId parameter)
        {
            return ShowAsyncInternal(parameter, null);
        }

        public static Task<ContentDialogResult> ShowAsync(InputFileId parameter, Action<Sticker> callback)
        {
            return ShowAsyncInternal(parameter, callback);
        }

        private static Task<ContentDialogResult> ShowAsyncInternal(object parameter, Action<Sticker> callback)
        {
            var popup = new StickersPopup();

            popup.ViewModel.IsLoading = true;
            popup.ViewModel.Items.Clear();

            RoutedEventHandler handler = null;
            handler = new RoutedEventHandler(async (s, args) =>
            {
                popup.Loaded -= handler;
                popup.ItemClick = callback;
                await popup.ViewModel.NavigatedToAsync(parameter, NavigationMode.New, null);
            });

            popup.Loaded += handler;
            return popup.ShowQueuedAsync();
        }

        public static Task<ContentDialogResult> ShowAsync(string parameter)
        {
            return ShowAsyncInternal(parameter, null);
        }

        public static Task<ContentDialogResult> ShowAsync(string parameter, Action<Sticker> callback)
        {
            return ShowAsyncInternal(parameter, callback);
        }

        #endregion

        #region Recycle

        private void OnChoosingItemContainer(ListViewBase sender, ChoosingItemContainerEventArgs args)
        {
            var typeName = args.Item is ViewModels.Drawers.StickerViewModel sticker ? sticker.Format switch
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
            var sticker = args.Item as ViewModels.Drawers.StickerViewModel;

            if (content.Children.Count > 1 && content.Children[1] is Border panel && panel.Child is TextBlock premium)
            {
                if (sticker.FullType is StickerFullTypeRegular regular && regular.PremiumAnimation != null && ViewModel.ClientService.IsPremiumAvailable)
                {
                    premium.Text = ViewModel.ClientService.IsPremium ? Icons.Premium16 : Icons.LockClosed16;
                    panel.HorizontalAlignment = ViewModel.ClientService.IsPremium ? HorizontalAlignment.Right : HorizontalAlignment.Center;
                    panel.Visibility = Visibility.Visible;
                }
                else
                {
                    panel.Visibility = Visibility.Collapsed;
                }
            }

            var file = sticker.StickerValue;
            if (file.Local.IsDownloadingCompleted)
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

                UpdateManager.Subscribe(content, ViewModel.ClientService, file, UpdateFile, true);

                if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive)
                {
                    ViewModel.ClientService.DownloadFile(file.Id, 1);
                }
            }

            args.Handled = true;
        }

        #endregion

        #region Binding

        private int ConvertItemsPerRow(StickerType type)
        {
            return type is StickerTypeCustomEmoji ? 8 : 5;
        }

        private string ConvertIsInstalled(bool installed, bool archived, bool official, StickerType type)
        {
            if (ViewModel == null || ViewModel.IsLoading)
            {
                return string.Empty;
            }

            var masks = type is StickerTypeMask;

            if (installed && !archived)
            {
                return official
                    ? string.Format(masks ? Strings.Resources.StickersRemove : Strings.Resources.StickersRemove, ViewModel.Count)
                    : string.Format(masks ? Strings.Resources.StickersRemove : Strings.Resources.StickersRemove, ViewModel.Count);
            }

            return official || archived
                ? string.Format(masks ? Strings.Resources.AddMasks : Strings.Resources.AddStickers, ViewModel.Count)
                : string.Format(masks ? Strings.Resources.AddMasks : Strings.Resources.AddStickers, ViewModel.Count);
        }

        private Style ConvertIsInstalledStyle(bool installed, bool archived)
        {
            if (ViewModel == null || ViewModel.IsLoading)
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
            var builder = new StringBuilder();

            foreach (var item in ViewModel.Items)
            {
                if (builder.Length > 0)
                {
                    builder.AppendLine();
                }

                builder.Append(MeUrlPrefixConverter.Convert(ViewModel.ClientService, $"addstickers/{item.Name}"));
            }

            Hide();

            var text = builder.ToString();
            var formatted = new FormattedText(text, new TextEntity[0]);

            await SharePopup.GetForCurrentView().ShowAsync(formatted);
        }

        private void List_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (ItemClick != null && e.ClickedItem is ViewModels.Drawers.StickerViewModel sticker)
            {
                ItemClick(sticker);
                Hide();
            }
        }
    }
}
