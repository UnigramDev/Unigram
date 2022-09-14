using LinqToVisualTree;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Controls.Drawers;
using Unigram.Services;
using Unigram.ViewModels;
using Unigram.ViewModels.Drawers;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media;
using Point = Windows.Foundation.Point;

namespace Unigram.Controls.Messages
{
    public sealed partial class MenuFlyoutReactions : UserControl
    {
        private readonly IClientService _clientService;
        private readonly EmojiDrawerMode _mode;

        private readonly IList<ReactionType> _reactions;
        private readonly bool _canUnlockMore;

        private readonly MessageViewModel _message;
        private readonly MessageBubble _bubble;
        private readonly MenuFlyout _flyout;

        private readonly MenuFlyoutPresenter _presenter;
        private readonly Popup _popup;

        public static MenuFlyoutReactions ShowAt(IList<ReactionType> reactions, MessageViewModel message, MessageBubble bubble, MenuFlyout flyout)
        {
            return new MenuFlyoutReactions(reactions, message, bubble, flyout);
        }

        private MenuFlyoutReactions(IList<ReactionType> reactions, MessageViewModel message, MessageBubble bubble, MenuFlyout flyout)
        {
            _reactions = reactions /*message.ClientService.IsPremium ? reactions : reactions.Where(x => !x.IsPremium).ToList()*/;
            _canUnlockMore = false; //message.ClientService.IsPremiumAvailable && !message.ClientService.IsPremium && reactions.Any(x => x.IsPremium);
            _message = message;
            _bubble = bubble;
            _flyout = flyout;

            InitializeComponent();

            var last = flyout.Items.LastOrDefault();
            var presenter = last.Ancestors<MenuFlyoutPresenter>().FirstOrDefault();

            _presenter = presenter;
            _popup = new Popup();

            var transform = presenter.TransformToVisual(Window.Current.Content);
            var position = transform.TransformPoint(new Point());

            var count = Math.Min(_reactions.Count, 8);

            var itemSize = 24;
            var itemPadding = 4;

            var itemTotal = itemSize + itemPadding;

            var actualWidth = presenter.ActualSize.X + 18 + 12 + 18;
            var width = 8 + 4 + (count * itemTotal);

            var padding = actualWidth - width;

            Shadow.Width = width;
            Pill.Width = width;
            Presenter.Width = Container.Width = width;

            Pill.VerticalAlignment = VerticalAlignment.Top;
            Pill.Height = Shadow.Height = 36 + 20;
            Pill.Margin = Shadow.Margin = new Thickness(0, 0, 0, -20);

            Expand.Visibility = _reactions.Count > 6 ? Visibility.Visible : Visibility.Collapsed;

            var yy = 20f;

            var figure = new PathFigure();
            figure.StartPoint = new Point(18, 0);
            //figure.Segments.Add(new LineSegment { Point = new Point(18 + 20, yy + 0) });


            figure.Segments.Add(new LineSegment { Point = new Point(width - 18, 0) });
            figure.Segments.Add(new ArcSegment { Point = new Point(width - 18, 36), Size = new Size(18, 18), RotationAngle = 180, SweepDirection = SweepDirection.Clockwise });
            
            figure.Segments.Add(new ArcSegment { Point = new Point(width - 18 - 14, 36), Size = new Size(7, 7), RotationAngle = 180, SweepDirection = SweepDirection.Clockwise });
            
            figure.Segments.Add(new LineSegment { Point = new Point(18, 36) });
            figure.Segments.Add(new ArcSegment { Point = new Point(18, 0), Size = new Size(18, 18), RotationAngle = 180, SweepDirection = SweepDirection.Clockwise });

            var path = new PathGeometry();
            path.Figures.Add(figure);

            var data = new GeometryGroup();
            data.FillRule = FillRule.Nonzero;
            data.Children.Add(path);
            data.Children.Add(new EllipseGeometry { Center = new Point(width - 18 - 8 + 5, 36 + 20 - 7), RadiusX = 3.5f, RadiusY = 3.5f });

            Pill.Data = data;

            LayoutRoot.Padding = new Thickness(16, 36, 16, 32);

            var offset = 0;
            var visible = _reactions.Count > 8 ? 7 : 8; //Math.Ceiling((width - 8) / 34);

            static void DownloadFile(MessageViewModel message, File file)
            {
                if (file != null && file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive && !file.Local.IsDownloadingCompleted)
                {
                    message.ClientService.DownloadFile(file.Id, 31);
                }
            }

            //foreach (var item in _reactions)
            //{
            //    // Pre-download additional assets
            //    DownloadFile(message, item.CenterAnimation?.StickerValue);
            //    DownloadFile(message, item.AroundAnimation?.StickerValue);

            //    if (offset >= visible)
            //    {
            //        DownloadFile(message, item.AppearAnimation.StickerValue);
            //        break;
            //    }

            //    var view = new LottieView();
            //    view.AutoPlay = offset < visible;
            //    view.IsLoopingEnabled = false;
            //    view.FrameSize = new Size(itemSize, itemSize);
            //    view.DecodeFrameType = DecodePixelType.Logical;
            //    view.Width = itemSize;
            //    view.Height = itemSize;
            //    view.Margin = new Thickness(0, 0, itemPadding, 0);
            //    view.VerticalAlignment = VerticalAlignment.Top;
            //    view.Tag = offset < visible ? null : new object();

            //    var file = item.AppearAnimation.StickerValue;
            //    if (file.Local.IsDownloadingCompleted)
            //    {
            //        view.Source = UriEx.ToLocal(file.Local.Path);
            //    }
            //    else
            //    {
            //        view.Source = null;

            //        UpdateManager.Subscribe(view, message, file, /*UpdateReaction*/UpdateFile, true);

            //        if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive)
            //        {
            //            message.ClientService.DownloadFile(file.Id, 32);
            //        }
            //    }

            //    Grid.SetColumn(view, offset);

            //    Presenter.Children.Add(view);
            //    Presenter.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });

            //    if (offset < visible)
            //    {
            //        var visual = ElementCompositionPreview.GetElementVisual(view);
            //        visual.CenterPoint = new Vector3(12, 12, 0);
            //        visual.Scale = Vector3.Zero;

            //        var scale = visual.Compositor.CreateVector3KeyFrameAnimation();
            //        scale.InsertKeyFrame(0, Vector3.Zero);
            //        scale.InsertKeyFrame(1, Vector3.One);
            //        scale.DelayTime = TimeSpan.FromMilliseconds(50 * (visible - Presenter.Children.Count));

            //        visual.StartAnimation("Scale", scale);
            //    }

            //    offset++;
            //}

            var device = CanvasDevice.GetSharedDevice();
            var rect1 = CanvasGeometry.CreateRectangle(device, Math.Min(width - actualWidth, 0), 0, Math.Max(width + 16 + 16 + Math.Max(0, padding), actualWidth), 860);
            var elli1 = CanvasGeometry.CreateRoundedRectangle(device, width - actualWidth + 18 + 16, 36 + 36 + 4, presenter.ActualSize.X, 860, 8, 8);
            var group1 = CanvasGeometry.CreateGroup(device, new[] { elli1, rect1 }, CanvasFilledRegionDetermination.Alternate);

            var rootVisual = ElementCompositionPreview.GetElementVisual(LayoutRoot);
            var compositor = rootVisual.Compositor;
            rootVisual.Clip = rootVisual.Compositor.CreateGeometricClip(rootVisual.Compositor.CreatePathGeometry(new CompositionPath(group1)));

            var pillShadow = compositor.CreateDropShadow();
            pillShadow.BlurRadius = 16;
            pillShadow.Opacity = 0.14f;
            pillShadow.Color = Colors.Black;
            pillShadow.Mask = Pill.GetAlphaMask();

            var pillReceiver = compositor.CreateSpriteVisual();
            pillReceiver.Shadow = pillShadow;
            pillReceiver.Size = new Vector2(width, 36 + 20);
            pillReceiver.Offset = new Vector3(0, 8, 0);

            ElementCompositionPreview.SetElementChildVisual(Shadow, pillReceiver);

            var x = position.X - 18 + padding;
            var y = position.Y - (36 + 4);

            _popup.Child = this;
            _popup.Margin = new Thickness(x - 16, y - 36, 0, 0);
            _popup.ShouldConstrainToRootBounds = false;
            _popup.RequestedTheme = presenter.ActualTheme;
            _popup.IsOpen = true;

            var visualPill = ElementCompositionPreview.GetElementVisual(Pill);
            visualPill.CenterPoint = new Vector3(36 / 2, 36 / 2, 0);
            visualPill.CenterPoint = new Vector3(width - 36 / 2, 36 / 2, 0);

            var visualExpand = ElementCompositionPreview.GetElementVisual(Expand);
            visualExpand.CenterPoint = new Vector3(32 / 2f, 24 / 2f, 0);

            var clip = compositor.CreateRoundedRectangleGeometry();
            clip.CornerRadius = new Vector2(36 / 2);

            var batch = compositor.CreateScopedBatch(CompositionBatchTypes.Animation);

            var scaleMedium = compositor.CreateVector3KeyFrameAnimation();
            scaleMedium.InsertKeyFrame(0, Vector3.Zero);
            scaleMedium.InsertKeyFrame(1, Vector3.One);
            scaleMedium.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;
            scaleMedium.DelayTime = TimeSpan.FromMilliseconds(100);
            scaleMedium.Duration = TimeSpan.FromMilliseconds(150);

            var scalePill = compositor.CreateSpringVector3Animation();
            scalePill.InitialValue = Vector3.Zero;
            scalePill.FinalValue = Vector3.One;
            scalePill.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;
            scalePill.DampingRatio = 0.7f;

            var translation = compositor.CreateScalarKeyFrameAnimation();
            translation.InsertKeyFrame(0, 0);
            translation.InsertKeyFrame(1, 16);

            var opacity = compositor.CreateScalarKeyFrameAnimation();
            opacity.InsertKeyFrame(0, 0);
            opacity.InsertKeyFrame(1, 0.14f);

            visualPill.StartAnimation("Scale", scalePill);
            visualExpand.StartAnimation("Scale", scalePill);

            translation.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;
            translation.DelayTime = scaleMedium.Duration + TimeSpan.FromMilliseconds(100);
            opacity.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;
            opacity.DelayTime = scaleMedium.Duration + TimeSpan.FromMilliseconds(100);

            pillShadow.StartAnimation("BlurRadius", translation);
            pillShadow.StartAnimation("Opacity", opacity);

            var resize = compositor.CreateVector2KeyFrameAnimation();
            resize.InsertKeyFrame(0, new Vector2(36, 36));
            resize.InsertKeyFrame(1, new Vector2(width, 36));
            resize.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;
            resize.DelayTime = TimeSpan.FromMilliseconds(100);
            resize.Duration = TimeSpan.FromMilliseconds(150);

            var move = compositor.CreateVector2KeyFrameAnimation();
            move.InsertKeyFrame(0, new Vector2(width - 36, 0));
            move.InsertKeyFrame(1, new Vector2());
            move.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;
            move.DelayTime = TimeSpan.FromMilliseconds(100);
            move.Duration = TimeSpan.FromMilliseconds(150);

            //visualPill.Clip = compositor.CreateGeometricClip(clip);
            clip.StartAnimation("Size", resize);
            clip.StartAnimation("Offset", move);

            batch.End();

            presenter.Unloaded += Presenter_Unloaded;
        }

        private void Presenter_Unloaded(object sender, RoutedEventArgs e)
        {
            _popup.IsOpen = false;
        }

        private async void Reaction_Click(object sender, RoutedEventArgs e)
        {
            if (sender is HyperlinkButton button && button.Tag is ReactionType reaction)
            {
                _flyout.Hide();
                await _message.ClientService.SendAsync(new SetMessageReaction(_message.ChatId, _message.Id, reaction, false, true));

                if (_bubble != null)
                {
                    _bubble.UpdateMessageReactions(_message, true);
                }
            }
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
            var k = (int)Math.Ceiling((e.NextView.HorizontalOffset - 8 + Shadow.ActualWidth) / 34);

            for (int i = 0; i < Presenter.Children.Count; i++)
            {
                var button = Presenter.Children[i] as HyperlinkButton;
                if (button.Content is LottieView view)
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

        private void Expand_Click(object sender, RoutedEventArgs e)
        {
            var itemSize = 24;
            var itemPadding = 4;

            var itemTotal = itemSize + itemPadding;

            var cols = 8;
            var rows = (int)Math.Ceiling((double)_reactions.Count / cols);

            var width = 8 + 4 + (cols * itemTotal);
            var viewport = 8 + 4 + (cols * itemTotal);
            var height = (rows + 3) * itemTotal;

            Presenter.HorizontalAlignment = HorizontalAlignment.Left;
            Presenter.VerticalAlignment = VerticalAlignment.Top;

            Shadow.Height = height;
            Pill.Height = height;
            Presenter.Height = Container.Height = height;

            var figure = new PathFigure();
            figure.StartPoint = new Point(18, 0);
            figure.Segments.Add(new LineSegment { Point = new Point(width - 18, 0) });
            figure.Segments.Add(new ArcSegment { Point = new Point(width, 18), Size = new Size(18, 18), RotationAngle = 90, SweepDirection = SweepDirection.Clockwise });
            figure.Segments.Add(new LineSegment { Point = new Point(width, height - 18) });
            figure.Segments.Add(new ArcSegment { Point = new Point(width - 18, height), Size = new Size(18, 18), RotationAngle = 90, SweepDirection = SweepDirection.Clockwise });
            figure.Segments.Add(new LineSegment { Point = new Point(18, height) });
            figure.Segments.Add(new ArcSegment { Point = new Point(0, height - 18), Size = new Size(18, 18), RotationAngle = 90, SweepDirection = SweepDirection.Clockwise });
            figure.Segments.Add(new LineSegment { Point = new Point(0, 18) });
            figure.Segments.Add(new ArcSegment { Point = new Point(18, 0), Size = new Size(18, 18), RotationAngle = 90, SweepDirection = SweepDirection.Clockwise });

            var path = new PathGeometry();
            path.Figures.Add(figure);

            var data = new GeometryGroup();
            data.FillRule = FillRule.Nonzero;
            data.Children.Add(path);

            Pill.Data = data;

            var rootVisual = ElementCompositionPreview.GetElementVisual(LayoutRoot);
            var compositor = rootVisual.Compositor;

            var batch = compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
            batch.Completed += (s, args) =>
            {
                Presenter.Children.Clear();
            };

            var pillShadow = compositor.CreateDropShadow();
            pillShadow.BlurRadius = 16;
            pillShadow.Opacity = 0.14f;
            pillShadow.Color = Colors.Black;
            pillShadow.Mask = Pill.GetAlphaMask();

            var pillReceiver = compositor.CreateSpriteVisual();
            pillReceiver.Shadow = pillShadow;
            pillReceiver.Size = new Vector2(viewport, height);
            pillReceiver.Offset = new Vector3(0, 8, 0);

            ElementCompositionPreview.SetElementChildVisual(Shadow, pillReceiver);

            rootVisual.Clip = null;

            var clip = compositor.CreateRoundedRectangleGeometry();
            clip.CornerRadius = new Vector2(36 / 2);

            var visualPill = ElementCompositionPreview.GetElementVisual(Pill);
            visualPill.Clip = compositor.CreateGeometricClip(clip);

            var resize = compositor.CreateVector2KeyFrameAnimation();
            resize.InsertKeyFrame(0, new Vector2(width, 36));
            resize.InsertKeyFrame(1, new Vector2(width, height));
            resize.Duration = TimeSpan.FromMilliseconds(150);

            clip.StartAnimation("Size", resize);

            var offset = compositor.CreateVector3KeyFrameAnimation();
            offset.InsertKeyFrame(0, Vector3.Zero);
            offset.InsertKeyFrame(1, new Vector3(0, 36, 0));

            var opacity = compositor.CreateScalarKeyFrameAnimation();
            opacity.InsertKeyFrame(0, 1);
            opacity.InsertKeyFrame(1, 0);

            ElementCompositionPreview.SetIsTranslationEnabled(Expand, true);
            var expand = ElementCompositionPreview.GetElementVisual(Expand);
            expand.StartAnimation("Translation", offset);
            expand.StartAnimation("Opacity", opacity);

            ElementCompositionPreview.SetIsTranslationEnabled(Presenter, true);
            var scrollingVisual = ElementCompositionPreview.GetElementVisual(Presenter);

            // Animating this breaks the menu flyout when it comes back
            _presenter.Visibility = Visibility.Collapsed;

            var viewModel = EmojiDrawerViewModel.GetForCurrentView(_message.ClientService.SessionId, EmojiDrawerMode.Reactions);
            var view = new EmojiDrawer(EmojiDrawerMode.Reactions);
            view.DataContext = viewModel;
            view.VerticalAlignment = VerticalAlignment.Top;
            view.Width = width;
            view.Height = height;
            view.ItemClick += OnItemClick;

            Container.Children.Add(view);
            _ = viewModel.UpdateReactions();

            offset = compositor.CreateVector3KeyFrameAnimation();
            offset.InsertKeyFrame(0, Vector3.Zero);
            offset.InsertKeyFrame(1, new Vector3(0, -36, 0));

            ElementCompositionPreview.SetIsTranslationEnabled(view, true);
            ElementCompositionPreview.SetIsTranslationEnabled(Pill, true);
            scrollingVisual = ElementCompositionPreview.GetElementVisual(view);
            //scrollingVisual.StartAnimation("Translation", offset);
            pillReceiver.StartAnimation("Offset", offset);
            visualPill.StartAnimation("Translation", offset);

            var drawer = ElementCompositionPreview.GetElementVisual(view);
            drawer.CenterPoint = new Vector3(width - 36 / 2, 36 / 2, 0);
            drawer.Clip = compositor.CreateGeometricClip(clip);

            batch.End();
        }

        private async void OnItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is StickerViewModel sticker)
            {
                _flyout.Hide();

                if (sticker.CustomEmojiId != 0)
                {
                    await _message.ClientService.SendAsync(new SetMessageReaction(_message.ChatId, _message.Id, new ReactionTypeCustomEmoji(sticker.CustomEmojiId), false, true));
                }
                else
                {
                    await _message.ClientService.SendAsync(new SetMessageReaction(_message.ChatId, _message.Id, new ReactionTypeEmoji(sticker.Emoji), false, true));
                }

                if (_bubble != null)
                {
                    _bubble.UpdateMessageReactions(_message, true);
                }
            }
        }
























        public static MenuFlyoutReactions ShowAt(IClientService clientService, EmojiDrawerMode mode, FrameworkElement element, HorizontalAlignment alignment)
        {
            return new MenuFlyoutReactions(clientService, mode, element, alignment);
        }

        private MenuFlyoutReactions(IClientService clientService, EmojiDrawerMode mode, FrameworkElement element, HorizontalAlignment alignment)
        {
            InitializeComponent();

            _clientService = clientService;
            _mode = mode;

            _popup = new Popup();

            var transform = element.TransformToVisual(Window.Current.Content);
            var position = transform.TransformPoint(new Point());

            var count = 8;

            var itemSize = 24;
            var itemPadding = 4;

            var itemTotal = itemSize + itemPadding;

            var actualWidth = 8 + 4 + (count * itemTotal);

            var cols = 8;
            var rows = 8;

            var width = 8 + 4 + (cols * itemTotal);
            var viewport = 8 + 4 + (cols * itemTotal);
            var height = rows * itemTotal;

            var padding = actualWidth - width;

            var viewModel = EmojiDrawerViewModel.GetForCurrentView(clientService.SessionId, mode);
            var view = new EmojiDrawer(mode);
            view.DataContext = viewModel;
            view.VerticalAlignment = VerticalAlignment.Top;
            view.Width = width;
            view.Height = height;
            view.ItemClick += OnStatusClick;

            Container.Margin = new Thickness();
            Container.Children.Add(view);

            if (mode == EmojiDrawerMode.CustomEmojis)
            {
                viewModel.UpdateStatuses();
            }
            else if (mode == EmojiDrawerMode.Reactions)
            {
                _ = viewModel.UpdateReactions();
            }

            Shadow.Width = width;
            Pill.Width = width;
            Presenter.Width = Container.Width = width;

            Shadow.Height = height;
            Pill.Height = height + 20;
            Presenter.Height = Container.Height = height;

            var yy = 20f;

            var figure = new PathFigure();
            figure.StartPoint = new Point(18, yy);

            if (alignment == HorizontalAlignment.Left)
            {
                figure.Segments.Add(new LineSegment { Point = new Point(18 + 20, yy + 0) });
                figure.Segments.Add(new ArcSegment { Point = new Point(18 + 20 + 14, yy + 0), Size = new Size(7, 7), RotationAngle = 180, SweepDirection = SweepDirection.Clockwise });
            }
            else if (alignment == HorizontalAlignment.Right)
            {
                figure.Segments.Add(new LineSegment { Point = new Point(width - 18 - 20 - 14, yy + 0) });
                figure.Segments.Add(new ArcSegment { Point = new Point(width - 18 - 20, yy + 0), Size = new Size(7, 7), RotationAngle = 180, SweepDirection = SweepDirection.Clockwise });
            }

            figure.Segments.Add(new LineSegment { Point = new Point(width - 18, yy + 0) });
            figure.Segments.Add(new ArcSegment { Point = new Point(width, yy + 18), Size = new Size(18, 18), RotationAngle = 90, SweepDirection = SweepDirection.Clockwise });
            figure.Segments.Add(new LineSegment { Point = new Point(width, yy + height - 18) });
            figure.Segments.Add(new ArcSegment { Point = new Point(width - 18, yy + height), Size = new Size(18, 18), RotationAngle = 90, SweepDirection = SweepDirection.Clockwise });
            

            figure.Segments.Add(new LineSegment { Point = new Point(18, yy + height) });
            figure.Segments.Add(new ArcSegment { Point = new Point(0, yy + height - 18), Size = new Size(18, 18), RotationAngle = 90, SweepDirection = SweepDirection.Clockwise });
            figure.Segments.Add(new LineSegment { Point = new Point(0, yy + 18) });
            figure.Segments.Add(new ArcSegment { Point = new Point(18, yy + 0), Size = new Size(18, 18), RotationAngle = 90, SweepDirection = SweepDirection.Clockwise });

            var path = new PathGeometry();
            path.Figures.Add(figure);

            var data = new GeometryGroup();
            data.FillRule = FillRule.Nonzero;
            data.Children.Add(path);

            if (alignment == HorizontalAlignment.Left)
            {
                data.Children.Add(new EllipseGeometry { Center = new Point(20 + 18, 7), RadiusX = 3.5, RadiusY = 3.5 });
            }
            else if (alignment == HorizontalAlignment.Right)
            {
                data.Children.Add(new EllipseGeometry { Center = new Point(width - 20 - 18, 7), RadiusX = 3.5, RadiusY = 3.5 });
            }

            Pill.Data = data;
            Pill.Margin = new Thickness(0, -yy, 0, 0);
            Shadow.Margin = new Thickness(0, -yy, 0, 0);

            LayoutRoot.Padding = new Thickness(16, 36, 16, 16);

            var rootVisual = ElementCompositionPreview.GetElementVisual(LayoutRoot);
            var compositor = rootVisual.Compositor;

            var pillShadow = compositor.CreateDropShadow();
            pillShadow.BlurRadius = 16;
            pillShadow.Opacity = 0.14f;
            pillShadow.Color = Colors.Black;
            pillShadow.Mask = Pill.GetAlphaMask();

            var pillReceiver = compositor.CreateSpriteVisual();
            pillReceiver.Shadow = pillShadow;
            pillReceiver.Size = new Vector2(width, height + yy);
            pillReceiver.Offset = new Vector3(0, -yy + 8, 0);

            ElementCompositionPreview.SetElementChildVisual(Shadow, pillReceiver);

            var x = position.X /*- 18 + padding*/;
            var y = position.Y + element.ActualHeight - 4;

            if (alignment == HorizontalAlignment.Right)
            {
                x = position.X - width + element.ActualWidth + 6;
            }

            _popup.Child = this;
            _popup.Margin = new Thickness(x - 16, y - 36, 0, 0);
            _popup.ShouldConstrainToRootBounds = false;
            _popup.RequestedTheme = element.ActualTheme;
            _popup.IsLightDismissEnabled = true;
            _popup.IsOpen = true;

            var visualPill = ElementCompositionPreview.GetElementVisual(Pill);
            visualPill.CenterPoint = new Vector3(alignment == HorizontalAlignment.Left ? 36 / 2 : width - 36 / 2, yy + 36 / 2, 0);

            var visualExpand = ElementCompositionPreview.GetElementVisual(Expand);
            visualExpand.CenterPoint = new Vector3(32 / 2f, 24 / 2f, 0);

            var clip = compositor.CreateRoundedRectangleGeometry();
            clip.CornerRadius = new Vector2(36 / 2);

            var batch = compositor.CreateScopedBatch(CompositionBatchTypes.Animation);

            var scaleMedium = compositor.CreateVector3KeyFrameAnimation();
            scaleMedium.InsertKeyFrame(0, Vector3.Zero);
            scaleMedium.InsertKeyFrame(1, Vector3.One);
            scaleMedium.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;
            scaleMedium.DelayTime = TimeSpan.FromMilliseconds(100);
            scaleMedium.Duration = TimeSpan.FromMilliseconds(150);

            var scalePill = compositor.CreateSpringVector3Animation();
            scalePill.InitialValue = Vector3.Zero;
            scalePill.FinalValue = Vector3.One;
            scalePill.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;
            scalePill.DampingRatio = 0.6f;

            var translation = compositor.CreateScalarKeyFrameAnimation();
            translation.InsertKeyFrame(0, 0);
            translation.InsertKeyFrame(1, 16);

            var opacity = compositor.CreateScalarKeyFrameAnimation();

            var drawer = ElementCompositionPreview.GetElementVisual(view);

            opacity.InsertKeyFrame(0, 0);
            opacity.InsertKeyFrame(1, 1);

            drawer.CenterPoint = new Vector3(alignment == HorizontalAlignment.Left ? 36 / 2 : width - 36 / 2, 36 / 2, 0);
            drawer.StartAnimation("Opacity", opacity);
            drawer.StartAnimation("Scale", scalePill);

            opacity.InsertKeyFrame(0, 0);
            opacity.InsertKeyFrame(1, 0.14f);

            visualPill.StartAnimation("Scale", scalePill);
            visualExpand.StartAnimation("Scale", scalePill);

            translation.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;
            translation.DelayTime = scaleMedium.Duration + TimeSpan.FromMilliseconds(100);
            opacity.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;
            opacity.DelayTime = scaleMedium.Duration + TimeSpan.FromMilliseconds(100);

            pillShadow.StartAnimation("BlurRadius", translation);
            pillShadow.StartAnimation("Opacity", opacity);

            var resize = compositor.CreateVector2KeyFrameAnimation();
            resize.InsertKeyFrame(0, new Vector2(36, 36));
            resize.InsertKeyFrame(1, new Vector2(width, height + yy));
            resize.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;
            resize.DelayTime = TimeSpan.FromMilliseconds(100);
            resize.Duration = TimeSpan.FromMilliseconds(150);

            drawer.Clip = compositor.CreateGeometricClip(clip);
            visualPill.Clip = compositor.CreateGeometricClip(clip);
            clip.StartAnimation("Size", resize);

            if (alignment == HorizontalAlignment.Right)
            {
                var move = compositor.CreateVector2KeyFrameAnimation();
                move.InsertKeyFrame(0, new Vector2(width - 36, 0));
                move.InsertKeyFrame(1, new Vector2());
                move.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;
                move.DelayTime = TimeSpan.FromMilliseconds(100);
                move.Duration = TimeSpan.FromMilliseconds(150);

                clip.StartAnimation("Offset", move);
            }

            batch.End();
        }

        private void OnStatusClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is StickerViewModel sticker)
            {
                _popup.IsOpen = false;

                if (_mode == EmojiDrawerMode.CustomEmojis)
                {
                    _clientService.Send(new SetEmojiStatus(new EmojiStatus(sticker.CustomEmojiId), 0));
                }
                else if (_mode == EmojiDrawerMode.Reactions)
                {
                    if (sticker.CustomEmojiId != 0)
                    {
                        _clientService.Send(new SetDefaultReactionType(new ReactionTypeCustomEmoji(sticker.CustomEmojiId)));
                    }
                    else
                    {
                        _clientService.Send(new SetDefaultReactionType(new ReactionTypeEmoji(sticker.Emoji)));
                    }
                }
            }
        }
    }
}
