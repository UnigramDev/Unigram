//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using Telegram.Common;
using Telegram.Services.Settings;
using Telegram.Streams;
using Telegram.Td.Api;
using Telegram.ViewModels.Drawers;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;
using Point = Windows.Foundation.Point;

namespace Telegram.Controls.Drawers
{
    public class ItemContextRequestedEventArgs<T> : EventArgs
    {
        private readonly ContextRequestedEventArgs _args;

        public ItemContextRequestedEventArgs(T item, ContextRequestedEventArgs args)
        {
            _args = args;
            Item = item;
        }

        public bool TryGetPosition(UIElement relativeTo, out Point point)
        {
            return _args.TryGetPosition(relativeTo, out point);
        }

        public T Item { get; }

        public bool Handled
        {
            get => _args.Handled;
            set => _args.Handled = value;
        }
    }

    public sealed partial class AnimationDrawer : UserControl, IDrawer
    {
        public AnimationDrawerViewModel ViewModel => DataContext as AnimationDrawerViewModel;

        public event EventHandler<ItemClickEventArgs> ItemClick;
        public event EventHandler<ItemContextRequestedEventArgs<Animation>> ItemContextRequested;

        private readonly AnimatedListHandler _handler;
        private readonly ZoomableListHandler _zoomer;

        private bool _isActive;

        public AnimationDrawer()
        {
            InitializeComponent();

            _handler = new AnimatedListHandler(List, AnimatedListType.Animations);

            _zoomer = new ZoomableListHandler(List);
            _zoomer.Opening = _handler.UnloadVisibleItems;
            _zoomer.Closing = _handler.ThrottleVisibleItems;
            _zoomer.DownloadFile = fileId => ViewModel.ClientService.DownloadFile(fileId, 32);
            _zoomer.SessionId = () => ViewModel.ClientService.SessionId;

            ElementComposition.GetElementVisual(this).Clip = Window.Current.Compositor.CreateInsetClip();

            var header = DropShadowEx.Attach(Separator);
            header.Clip = header.Compositor.CreateInsetClip(0, 40, 0, -40);

            var debouncer = new EventDebouncer<TextChangedEventArgs>(Constants.TypingTimeout, handler => SearchField.TextChanged += new TextChangedEventHandler(handler));
            debouncer.Invoked += (s, args) =>
            {
                ViewModel?.Search(SearchField.Text);
            };
        }

        public StickersTab Tab => StickersTab.Animations;

        public Thickness ScrollingHostPadding
        {
            get => List.Padding;
            set => List.Padding = new Thickness(2, value.Top, 0, value.Bottom);
        }

        public ListViewBase ScrollingHost => List;

        public void Activate(Chat chat, EmojiSearchType type = EmojiSearchType.Default)
        {
            _isActive = true;
            _handler.ThrottleVisibleItems();

            SearchField.SetType(ViewModel.ClientService, type);

            ViewModel.Update();
        }

        public void Deactivate()
        {
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

        public void UnloadVisibleItems()
        {
            _handler.UnloadVisibleItems();
        }

        private void OnItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is Animation animation)
            {
                ItemClick?.Invoke(sender, e);
            }
        }

        private void OnChoosingItemContainer(ListViewBase sender, ChoosingItemContainerEventArgs args)
        {
            if (args.ItemContainer == null)
            {
                args.ItemContainer = new GridViewItem();
                args.ItemContainer.Style = sender.ItemContainerStyle;
                args.ItemContainer.ContentTemplate = sender.ItemTemplate;
                args.ItemContainer.ContextRequested += OnContextRequested;

                _zoomer.ElementPrepared(args.ItemContainer);
            }

            args.IsContainerPrepared = true;
        }

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            var content = args.ItemContainer.ContentTemplateRoot as Border;
            var animation = args.Item as Animation;

            if (args.InRecycleQueue)
            {
                return;
            }

            var file = animation.AnimationValue;
            if (file == null)
            {
                return;
            }

            var animated = content.Child as AnimatedImage;
            animated.Source = new DelayedFileSource(ViewModel.ClientService, file);

            args.Handled = true;
        }

        private void OnContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
            var animation = List.ItemFromContainer(sender) as Animation;
            if (animation == null)
            {
                return;
            }

            ItemContextRequested?.Invoke(sender, new ItemContextRequestedEventArgs<Animation>(animation, args));
        }

        private void SearchField_TextChanged(object sender, TextChangedEventArgs e)
        {
            //ViewModel.Search(SearchField.Text, false);
        }

        private void SearchField_CategorySelected(object sender, EmojiCategorySelectedEventArgs e)
        {
            if (e.Category.Source is EmojiCategorySourceSearch search)
            {
                ViewModel.Search(string.Join(" ", search.Emojis));
            }
        }

        private object ConvertItems(object items)
        {
            _handler.ThrottleVisibleItems();
            return items;
        }

        private void Player_Ready(object sender, EventArgs e)
        {
            _handler.ThrottleVisibleItems();
        }
    }
}
