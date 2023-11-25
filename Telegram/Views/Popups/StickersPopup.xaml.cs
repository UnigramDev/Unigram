//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Converters;
using Telegram.Navigation;
using Telegram.Streams;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Telegram.Views.Popups
{
    public sealed partial class StickersPopup : ContentPopup
    {
        public StickersViewModel ViewModel => DataContext as StickersViewModel;

        private readonly AnimatedListHandler _handler;
        private readonly ZoomableListHandler _zoomer;

        private StickersPopup()
        {
            InitializeComponent();
            DataContext = TypeResolver.Current.Resolve<StickersViewModel>();

            // TODO: this might need to change depending on context
            _handler = new AnimatedListHandler(ScrollingHost, AnimatedListType.Stickers);

            _zoomer = new ZoomableListHandler(ScrollingHost);
            _zoomer.Opening = _handler.UnloadVisibleItems;
            _zoomer.Closing = _handler.ThrottleVisibleItems;
            _zoomer.DownloadFile = fileId => ViewModel.ClientService.DownloadFile(fileId, 32);
            _zoomer.SessionId = () => ViewModel.ClientService.SessionId;

            SecondaryButtonText = Strings.Close;
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
            if (args.ItemContainer == null)
            {
                var item = new GridViewItem();
                item.ContentTemplate = sender.ItemTemplate;
                item.Style = sender.ItemContainerStyle;
                args.ItemContainer = item;

                _zoomer.ElementPrepared(args.ItemContainer);
            }

            args.IsContainerPrepared = true;
        }

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            var content = args.ItemContainer.ContentTemplateRoot as Grid;
            var sticker = args.Item as ViewModels.Drawers.StickerViewModel;

            var file = sticker.StickerValue;
            if (file == null)
            {
                return;
            }

            var animated = content.Children[0] as AnimatedImage;
            using (animated.BeginBatchUpdate())
            {
                if (sticker.FullType is StickerFullTypeCustomEmoji)
                {
                    animated.FrameSize = new Size(40, 40);
                }
                else
                {
                    animated.FrameSize = new Size(64, 64);
                }

                animated.Source = new DelayedFileSource(ViewModel.ClientService, sticker);
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
                    ? string.Format(masks ? Strings.StickersRemove : Strings.StickersRemove, ViewModel.Count)
                    : string.Format(masks ? Strings.StickersRemove : Strings.StickersRemove, ViewModel.Count);
            }

            return official || archived
                ? string.Format(masks ? Strings.AddMasks : Strings.AddStickers, ViewModel.Count)
                : string.Format(masks ? Strings.AddMasks : Strings.AddStickers, ViewModel.Count);
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
            var formatted = new FormattedText(text, Array.Empty<TextEntity>());

            // TODO: currently not used
            //await new ChooseChatsPopup().ShowAsync(formatted);
        }

        private void List_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (ItemClick != null && e.ClickedItem is ViewModels.Drawers.StickerViewModel sticker)
            {
                ItemClick(sticker);
                Hide();
            }
        }

        private void Player_Ready(object sender, EventArgs e)
        {
            _handler.ThrottleVisibleItems();
        }
    }
}
