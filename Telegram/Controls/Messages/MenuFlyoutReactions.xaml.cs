//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using LinqToVisualTree;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Geometry;
using System;
using System.Linq;
using System.Numerics;
using Telegram.Common;
using Telegram.Controls.Drawers;
using Telegram.Services;
using Telegram.Streams;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Telegram.ViewModels.Drawers;
using Telegram.Views.Popups;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media;
using Point = Windows.Foundation.Point;

namespace Telegram.Controls.Messages
{
    public sealed partial class MenuFlyoutReactions : UserControl
    {
        private readonly IClientService _clientService;
        private readonly EmojiDrawerMode _mode;

        private readonly AvailableReactions _reactions;
        private readonly bool _canUnlockMore;

        private readonly MessageViewModel _message;
        private readonly MessageBubble _bubble;
        private readonly MenuFlyout _flyout;

        private readonly MenuFlyoutPresenter _presenter;
        private readonly Popup _popup;

        public static MenuFlyoutReactions ShowAt(AvailableReactions reactions, MessageViewModel message, MessageBubble bubble, MenuFlyout flyout)
        {
            return new MenuFlyoutReactions(reactions, message, bubble, flyout);
        }

        private MenuFlyoutReactions(AvailableReactions reactions, MessageViewModel message, MessageBubble bubble, MenuFlyout flyout)
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

            var source = reactions.TopReactions.ToList();
            if (source.Count < 8)
            {
                var additional = reactions.RecentReactions.Count > 0
                    ? reactions.RecentReactions
                    : reactions.PopularReactions;

                reactions.TopReactions
                    .Select(x => x.Type)
                    .Discern(out var emoji, out var customEmoji);

                foreach (var item in additional)
                {
                    if (item.Type is ReactionTypeEmoji emojii
                        && emoji != null
                        && emoji.Contains(emojii.Emoji))
                    {
                        continue;
                    }
                    else if (item.Type is ReactionTypeCustomEmoji customEmojii
                        && customEmoji != null
                        && customEmoji.Contains(customEmojii.CustomEmojiId))
                    {
                        continue;
                    }

                    source.Add(item);
                }
            }

            var hasMore = source.Count > 7 || reactions.AllowCustomEmoji;
            var count = Math.Min(source.Count, 8);

            while (source.Count > 7)
            {
                source.RemoveAt(source.Count - 1);
            }

            var itemSize = 24;
            var itemPadding = 4;

            var itemTotal = itemSize + itemPadding;

            var cols = count;
            var rows = 8;

            var actualWidth = presenter.ActualSize.X + 18 + 12 + 18;
            var width = Math.Max(36 + 14, 8 + 4 + (cols * itemTotal));
            var viewport = Math.Max(36 + 14, 8 + 4 + (cols * itemTotal));
            var height = rows * itemTotal;

            var padding = actualWidth - width;

            var viewModel = EmojiDrawerViewModel.GetForCurrentView(_message.ClientService.SessionId, EmojiDrawerMode.Reactions);
            var view = new EmojiDrawer(EmojiDrawerMode.Reactions);
            view.DataContext = viewModel;
            view.VerticalAlignment = VerticalAlignment.Top;
            view.Width = width;
            view.Height = height;
            view.Margin = new Thickness(itemTotal == 1 ? 5 : 0, -40, 0, 0);
            view.IsShadowVisible = false;
            view.ItemClick += OnItemClick;

            Presenter.Children.Add(view);
            _ = viewModel.UpdateReactions(_reactions, source);

            Shadow.Width = width;
            Pill.Width = width;
            Presenter.Width = width;

            Pill.VerticalAlignment = VerticalAlignment.Top;
            Pill.Height = Shadow.Height = 36 + 20;
            Pill.Margin = Shadow.Margin = new Thickness(0, 0, 0, -20);

            Expand.Visibility = hasMore ? Visibility.Visible : Visibility.Collapsed;

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

            LayoutRoot.Padding = new Thickness(16, 40, 16, 32);

            var offset = 0;
            var visible = hasMore ? 7 : 8; //Math.Ceiling((width - 8) / 34);

            var device = CanvasDevice.GetSharedDevice();
            var rect1 = CanvasGeometry.CreateRectangle(device, Math.Min(width - actualWidth, 0), 0, Math.Max(width + 16 + 16 + Math.Max(0, padding), actualWidth), 860);
            var elli1 = CanvasGeometry.CreateRoundedRectangle(device, width - actualWidth + 18 + 16, 40 + 36 + 4, presenter.ActualSize.X, 860, 8, 8);
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
            var y = position.Y - (40 + 4);

            _popup.Child = this;
            _popup.Margin = new Thickness(x - 16, y - 36, 0, 0);
            _popup.RequestedTheme = presenter.ActualTheme;
            _popup.ShouldConstrainToRootBounds = false;
            _popup.AllowFocusOnInteraction = false;
            _popup.IsOpen = true;

            var visualPill = ElementCompositionPreview.GetElementVisual(Pill);
            visualPill.CenterPoint = new Vector3(36 / 2, 36 / 2, 0);
            visualPill.CenterPoint = new Vector3(width - 36 / 2, 36 / 2, 0);

            var visualExpand = ElementCompositionPreview.GetElementVisual(Expand);
            visualExpand.CenterPoint = new Vector3(32 / 2f, 24 / 2f, 0);

            var clip = compositor.CreateRoundedRectangleGeometry();
            clip.CornerRadius = new Vector2(36 / 2);

            var batch = compositor.CreateScopedBatch(CompositionBatchTypes.Animation);

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
            translation.DelayTime = TimeSpan.FromMilliseconds(150 + 100);
            opacity.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;
            opacity.DelayTime = TimeSpan.FromMilliseconds(150 + 100);

            pillShadow.StartAnimation("BlurRadius", translation);
            pillShadow.StartAnimation("Opacity", opacity);

            var resize = compositor.CreateVector2KeyFrameAnimation();
            resize.InsertKeyFrame(0, new Vector2(36, 36));
            resize.InsertKeyFrame(1, new Vector2(width, 36));
            resize.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;
            resize.DelayTime = TimeSpan.FromMilliseconds(100);
            resize.Duration = Constants.FastAnimation;

            var move = compositor.CreateVector2KeyFrameAnimation();
            move.InsertKeyFrame(0, new Vector2(width - 36, 40));
            move.InsertKeyFrame(1, new Vector2(0, 40));
            move.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;
            move.DelayTime = TimeSpan.FromMilliseconds(100);
            move.Duration = Constants.FastAnimation;

            var viewVisual = ElementCompositionPreview.GetElementVisual(view);
            viewVisual.Clip = compositor.CreateGeometricClip(clip);
            clip.StartAnimation("Size", resize);
            clip.StartAnimation("Offset", move);

            batch.End();

            presenter.Unloaded += Presenter_Unloaded;
        }

        private void Presenter_Unloaded(object sender, RoutedEventArgs e)
        {
            _popup.IsOpen = false;
        }

        private void Reaction_Click(object sender, RoutedEventArgs e)
        {
            _flyout.Hide();

            if (sender is HyperlinkButton button && button.Tag is ReactionType reaction)
            {
                ToggleReaction(reaction);
            }
        }

        private async void ToggleReaction(ReactionType reaction)
        {
            if (_message.InteractionInfo != null && _message.InteractionInfo.Reactions.IsChosen(reaction))
            {
                _message.ClientService.Send(new RemoveMessageReaction(_message.ChatId, _message.Id, reaction));
            }
            else
            {
                await _message.ClientService.SendAsync(new AddMessageReaction(_message.ChatId, _message.Id, reaction, false, true));

                if (_bubble != null && _bubble.IsLoaded)
                {
                    var unread = new UnreadReaction(reaction, null, false);

                    _message.UnreadReactions.Add(unread);
                    _bubble.UpdateMessageReactions(_message, true);
                    _message.UnreadReactions.Remove(unread);
                }
            }
        }

        private void UpdateFile(object target, File file)
        {
            if (target is LottieView lottie)
            {
                lottie.Source = new LocalFileSource(file);
            }
            else if (target is AnimationView animation)
            {
                animation.Source = new LocalFileSource(file);
            }
            else if (target is Image image)
            {
                image.Source = PlaceholderHelper.GetWebPFrame(file.Local.Path, 24);
            }
        }

        private void Expand_Click(object sender, RoutedEventArgs e)
        {
            var itemSize = 24;
            var itemPadding = 4;

            var itemTotal = itemSize + itemPadding;

            var cols = 8;
            var rows = 8;

            var width = 8 + 4 + (cols * itemTotal);
            var viewport = 8 + 4 + (cols * itemTotal);
            var height = rows * itemTotal;

            Presenter.HorizontalAlignment = HorizontalAlignment.Left;
            Presenter.VerticalAlignment = VerticalAlignment.Top;

            Shadow.Height = height;
            Pill.Height = height;
            Presenter.Height = height;

            Expand.Visibility = Visibility.Collapsed;

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
            resize.Duration = Constants.FastAnimation;

            clip.StartAnimation("Size", resize);

            ElementCompositionPreview.SetIsTranslationEnabled(Presenter, true);

            // Animating this breaks the menu flyout when it comes back
            _presenter.Visibility = Visibility.Collapsed;

            //var viewModel = EmojiDrawerViewModel.GetForCurrentView(_message.ClientService.SessionId, EmojiDrawerMode.Reactions);
            //var view = new EmojiDrawer(EmojiDrawerMode.Reactions);
            //view.DataContext = viewModel;
            //view.VerticalAlignment = VerticalAlignment.Top;
            //view.Width = width;
            //view.Height = height;
            //view.ItemClick += OnItemClick;

            //Container.Children.Add(view);
            //_ = viewModel.UpdateReactions(_reactions);
            var view = Presenter.Children[0] as EmojiDrawer;
            var viewModel = view.ViewModel;

            var viewVisual = ElementCompositionPreview.GetElementVisual(view);
            viewVisual.Clip = null;

            view.IsShadowVisible = _reactions.AllowCustomEmoji;
            view.Margin = new Thickness();
            Presenter.Margin = new Thickness(0, -40, 0, 0);

            _ = viewModel.UpdateAsync();

            var clipDrawer = compositor.CreateRoundedRectangleGeometry();
            clipDrawer.CornerRadius = new Vector2(36 / 2);

            var drawer = ElementCompositionPreview.GetElementVisual(Presenter);
            drawer.CenterPoint = new Vector3(width - 36 / 2, 36 / 2, 0);
            drawer.Clip = compositor.CreateGeometricClip(clipDrawer);

            clipDrawer.StartAnimation("Size", resize);

            if (_reactions.AllowCustomEmoji)
            {
                var offset = compositor.CreateVector3KeyFrameAnimation();
                offset.InsertKeyFrame(0, Vector3.Zero);
                offset.InsertKeyFrame(1, new Vector3(0, -40, 0));

                ElementCompositionPreview.SetIsTranslationEnabled(view, true);
                ElementCompositionPreview.SetIsTranslationEnabled(Pill, true);
                //scrollingVisual.StartAnimation("Translation", offset);
                pillReceiver.StartAnimation("Offset", offset);
                visualPill.StartAnimation("Translation", offset);

                var offset2 = compositor.CreateVector2KeyFrameAnimation();
                offset2.InsertKeyFrame(0, new Vector2(0, 40));
                offset2.InsertKeyFrame(1, Vector2.Zero);
                clipDrawer.StartAnimation("Offset", offset2);
            }

            batch.End();
        }

        private void OnItemClick(object sender, ItemClickEventArgs e)
        {
            _flyout.Hide();

            if (e.ClickedItem is StickerViewModel sticker)
            {
                ToggleReaction(sticker.ToReactionType());
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

            if (_mode == EmojiDrawerMode.CustomEmojis)
            {
                view.ItemContextRequested += OnStatusContextRequested;
            }

            Presenter.Children.Add(view);

            if (mode == EmojiDrawerMode.CustomEmojis)
            {
                viewModel.UpdateStatuses();
            }
            else if (mode == EmojiDrawerMode.Reactions)
            {
                _ = viewModel.UpdateReactions(_reactions, null);
            }

            Shadow.Width = width;
            Pill.Width = width;
            Presenter.Width = width;

            Shadow.Height = height;
            Pill.Height = height + 20;
            Presenter.Height = height;

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
            scaleMedium.Duration = Constants.FastAnimation;

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
            resize.Duration = Constants.FastAnimation;

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
                move.Duration = Constants.FastAnimation;

                clip.StartAnimation("Offset", move);
            }

            batch.End();
        }

        private void OnStatusContextRequested(UIElement sender, ItemContextRequestedEventArgs<Sticker> args)
        {
            var element = sender as FrameworkElement;
            var sticker = args.Item;

            if (sticker == null)
            {
                return;
            }

            var flyout = new MenuFlyout();
            flyout.CreateFlyoutItem(SetStatus, (sticker, 60 * 60), Strings.SetEmojiStatusUntil1Hour);
            flyout.CreateFlyoutItem(SetStatus, (sticker, 60 * 60 * 2), Strings.SetEmojiStatusUntil2Hours);
            flyout.CreateFlyoutItem(SetStatus, (sticker, 60 * 60 * 8), Strings.SetEmojiStatusUntil8Hours);
            flyout.CreateFlyoutItem(SetStatus, (sticker, 60 * 60 * 48), Strings.SetEmojiStatusUntil2Days);
            flyout.CreateFlyoutItem(ChooseStatus, sticker, Strings.SetEmojiStatusUntilOther);

            args.ShowAt(flyout, element);
        }

        private void OnStatusClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is StickerViewModel sticker)
            {
                _popup.IsOpen = false;

                if (_mode == EmojiDrawerMode.CustomEmojis && sticker.FullType is StickerFullTypeCustomEmoji customEmoji)
                {
                    _clientService.Send(new SetEmojiStatus(new EmojiStatus(customEmoji.CustomEmojiId), 0));
                }
                else if (_mode == EmojiDrawerMode.Reactions)
                {
                    _clientService.Send(new SetDefaultReactionType(sticker.ToReactionType()));
                }
            }
        }

        private void SetStatus((Sticker Sticker, int Duration) item)
        {
            if (_mode == EmojiDrawerMode.CustomEmojis && item.Sticker.FullType is StickerFullTypeCustomEmoji customEmoji)
            {
                _clientService.Send(new SetEmojiStatus(new EmojiStatus(customEmoji.CustomEmojiId), item.Duration));
            }
        }

        private async void ChooseStatus(Sticker sticker)
        {
            var popup = new ChooseStatusDurationPopup();

            var confirm = await popup.ShowQueuedAsync();
            if (confirm == ContentDialogResult.Primary && _mode == EmojiDrawerMode.CustomEmojis && sticker.FullType is StickerFullTypeCustomEmoji customEmoji)
            {
                _clientService.Send(new SetEmojiStatus(new EmojiStatus(customEmoji.CustomEmojiId), popup.Value));
            }
        }
    }
}
