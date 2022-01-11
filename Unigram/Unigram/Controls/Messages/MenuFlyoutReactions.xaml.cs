using LinqToVisualTree;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Geometry;
using System;
using System.Linq;
using System.Numerics;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.ViewModels;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media.Imaging;
using Point = Windows.Foundation.Point;

namespace Unigram.Controls.Messages
{
    public sealed partial class MenuFlyoutReactions : UserControl
    {
        public static MenuFlyoutReactions ShowAt(MessageViewModel message, MenuFlyout flyout, Point absolute)
        {
            return new MenuFlyoutReactions(message, flyout, absolute);
        }

        private MenuFlyoutReactions(MessageViewModel message, MenuFlyout flyout, Point absolute)
        {
            InitializeComponent();

            var last = flyout.Items.LastOrDefault();
            var presenter = last.Ancestors<MenuFlyoutPresenter>().FirstOrDefault();

            var transform = presenter.TransformToVisual(Window.Current.Content);
            var position = transform.TransformPoint(new Point());

            var relativeFirst = Math.Abs(absolute.Y - position.Y);
            var relativeLast = Math.Abs(absolute.Y - (position.Y + presenter.ActualHeight));
            var upsideDown = relativeLast < relativeFirst;

            var width = presenter.ActualSize.X + 18 + 12 + 18;

            Shadow.Width = width;
            Pill.Width = width;
            ScrollingHost.Width = width;

            BubbleMedium.VerticalAlignment = upsideDown ? VerticalAlignment.Top : VerticalAlignment.Bottom;
            BubbleMedium.Margin = new Thickness(0, upsideDown ? -6 : 0, 18, upsideDown ? 0 : -6);

            BubbleOverlay.VerticalAlignment = upsideDown ? VerticalAlignment.Top : VerticalAlignment.Bottom;
            BubbleOverlay.Margin = new Thickness(0, upsideDown ? -6 : 0, 18, upsideDown ? 0 : -6);

            LayoutRoot.Padding = new Thickness(16, upsideDown ? 32 : 16, 16, upsideDown ? 16 : 32);

            var reactions = message.ProtoService.Reactions;
            var offset = 0;
            var visible = Math.Ceiling((width - 8) / 34);

            foreach (var item in reactions)
            {
                var view = new LottieView();
                view.AutoPlay = offset < visible;
                view.IsLoopingEnabled = false;
                view.FrameSize = new Windows.Graphics.SizeInt32 { Width = 24, Height = 24 };
                view.DecodeFrameType = DecodePixelType.Logical;
                view.Width = 24;
                view.Height = 24;
                view.Margin = new Thickness(0, 0, 10, 0);
                view.Tag = offset < visible ? null : new object();

                var file = item.SelectAnimation.StickerValue;
                if (file.Local.IsDownloadingCompleted)
                {
                    view.Source = UriEx.ToLocal(file.Local.Path);
                }
                else
                {
                    view.Source = null;

                    UpdateManager.Subscribe(view, message, file, /*UpdateReaction*/UpdateFile, true);

                    if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive)
                    {
                        message.ProtoService.DownloadFile(file.Id, 16);
                    }
                }

                Presenter.Children.Add(view);

                if (offset < visible)
                {
                    var visual = ElementCompositionPreview.GetElementVisual(view);
                    visual.CenterPoint = new Vector3(12, 12, 0);
                    visual.Scale = Vector3.Zero;

                    var scale = visual.Compositor.CreateVector3KeyFrameAnimation();
                    scale.InsertKeyFrame(0, Vector3.Zero);
                    scale.InsertKeyFrame(1, Vector3.One);
                    scale.DelayTime = TimeSpan.FromMilliseconds(50 * (visible - Presenter.Children.Count));

                    visual.StartAnimation("Scale", scale);
                }

                offset++;
            }

            var device = CanvasDevice.GetSharedDevice();
            var rect1 = CanvasGeometry.CreateRectangle(device, 0, 0, 306, 86);
            var elli1 = CanvasGeometry.CreateRoundedRectangle(device, 18 + 16, upsideDown ? -86 - 4 : 16 + 36 + 4, presenter.ActualSize.X, 86, 8, 8);
            var group1 = CanvasGeometry.CreateGroup(device, new[] { elli1, rect1 }, CanvasFilledRegionDetermination.Alternate);

            var rootVisual = ElementCompositionPreview.GetElementVisual(LayoutRoot);
            rootVisual.Clip = rootVisual.Compositor.CreateGeometricClip(rootVisual.Compositor.CreatePathGeometry(new CompositionPath(group1)));

            var pillShadow = Window.Current.Compositor.CreateDropShadow();
            pillShadow.BlurRadius = 16;
            pillShadow.Opacity = 0.14f;
            pillShadow.Color = Colors.Black;
            pillShadow.Mask = Pill.GetAlphaMask();

            var pillReceiver = Window.Current.Compositor.CreateSpriteVisual();
            pillReceiver.Shadow = pillShadow;
            pillReceiver.Size = new Vector2(width, 36);
            pillReceiver.Offset = new Vector3(0, 8, 0);

            var mediumShadow = Window.Current.Compositor.CreateDropShadow();
            mediumShadow.BlurRadius = 16;
            mediumShadow.Opacity = 0.14f;
            mediumShadow.Color = Colors.Black;
            mediumShadow.Mask = BubbleMedium.GetAlphaMask();

            var mediumReceiver = Window.Current.Compositor.CreateSpriteVisual();
            mediumReceiver.Shadow = mediumShadow;
            mediumReceiver.Size = new Vector2(12, 12);
            mediumReceiver.Offset = new Vector3(width - 18 - 12, upsideDown ? -8 : 36 - 8, 0);

            var receivers = Window.Current.Compositor.CreateContainerVisual();
            receivers.Children.InsertAtBottom(pillReceiver);
            receivers.Children.InsertAtBottom(mediumReceiver);
            receivers.Size = new Vector2(width, 54);

            ElementCompositionPreview.SetElementChildVisual(Shadow, receivers);

            var x = position.X - 18;
            var y = position.Y - (36 + 4);

            if (upsideDown)
            {
                y = position.Y + presenter.ActualHeight + 4;
                y -= 16;
            }

            var popup = new Popup();
            popup.Child = this;
            popup.Margin = new Thickness(x - 16, y - 16, 0, 0);
            popup.ShouldConstrainToRootBounds = false;
            popup.RequestedTheme = presenter.ActualTheme;
            popup.IsOpen = true;

            var visualMedium = ElementCompositionPreview.GetElementVisual(BubbleMedium);
            var visualOverlay = ElementCompositionPreview.GetElementVisual(BubbleOverlay);
            visualMedium.CenterPoint = new Vector3(6, 6, 0);
            visualOverlay.CenterPoint = new Vector3(6, 6, 0);

            var visualPill = ElementCompositionPreview.GetElementVisual(Pill);
            visualPill.CenterPoint = new Vector3(36 / 2, 36 / 2, 0);
            visualPill.CenterPoint = new Vector3(width - 36 / 2, 36 / 2, 0);

            var clip = visualPill.Compositor.CreateRoundedRectangleGeometry();
            clip.CornerRadius = new Vector2(36 / 2);

            var batch = visualPill.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);

            var scaleSmall = visualMedium.Compositor.CreateVector3KeyFrameAnimation();
            scaleSmall.InsertKeyFrame(0, Vector3.Zero);
            scaleSmall.InsertKeyFrame(1, Vector3.One);
            scaleSmall.Duration = TimeSpan.FromMilliseconds(150);

            var scaleMedium = visualMedium.Compositor.CreateVector3KeyFrameAnimation();
            scaleMedium.InsertKeyFrame(0, Vector3.Zero);
            scaleMedium.InsertKeyFrame(1, Vector3.One);
            scaleMedium.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;
            scaleMedium.DelayTime = TimeSpan.FromMilliseconds(100);
            scaleMedium.Duration = TimeSpan.FromMilliseconds(150);

            var scalePill = visualMedium.Compositor.CreateSpringVector3Animation();
            scalePill.InitialValue = Vector3.Zero;
            scalePill.FinalValue = Vector3.One;
            scalePill.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;
            scalePill.DampingRatio = 0.6f;

            var translation = visualMedium.Compositor.CreateScalarKeyFrameAnimation();
            translation.InsertKeyFrame(0, 0);
            translation.InsertKeyFrame(1, 16);

            var opacity = visualMedium.Compositor.CreateScalarKeyFrameAnimation();
            opacity.InsertKeyFrame(0, 0);
            opacity.InsertKeyFrame(1, 0.14f);

            visualMedium.StartAnimation("Scale", scaleMedium);
            visualOverlay.StartAnimation("Scale", scaleMedium);
            visualPill.StartAnimation("Scale", scalePill);

            mediumShadow.StartAnimation("BlurRadius", translation);
            mediumShadow.StartAnimation("Opacity", opacity);

            translation.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;
            translation.DelayTime = scaleMedium.Duration + TimeSpan.FromMilliseconds(100);
            opacity.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;
            opacity.DelayTime = scaleMedium.Duration + TimeSpan.FromMilliseconds(100);

            pillShadow.StartAnimation("BlurRadius", translation);
            pillShadow.StartAnimation("Opacity", opacity);

            var resize = visualPill.Compositor.CreateVector2KeyFrameAnimation();
            resize.InsertKeyFrame(0, new Vector2(36, 36));
            resize.InsertKeyFrame(1, new Vector2(width, 36));
            resize.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;
            resize.DelayTime = TimeSpan.FromMilliseconds(100);
            resize.Duration = TimeSpan.FromMilliseconds(150);

            var move = visualPill.Compositor.CreateVector2KeyFrameAnimation();
            move.InsertKeyFrame(0, new Vector2(width - 36, 0));
            move.InsertKeyFrame(1, new Vector2());
            move.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;
            move.DelayTime = TimeSpan.FromMilliseconds(100);
            move.Duration = TimeSpan.FromMilliseconds(150);

            visualPill.Clip = visualPill.Compositor.CreateGeometricClip(clip);
            clip.StartAnimation("Size", resize);
            clip.StartAnimation("Offset", move);

            batch.End();

            flyout.Closing += (s, args) =>
            {
                popup.IsOpen = false;
            };
        }

        private void UpdateFile(object target, File file)
        {
            if (target is LottieView lottie)
            {
                lottie.Source = UriEx.ToLocal(file.Local.Path);
            }
        }

        private void OnViewChanging(object sender, ScrollViewerViewChangingEventArgs e)
        {
            var j = (int)Math.Floor((e.NextView.HorizontalOffset - 8) / 34);
            var k = (int)Math.Ceiling(((e.NextView.HorizontalOffset - 8) + Shadow.ActualWidth) / 34);

            for (int i = 0; i < Presenter.Children.Count; i++)
            {
                var view = Presenter.Children[i] as LottieView;
                if (view != null)
                {
                    if (view.Tag != null && i >= j && i < k)
                    {
                        view.Play();
                        view.Tag = null;
                    }
                    else if (view.Tag == null && (i < j || i >= k))
                    {
                        view.Tag = new object();
                    }
                }
            }
        }
    }
}
