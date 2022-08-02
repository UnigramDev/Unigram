using System;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Services.Settings;
using Unigram.ViewModels.Drawers;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
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

        private bool _isActive;

        public AnimationDrawer()
        {
            InitializeComponent();

            _handler = new AnimatedListHandler<Animation>(List);

            _zoomer = new ZoomableListHandler(List);
            _zoomer.Opening = _handler.UnloadVisibleItems;
            _zoomer.Closing = _handler.ThrottleVisibleItems;
            _zoomer.DownloadFile = fileId => ViewModel.ProtoService.DownloadFile(fileId, 32);
            _zoomer.SessionId = () => ViewModel.ProtoService.SessionId;

            ElementCompositionPreview.GetElementVisual(this).Clip = Window.Current.Compositor.CreateInsetClip();

            var header = DropShadowEx.Attach(Separator);
            header.Clip = header.Compositor.CreateInsetClip(0, 48, 0, -48);

            var debouncer = new EventDebouncer<TextChangedEventArgs>(Constants.TypingTimeout, handler => FieldAnimations.TextChanged += new TextChangedEventHandler(handler));
            debouncer.Invoked += (s, args) =>
            {
                ViewModel.Search(FieldAnimations.Text);
            };
        }

        public StickersTab Tab => StickersTab.Animations;

        public void Activate(Chat chat)
        {
            _isActive = true;
            _handler.ThrottleVisibleItems();

            if (chat != null)
            {
                ViewModel.Update();
            }
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
                _handler.LoadVisibleItems(false);
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

            if (file.Local.IsFileExisting())
            {
                view.Source = new LocalVideoSource(file);
                view.Thumbnail = null;
            }
            else
            {
                view.Source = null;

                UpdateManager.Subscribe(content, ViewModel.ProtoService, file, UpdateFile, true);

                if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive)
                {
                    ViewModel.ProtoService.DownloadFile(file.Id, 1);
                }

                var thumbnail = animation.Thumbnail?.File;
                if (thumbnail != null)
                {
                    if (thumbnail.Local.IsFileExisting())
                    {
                        view.Thumbnail = new BitmapImage(UriEx.ToLocal(thumbnail.Local.Path));
                    }
                    else
                    {
                        view.Thumbnail = null;

                        UpdateManager.Subscribe(view, ViewModel.ProtoService, thumbnail, UpdateThumbnail, true);

                        if (thumbnail.Local.CanBeDownloaded && !thumbnail.Local.IsDownloadingActive)
                        {
                            ViewModel.ProtoService.DownloadFile(thumbnail.Id, 1);
                        }
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

        private void UpdateFile(object target, File file)
        {
            var content = target as Border;
            if (content == null)
            {
                return;
            }

            if (content.Child is AnimationView view)
            {
                view.Source = new LocalVideoSource(file);
                _handler.ThrottleVisibleItems();
            }
        }

        private void UpdateThumbnail(object target, File file)
        {
            if (target is AnimationView view)
            {
                view.Thumbnail = new BitmapImage(UriEx.ToLocal(file.Local.Path));
            }
        }
    }
}
