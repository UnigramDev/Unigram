//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Collections.Generic;
using Telegram.Common;
using Telegram.Navigation;
using Telegram.Streams;
using Telegram.Td.Api;
using Telegram.ViewModels.Drawers;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;

namespace Telegram.Controls.Drawers
{
    public sealed partial class EffectDrawer : UserControl
    {
        public EffectDrawerViewModel ViewModel => DataContext as EffectDrawerViewModel;

        public event EventHandler<MessageEffect> ItemClick;

        private readonly AnimatedListHandler _handler;
        private readonly ZoomableListHandler _zoomer;

        private readonly Dictionary<MessageEffect, Grid> _itemIdToContent = new();
        private long _selectedSetId;

        private bool _isActive;

        public EffectDrawer()
        {
            InitializeComponent();

            ElementComposition.GetElementVisual(this).Clip = Window.Current.Compositor.CreateInsetClip();

            _handler = new AnimatedListHandler(List, AnimatedListType.Stickers);

            _zoomer = new ZoomableListHandler(List);
            _zoomer.Opening = UnloadVisibleItems;
            _zoomer.Closing = ThrottleVisibleItems;
            _zoomer.DownloadFile = fileId => ViewModel.ClientService.DownloadFile(fileId, 32);
            _zoomer.SessionId = () => ViewModel.ClientService.SessionId;

            //var debouncer = new EventDebouncer<TextChangedEventArgs>(Constants.TypingTimeout, handler => FieldStickers.TextChanged += new TextChangedEventHandler(handler));
            //debouncer.Invoked += async (s, args) =>
            //{
            //    var items = ViewModel.SearchStickers;
            //    if (items != null && string.Equals(FieldStickers.Text, items.Query))
            //    {
            //        await items.LoadMoreItemsAsync(1);
            //        await items.LoadMoreItemsAsync(2);
            //    }
            //};
        }

        public Services.Settings.StickersTab Tab => Services.Settings.StickersTab.Stickers;

        public Thickness ScrollingHostPadding
        {
            get => List.Padding;
            set => List.Padding = new Thickness(2, value.Top, 2, value.Bottom);
        }

        public ListViewBase ScrollingHost => List;

        public void Activate()
        {
            _isActive = true;
            _handler.ThrottleVisibleItems();

            SearchField.SetType(ViewModel.ClientService, EmojiSearchType.Default);
            ViewModel.Update();
        }

        public void Deactivate()
        {
            _itemIdToContent.Clear();

            _isActive = false;
            _handler.UnloadItems();

            // This is called only right before XamlMarkupHelper.UnloadObject
            // so we can safely clean up any kind of anything from here.
            _zoomer.Release();
            Bindings.StopTracking();
        }

        public void LoadVisibleItems()
        {
            if (_isActive)
            {
                _handler.LoadVisibleItems();
            }
        }

        public void ThrottleVisibleItems()
        {
            if (_isActive)
            {
                _handler.ThrottleVisibleItems();
            }
        }

        public void UnloadVisibleItems()
        {
            _handler.UnloadVisibleItems();
        }

        private void Stickers_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is MessageEffect effect)
            {
                if (effect.IsPremium && !ViewModel.IsPremium)
                {
                    var navigationService = WindowContext.Current.GetNavigationService();

                    ToastPopup.ShowPromo(navigationService, Strings.AnimatedEffectPremium, Strings.OptionPremiumRequiredButton, null);
                }
                else
                {
                    ItemClick?.Invoke(this, effect);
                }
            }
        }

        private void Stickers_Loaded(object sender, RoutedEventArgs e)
        {
            var scrollingHost = List.GetChild<ScrollViewer>();
            if (scrollingHost != null)
            {
                scrollingHost.VerticalSnapPointsType = SnapPointsType.None;

                // Syncronizes GridView with the toolbar ListView
                scrollingHost.ViewChanged += ScrollingHost_ViewChanged;
                ScrollingHost_ViewChanged(null, null);
            }
        }

        private void Toolbar_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is StickerSetViewModel set && set.Stickers != null)
            {
                List.ScrollIntoView(e.ClickedItem, ScrollIntoViewAlignment.Leading);
            }
        }

        private void ScrollingHost_ViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
        {
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
            var effect = args.Item as MessageEffect;

            if (args.InRecycleQueue || effect == null)
            {
                if (effect != null)
                {
                    _itemIdToContent.Remove(effect);
                }

                return;
            }

            _itemIdToContent[effect] = content;

            if (effect?.Type is MessageEffectTypeEmojiReaction emojiReaction)
            {
                var animation = content.Children[0] as AnimatedImage;
                animation.Source = new DelayedFileSource(ViewModel.ClientService, emojiReaction.SelectAnimation);
            }
            else if (effect?.Type is MessageEffectTypePremiumSticker premiumSticker)
            {
                var animation = content.Children[0] as AnimatedImage;
                animation.Source = new DelayedFileSource(ViewModel.ClientService, premiumSticker.Sticker);

                var emoji = content.Children[2] as TextBlock;
                emoji.Text = effect.Emoji;
                emoji.Visibility = effect.IsPremium && !ViewModel.IsPremium
                    ? Visibility.Collapsed
                    : Visibility.Visible;
            }
            else
            {
                var animation = content.Children[0] as AnimatedImage;
                animation.Source = null;
            }

            var locked = content.Children[1] as Border;
            locked.Visibility = effect.IsPremium && !ViewModel.IsPremium
                ? Visibility.Visible
                : Visibility.Collapsed;

            args.Handled = true;
        }

        private void Toolbar_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }
            else if (args.Item is SupergroupStickerSetViewModel supergroup)
            {
                Automation.SetToolTip(args.ItemContainer, supergroup.Title);

                var chat = ViewModel.ClientService.GetChat(supergroup.ChatId);
                if (chat == null)
                {
                    return;
                }

                var content = args.ItemContainer.ContentTemplateRoot as Border;
                if (content?.Child is not ProfilePicture photo)
                {
                    return;
                }

                photo.SetChat(ViewModel.ClientService, chat, 24);
                args.Handled = true;
            }
            else if (args.Item is StickerSetViewModel sticker)
            {
                Automation.SetToolTip(args.ItemContainer, sticker.Title);

                var content = args.ItemContainer.ContentTemplateRoot as Grid;

                if (content == null || sticker == null || (sticker.Thumbnail == null && sticker.Covers == null))
                {
                    return;
                }

                var cover = sticker.GetThumbnail();
                if (cover != null)
                {
                    var animation = content.Children[0] as AnimatedImage;
                    animation.Source = new DelayedFileSource(ViewModel.ClientService, cover);
                }
                else
                {
                    var animation = content.Children[0] as AnimatedImage;
                    animation.Source = null;
                }

                args.Handled = true;
            }
        }

        private void SearchField_TextChanged(object sender, TextChangedEventArgs e)
        {
            ViewModel.Search(SearchField.Text, false);
        }

        private void SearchField_CategorySelected(object sender, EmojiCategorySelectedEventArgs e)
        {
            ViewModel.Search(e.Category.Source);
        }

        private void Player_Ready(object sender, EventArgs e)
        {
            _handler.ThrottleVisibleItems();
        }
    }
}
