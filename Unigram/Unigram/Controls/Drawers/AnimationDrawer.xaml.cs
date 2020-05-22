using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Reactive.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Telegram.Td;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Converters;
using Unigram.Services;
using Unigram.Services.Settings;
using Unigram.ViewModels;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

namespace Unigram.Controls.Drawers
{
    public sealed partial class AnimationDrawer : UserControl, IDrawer
    {
        public DialogViewModel ViewModel => DataContext as DialogViewModel;

        public Action<Animation> AnimationClick { get; set; }

        private FileContext<Animation> _animations = new FileContext<Animation>();

        public AnimationDrawer()
        {
            InitializeComponent();

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

        }

        public void Deactivate()
        {

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
            var content = args.Element as Button;
            var animation = content.DataContext as Animation;

            content.Tag = animation;

            var image = content.Content as Image;

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
                        var content = Repeater.TryGetElement(index) as Button;
                        var image = content.Content as Image;

                        image.Source = new BitmapImage(new Uri("file:///" + file.Local.Path));
                    }
                    else if (item.AnimationValue.Id == file.Id)
                    {
                        //_throttler.Stop();
                        //_throttler.Start();
                    }
                }
            }
        }
    }
}
