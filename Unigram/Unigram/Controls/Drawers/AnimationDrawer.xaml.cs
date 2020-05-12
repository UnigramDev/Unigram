using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
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

        private void Mosaic_Click(object item)
        {
            if (item is Animation animation)
            {
                Animation_Click(null, animation);
            }
            else if (item is InlineQueryResultAnimation inlineAnimation)
            {
                Animation_Click(null, inlineAnimation.Animation);
            }
        }

        private void Animation_Click(object sender, Animation animation)
        {
            AnimationClick?.Invoke(animation);

            if (Window.Current.Bounds.Width >= 500)
            {
                Focus(FocusState.Programmatic);
            }
        }

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }

            var content = args.ItemContainer.ContentTemplateRoot as MosaicRow;
            var position = args.Item as MosaicMediaRow;

            content.UpdateLine(ViewModel.ProtoService, position, Mosaic_Click);

            //var content = args.ItemContainer.ContentTemplateRoot as Border;
            //var position = args.Item as MosaicMediaPosition;

            //var animation = position.Item as Animation;
            //if (animation == null)
            //{
            //    return;
            //}

            //if (args.Phase < 2)
            //{
            //    args.RegisterUpdateCallback(Animations_ContainerContentChanging);
            //}
            //else
            //{
            //    var file = animation.Thumbnail.Photo;
            //    if (file.Local.IsDownloadingCompleted)
            //    {
            //        content.Background = new ImageBrush { ImageSource = new BitmapImage(new Uri("file:///" + file.Local.Path)), AlignmentX = AlignmentX.Center, AlignmentY = AlignmentY.Center, Stretch = Stretch.UniformToFill };
            //    }
            //    else if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive)
            //    {
            //        DownloadFile(file.Id, animation);
            //    }
            //}

            args.Handled = true;
        }

        private void OnContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
            var element = sender as FrameworkElement;
            var position = element.Tag as MosaicMediaPosition;
            var animation = position.Item as Animation;

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
            _animations[id][animation.AnimationValue.Id] = animation;
            ViewModel.ProtoService.DownloadFile(id, 1);
        }

        public void UpdateFile(File file)
        {
            foreach (MosaicMediaRow line in GifsView.Items)
            {
                var any = false;
                foreach (var item in line)
                {
                    if (item.Item is Animation animation && animation.UpdateFile(file))
                    {
                        any = true;
                    }
                    else if (item.Item is InlineQueryResultAnimation inlineAnimation && inlineAnimation.Animation.UpdateFile(file))
                    {
                        any = true;
                    }
                }

                if (!any)
                {
                    continue;
                }

                var container = GifsView.ContainerFromItem(line) as SelectorItem;
                if (container == null)
                {
                    continue;
                }

                var content = container.ContentTemplateRoot as MosaicRow;
                if (content == null)
                {
                    continue;
                }

                content.UpdateFile(line, file);
            }
        }
    }
}
