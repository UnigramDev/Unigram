using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Reactive.Linq;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Services;
using Unigram.Services.Settings;
using Unigram.ViewModels.Drawers;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media.Imaging;

namespace Unigram.Controls.Drawers
{
    public sealed partial class AnimationDrawer : UserControl, IDrawer
    {
        public AnimationDrawerViewModel ViewModel => DataContext as AnimationDrawerViewModel;

        public Action<Animation> ItemClick { get; set; }
        public event TypedEventHandler<UIElement, ContextRequestedEventArgs> ItemContextRequested;

        private AnimatedRepeaterHandler<Animation> _handler;
        private ZoomableRepeaterHandler _zoomer;

        private FileContext<Animation> _animations = new FileContext<Animation>();

        private bool _isActive;

        public AnimationDrawer()
        {
            InitializeComponent();

            _handler = new AnimatedRepeaterHandler<Animation>(Repeater, ScrollingHost);
            _handler.DownloadFile = DownloadFile;

            _zoomer = new ZoomableRepeaterHandler(Repeater);
            _zoomer.Opening = _handler.UnloadVisibleItems;
            _zoomer.Closing = _handler.ThrottleVisibleItems;
            _zoomer.DownloadFile = fileId => ViewModel.ProtoService.DownloadFile(fileId, 32);

            ElementCompositionPreview.GetElementVisual(this).Clip = Window.Current.Compositor.CreateInsetClip();

            var shadow = DropShadowEx.Attach(Separator, 20, 0.25f);
            Separator.SizeChanged += (s, args) =>
            {
                shadow.Size = args.NewSize.ToVector2();
            };

            var observable = Observable.FromEventPattern<TextChangedEventArgs>(FieldAnimations, "TextChanged");
            var throttled = observable.Throttle(TimeSpan.FromMilliseconds(Constants.TypingTimeout)).ObserveOnDispatcher().Subscribe(x =>
            {
                ViewModel.FindAnimations(FieldAnimations.Text);
                //var items = ViewModel.Stickers.SearchStickers;
                //if (items != null && string.Equals(FieldStickers.Text, items.Query))
                //{
                //    await items.LoadMoreItemsAsync(1);
                //    await items.LoadMoreItemsAsync(2);
                //}
            });
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
            _handler.UnloadVisibleItems();
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

        private void Animation_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var animation = button.Tag as Animation;

            Animation_Click(sender, animation);
        }

        private void Animation_Click(object sender, Animation animation)
        {
            ItemClick?.Invoke(animation);

            if (Window.Current.Bounds.Width >= 500)
            {
                Focus(FocusState.Programmatic);
            }
        }

        private void OnElementPrepared(ItemsRepeater sender, ItemsRepeaterElementPreparedEventArgs args)
        {
            var button = args.Element as Button;
            var animation = button.DataContext as Animation;

            _zoomer.ElementPrepared(button);

            button.Tag = animation;

            var content = button.Content as Grid;
            var image = content.Children[0] as Image;

            var file = animation.Thumbnail?.File;
            if (file == null)
            {
                return;
            }

            if (file.Local.IsDownloadingCompleted)
            {
                image.Source = new BitmapImage(new Uri("file:///" + file.Local.Path));
            }
            else if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive)
            {
                image.Source = null;
                DownloadFile(file.Id, animation);
            }
        }

        private void OnElementClearing(ItemsRepeater sender, ItemsRepeaterElementClearingEventArgs args)
        {
            _zoomer.ElementClearing(args.Element);

            if (args.Element is Button button && button.Content is Grid content && content.Children[0] is Image image)
            {
                if (content.Children.Count > 1)
                {
                    content.Children.RemoveAt(1);
                }

                image.Source = null;
            }
        }

        private void OnContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
            ItemContextRequested?.Invoke(sender, args);
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

                    var index = ViewModel.Items.IndexOf(item);
                    if (index < 0)
                    {
                        continue;
                    }

                    if (item.Thumbnail?.File.Id == file.Id)
                    {
                        var button = Repeater.TryGetElement(index) as Button;
                        if (button == null)
                        {
                            continue;
                        }

                        var content = button.Content as Grid;
                        var image = content.Children[0] as Image;

                        image.Source = new BitmapImage(new Uri("file:///" + file.Local.Path));
                    }
                    else if (item.AnimationValue.Id == file.Id)
                    {
                        _handler.ThrottleVisibleItems();
                    }
                }
            }

            _zoomer.UpdateFile(file);
        }
    }
}
