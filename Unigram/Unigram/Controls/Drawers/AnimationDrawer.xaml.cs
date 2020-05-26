using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Converters;
using Unigram.Services;
using Unigram.Services.Settings;
using Unigram.ViewModels;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media.Imaging;

namespace Unigram.Controls.Drawers
{
    public sealed partial class AnimationDrawer : UserControl, IDrawer
    {
        public DialogViewModel ViewModel => DataContext as DialogViewModel;

        public Action<Animation> AnimationClick { get; set; }

        private AnimatedRepeaterHandler<Animation> _handler;
        private DispatcherTimer _throttler;

        private FileContext<Animation> _animations = new FileContext<Animation>();

        public AnimationDrawer()
        {
            InitializeComponent();

            _handler = new AnimatedRepeaterHandler<Animation>(Repeater, ScrollingHost);
            _handler.DownloadFile = DownloadFile;

            _throttler = new DispatcherTimer();
            _throttler.Interval = TimeSpan.FromMilliseconds(Constants.TypingTimeout);
            _throttler.Tick += (s, args) =>
            {
                _throttler.Stop();
                _handler.LoadVisibleItems(false);
            };

            ElementCompositionPreview.GetElementVisual(this).Clip = Window.Current.Compositor.CreateInsetClip();

            var observable = Observable.FromEventPattern<TextChangedEventArgs>(FieldAnimations, "TextChanged");
            var throttled = observable.Throttle(TimeSpan.FromMilliseconds(Constants.TypingTimeout)).ObserveOnDispatcher().Subscribe(x =>
            {
                ViewModel.Stickers.FindAnimations(FieldAnimations.Text);
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
            _handler.LoadVisibleItems(false);
        }

        public void Deactivate()
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
            AnimationClick?.Invoke(animation);

            if (Window.Current.Bounds.Width >= 500)
            {
                Focus(FocusState.Programmatic);
            }
        }

        private void OnElementPrepared(ItemsRepeater sender, ItemsRepeaterElementPreparedEventArgs args)
        {
            var button = args.Element as Button;
            var animation = button.DataContext as Animation;

            button.Tag = animation;

            var content = button.Content as Grid;
            var image = content.Children[0] as Image;

            var file = animation.Thumbnail?.Photo;
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
            var element = sender as FrameworkElement;
            var animation = element.DataContext as Animation;

            var flyout = new MenuFlyout();
            flyout.CreateFlyoutItem(ViewModel.AnimationDeleteCommand, animation, Strings.Resources.Delete, new FontIcon { Glyph = Icons.Delete });

            if (!ViewModel.IsSchedule)
            {
                var chat = ViewModel.Chat;
                if (chat == null)
                {
                    return;
                }

                var self = ViewModel.CacheService.IsSavedMessages(chat);

                flyout.CreateFlyoutSeparator();
                flyout.CreateFlyoutItem(new RelayCommand<Animation>(anim => ViewModel.AnimationSendExecute(anim, null, true)), animation, Strings.Resources.SendWithoutSound, new FontIcon { Glyph = Icons.Mute });
                //flyout.CreateFlyoutItem(new RelayCommand<Animation>(anim => ViewModel.AnimationSendExecute(anim, true, null)), animation, self ? Strings.Resources.SetReminder : Strings.Resources.ScheduleMessage, new FontIcon { Glyph = Icons.Schedule });
            }

            args.ShowAt(flyout, element);
        }

        private void FieldAnimations_TextChanged(object sender, TextChangedEventArgs e)
        {
            //ViewModel.Stickers.FindAnimations(FieldAnimations.Text);
        }

        private void DownloadFile(int id, Animation animation)
        {
            _animations[id].Add(animation);
            ViewModel.ProtoService.DownloadFile(id, 1);
        }

        public void UpdateFile(File file)
        {
            if (_animations.TryGetValue(file.Id, out List<Animation> items) && items.Count > 0)
            {
                foreach (var item in items)
                {
                    item.UpdateFile(file);

                    var index = Repeater.ItemsSourceView.IndexOf(item);
                    if (index < 0)
                    {
                        continue;
                    }

                    if (item.Thumbnail?.Photo.Id == file.Id)
                    {
                        var button = Repeater.TryGetElement(index) as Button;
                        var content = button.Content as Grid;
                        var image = content.Children[0] as Image;

                        image.Source = new BitmapImage(new Uri("file:///" + file.Local.Path));
                    }
                    else if (item.AnimationValue.Id == file.Id)
                    {
                        _throttler.Stop();
                        _throttler.Start();
                    }
                }
            }
        }
    }
}
