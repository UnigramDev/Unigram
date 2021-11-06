using System;
using System.Collections.Generic;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Services;
using Unigram.Services.Settings;
using Unigram.ViewModels.Drawers;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media.Imaging;
using Point = Windows.Foundation.Point;

namespace Unigram.Controls.Drawers
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

        public Action<Animation> ItemClick { get; set; }
        public event TypedEventHandler<UIElement, ItemContextRequestedEventArgs<Animation>> ItemContextRequested;

        private readonly AnimatedListHandler<Animation> _handler;
        private readonly ZoomableListHandler _zoomer;

        private readonly FileContext<Animation> _animations = new FileContext<Animation>();

        private bool _isActive;

        public AnimationDrawer()
        {
            InitializeComponent();

            _handler = new AnimatedListHandler<Animation>(List);

            _zoomer = new ZoomableListHandler(List);
            _zoomer.Opening = _handler.UnloadVisibleItems;
            _zoomer.Closing = _handler.ThrottleVisibleItems;
            _zoomer.DownloadFile = fileId => ViewModel.ProtoService.DownloadFile(fileId, 32);

            ElementCompositionPreview.GetElementVisual(this).Clip = Window.Current.Compositor.CreateInsetClip();

            DropShadowEx.Attach(Separator);

            var debouncer = new EventDebouncer<TextChangedEventArgs>(Constants.TypingTimeout, handler => FieldAnimations.TextChanged += new TextChangedEventHandler(handler));
            debouncer.Invoked += (s, args) =>
            {
                ViewModel.Search(FieldAnimations.Text);
            };
        }

        public StickersTab Tab => StickersTab.Animations;

        public void Activate()
        {
            _isActive = true;
            _handler.ThrottleVisibleItems();
        }

        public void Deactivate()
        {
            _isActive = false;
            _handler.UnloadVisibleItems(true);
        }

        public void LoadVisibleItems()
        {
            if (_isActive)
            {
                _handler.LoadVisibleItems(false);
            }
        }

        public void UnloadVisibleItems()
        {
            _handler.UnloadVisibleItems(false);
        }

        private void OnItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is Animation animation)
            {
                ItemClick?.Invoke(animation);
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
                if (content.Child is AnimationView recycle)
                {
                    recycle.Source = null;
                }

                return;
            }

            var view = content.Child as AnimationView;

            var file = animation.AnimationValue;
            if (file == null)
            {
                return;
            }

            if (file.Local.IsDownloadingCompleted)
            {
                view.Source = new LocalVideoSource(file);
                view.Thumbnail = null;
            }
            else if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive)
            {
                view.Source = null;
                DownloadFile(file.Id, animation);

                var thumbnail = animation.Thumbnail?.File;
                if (thumbnail != null)
                {
                    if (thumbnail.Local.IsDownloadingCompleted)
                    {
                        view.Thumbnail = new BitmapImage(UriEx.ToLocal(thumbnail.Local.Path));
                    }
                    else if (thumbnail.Local.CanBeDownloaded && !thumbnail.Local.IsDownloadingActive)
                    {
                        view.Thumbnail = null;
                        DownloadFile(thumbnail.Id, animation);
                    }
                }
            }
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

        private void FieldAnimations_TextChanged(object sender, TextChangedEventArgs e)
        {
            //ViewModel.Stickers.FindAnimations(FieldAnimations.Text);
        }

        private object ConvertItems(object items)
        {
            _handler.ThrottleVisibleItems();
            return items;
        }

        private void DownloadFile(int id, Animation animation = null)
        {
            if (animation != null)
            {
                _animations[id].Add(animation);
            }

            ViewModel.ProtoService.DownloadFile(id, 1);
        }

        public void UpdateFile(File file)
        {
            if (_animations.TryGetValue(file.Id, out List<Animation> items) && items.Count > 0)
            {
                foreach (var item in items)
                {
                    item.UpdateFile(file);

                    var container = List.ContainerFromItem(item) as SelectorItem;
                    if (container == null)
                    {
                        continue;
                    }

                    var content = container.ContentTemplateRoot as Border;
                    if (content == null)
                    {
                        continue;
                    }

                    if (content.Child is AnimationView view)
                    {
                        view.Source = new LocalVideoSource(file);
                        _handler.ThrottleVisibleItems();
                    }
                }
            }

            _zoomer.UpdateFile(file);
        }
    }
}
