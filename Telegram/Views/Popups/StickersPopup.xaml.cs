//
// Copyright Fela Ameghino 2015-2024
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
using Telegram.Controls.Media;
using Telegram.Converters;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Streams;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Navigation;

namespace Telegram.Views.Popups
{
    public sealed partial class StickersPopup : ContentPopup
    {
        public StickersViewModel ViewModel => DataContext as StickersViewModel;

        private readonly AnimatedListHandler _handler;
        private readonly ZoomableListHandler _zoomer;

        private StickersPopup(INavigationService navigationService)
        {
            InitializeComponent();
            DataContext = TypeResolver.Current.Resolve<StickersViewModel>(navigationService.SessionId);

            VerticalContentAlignment = VerticalAlignment.Center;

            ViewModel.NavigationService = navigationService;
            ViewModel.Dispatcher = navigationService.Dispatcher;
            ViewModel.PropertyChanged += OnPropertyChanged;

            // TODO: this might need to change depending on context
            _handler = new AnimatedListHandler(ScrollingHost, AnimatedListType.Stickers);

            _zoomer = new ZoomableListHandler(ScrollingHost);
            _zoomer.Opening = _handler.UnloadVisibleItems;
            _zoomer.Closing = _handler.ThrottleVisibleItems;
            _zoomer.DownloadFile = fileId => ViewModel.ClientService.DownloadFile(fileId, 32);
            _zoomer.SessionId = () => ViewModel.ClientService.SessionId;
        }

        private void OnClosing(ContentDialog sender, ContentDialogClosingEventArgs args)
        {
            ViewModel.PropertyChanged -= OnPropertyChanged;

            _handler.UnloadItems();
            _zoomer.Release();
        }

        private void OnPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName.Equals("STICKERSET_INVALID"))
            {
                Hide();
                ViewModel.NavigationService.ShowToast(Strings.AddStickersNotFound, ToastPopupIcon.Info);
            }
        }

        #region Show

        public Action<Sticker> ItemClick { get; set; }

        public static Task<ContentDialogResult> ShowAsync(INavigationService navigation, StickerSet parameter)
        {
            return ShowAsyncInternal(navigation, parameter, null);
        }

        public static Task<ContentDialogResult> ShowAsync(INavigationService navigation, StickerSet parameter, Action<Sticker> callback)
        {
            return ShowAsyncInternal(navigation, parameter, callback);
        }

        public static Task<ContentDialogResult> ShowAsync(INavigationService navigation, HashSet<long> parameter)
        {
            return ShowAsyncInternal(navigation, parameter, null);
        }

        public static Task<ContentDialogResult> ShowAsync(INavigationService navigation, HashSet<long> parameter, Action<Sticker> callback)
        {
            return ShowAsyncInternal(navigation, parameter, callback);
        }

        public static Task<ContentDialogResult> ShowAsync(INavigationService navigation, long parameter)
        {
            return ShowAsyncInternal(navigation, parameter, null);
        }

        public static Task<ContentDialogResult> ShowAsync(INavigationService navigation, long parameter, Action<Sticker> callback)
        {
            return ShowAsyncInternal(navigation, parameter, callback);
        }

        public static Task<ContentDialogResult> ShowAsync(INavigationService navigation, InputFileId parameter)
        {
            return ShowAsyncInternal(navigation, parameter, null);
        }

        public static Task<ContentDialogResult> ShowAsync(INavigationService navigation, InputFileId parameter, Action<Sticker> callback)
        {
            return ShowAsyncInternal(navigation, parameter, callback);
        }

        private static Task<ContentDialogResult> ShowAsyncInternal(INavigationService navigation, object parameter, Action<Sticker> callback)
        {
            var popup = new StickersPopup(navigation);

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
            return popup.ShowQueuedAsync(navigation.XamlRoot);
        }

        public static Task<ContentDialogResult> ShowAsync(INavigationService navigation, string parameter)
        {
            return ShowAsyncInternal(navigation, parameter, null);
        }

        public static Task<ContentDialogResult> ShowAsync(INavigationService navigation, string parameter, Action<Sticker> callback)
        {
            return ShowAsyncInternal(navigation, parameter, callback);
        }

        #endregion

        #region Recycle

        private void OnChoosingGroupHeaderContainer(ListViewBase sender, ChoosingGroupHeaderContainerEventArgs args)
        {
            if (args.GroupHeaderContainer == null)
            {
                args.GroupHeaderContainer = new GridViewHeaderItem
                {
                    Style = sender.GroupStyle[0].HeaderContainerStyle,
                    ContentTemplate = sender.GroupStyle[0].HeaderTemplate
                };
            }

            args.GroupHeaderContainer.Padding = new Thickness(0, args.GroupIndex > 0 ? 16 : 0, 0, 0);
            args.GroupHeaderContainer.Visibility = ViewModel.Items.Count > 1
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

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

        private string ConvertIsInstalled(bool installed, bool archived, StickerType type)
        {
            if (ViewModel == null || ViewModel.IsLoading)
            {
                Action.Visibility = Visibility.Collapsed;
                return string.Empty;
            }

            Action.Visibility = Visibility.Visible;

            if (ViewModel.Items.Count > 1)
            {
                MoreButton.Visibility = Visibility.Collapsed;

                if (installed && !archived)
                {
                    Action.Style = BootStrapper.Current.Resources["DangerButtonStyle"] as Style;
                    return Locale.Declension(Strings.R.RemoveManyEmojiPacksCount, ViewModel.Items.Count(x => x.IsInstalled));
                }

                Action.Style = BootStrapper.Current.Resources["AccentButtonStyle"] as Style;
                return Locale.Declension(Strings.R.AddManyEmojiPacksCount, ViewModel.Items.Count(x => !x.IsInstalled));

            }
            else
            {
                MoreButton.Visibility = Visibility.Visible;

                if (installed && !archived)
                {
                    Action.Style = BootStrapper.Current.Resources["DangerButtonStyle"] as Style;
                    return Locale.Declension(type is StickerTypeCustomEmoji ? Strings.R.RemoveManyEmojiCount : Strings.R.RemoveManyStickersCount, ViewModel.Count);
                }

                Action.Style = BootStrapper.Current.Resources["AccentButtonStyle"] as Style;
                return Locale.Declension(type is StickerTypeCustomEmoji ? Strings.R.AddManyEmojiCount : Strings.R.AddManyStickersCount, ViewModel.Count);
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

        private void More_ContextRequested(object sender, RoutedEventArgs e)
        {
            var flyout = new MenuFlyout();
            flyout.CreateFlyoutItem(Share, Strings.ShareFile, Icons.Share);
            flyout.CreateFlyoutItem(CopyLink, Strings.CopyLink, Icons.Link);

            flyout.ShowAt(sender as UIElement, FlyoutPlacementMode.BottomEdgeAlignedRight);
        }

        private void Share()
        {
            ViewModel.ShowPopup(new ChooseChatsPopup(), new ChooseChatsConfigurationPostLink(new InternalLinkTypeStickerSet(ViewModel.Items[0].Name, ViewModel.StickerType is StickerTypeCustomEmoji)));
        }

        private void CopyLink()
        {
            MessageHelper.CopyLink(ViewModel.ClientService, XamlRoot, new InternalLinkTypeStickerSet(ViewModel.Items[0].Name, ViewModel.StickerType is StickerTypeCustomEmoji));
        }

        private void Action_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.Execute();
            Hide();
        }
    }
}
