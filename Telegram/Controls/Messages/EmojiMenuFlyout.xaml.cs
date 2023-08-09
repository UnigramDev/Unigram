//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Numerics;
using Telegram.Common;
using Telegram.Controls.Drawers;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Telegram.ViewModels.Drawers;
using Telegram.ViewModels.Stories;
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
    public sealed partial class EmojiMenuFlyout : UserControl
    {
        private readonly IClientService _clientService;
        private readonly EmojiDrawerMode _mode;

        private readonly MessageViewModel _message;
        private readonly MessageBubble _bubble;

        private readonly StoryViewModel _story;
        private readonly FrameworkElement _reserved;

        private readonly Popup _popup;

        public static EmojiMenuFlyout ShowAt(IClientService clientService, EmojiDrawerMode mode, FrameworkElement element, HorizontalAlignment alignment, MessageViewModel message = null, AvailableReactions reactions = null)
        {
            return new EmojiMenuFlyout(clientService, mode, element, alignment, message, reactions);
        }

        private EmojiMenuFlyout(IClientService clientService, EmojiDrawerMode mode, FrameworkElement element, HorizontalAlignment alignment, MessageViewModel message = null, AvailableReactions reactions = null)
        {
            InitializeComponent();

            _clientService = clientService;
            _mode = mode;
            _message = message;

            _popup = new Popup();

            Initialize(clientService, element, alignment);
        }

        public static EmojiMenuFlyout ShowAt(IClientService clientService, FrameworkElement element, HorizontalAlignment alignment, StoryViewModel story, FrameworkElement reserved, AvailableReactions reactions)
        {
            return new EmojiMenuFlyout(clientService, element, alignment, story, reserved, reactions);
        }

        private EmojiMenuFlyout(IClientService clientService, FrameworkElement element, HorizontalAlignment alignment, StoryViewModel story, FrameworkElement reserved, AvailableReactions reactions)
        {
            InitializeComponent();

            _clientService = clientService;
            _mode = EmojiDrawerMode.Reactions;
            _story = story;
            _reserved = reserved;

            _popup = new Popup();

            Initialize(clientService, element, alignment);
        }

        private void Initialize(IClientService clientService, FrameworkElement element, HorizontalAlignment alignment)
        {
            var transform = element.TransformToVisual(Window.Current.Content);
            var position = transform.TransformPoint(new Point());

            var count = 8;

            var itemSize = 32;
            var itemPadding = 4;

            var itemTotal = itemSize + itemPadding;

            var actualWidth = 8 + 4 + (count * itemTotal);

            var cols = 8;
            var rows = 8;

            var width = 8 + 4 + (cols * itemTotal);
            var viewport = 8 + 4 + (cols * itemTotal);
            var height = rows * itemTotal;

            var padding = actualWidth - width;

            var viewModel = EmojiDrawerViewModel.GetForCurrentView(clientService.SessionId, _mode);
            var view = new EmojiDrawer(_mode);
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

            if (_mode == EmojiDrawerMode.CustomEmojis)
            {
                view.Activate(null, EmojiSearchType.EmojiStatus);
                viewModel.UpdateStatuses();
            }
            else if (_mode == EmojiDrawerMode.Reactions)
            {
                //_ = viewModel.UpdateReactions(reactions, null);
            }

            Shadow.Width = width;
            Pill.Width = width;
            Presenter.Width = width;

            Shadow.Height = height;
            Pill.Height = height + 20;
            Presenter.Padding = new Thickness(0, 20, 0, 0);
            Presenter.Height = height + 20;

            var yy = 20f;
            var radius = 8;

            var figure = new PathFigure();
            figure.StartPoint = new Point(radius, yy);

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

            figure.Segments.Add(new LineSegment { Point = new Point(width - radius, yy + 0) });
            figure.Segments.Add(new ArcSegment { Point = new Point(width, yy + radius), Size = new Size(radius, radius), RotationAngle = 90, SweepDirection = SweepDirection.Clockwise });
            figure.Segments.Add(new LineSegment { Point = new Point(width, yy + height - radius) });
            figure.Segments.Add(new ArcSegment { Point = new Point(width - radius, yy + height), Size = new Size(radius, radius), RotationAngle = 90, SweepDirection = SweepDirection.Clockwise });


            figure.Segments.Add(new LineSegment { Point = new Point(radius, yy + height) });
            figure.Segments.Add(new ArcSegment { Point = new Point(0, yy + height - radius), Size = new Size(radius, radius), RotationAngle = 90, SweepDirection = SweepDirection.Clockwise });
            figure.Segments.Add(new LineSegment { Point = new Point(0, yy + radius) });
            figure.Segments.Add(new ArcSegment { Point = new Point(radius, yy + 0), Size = new Size(radius, radius), RotationAngle = 90, SweepDirection = SweepDirection.Clockwise });

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
            Presenter.Margin = new Thickness(0, -yy, 0, 0);

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
            pillReceiver.Offset = new Vector3(0, -yy + 16, 0);

            ElementCompositionPreview.SetElementChildVisual(Shadow, pillReceiver);

            var x = position.X /*- 18 + padding*/;
            var y = position.Y + element.ActualHeight - 4;

            if (alignment == HorizontalAlignment.Right)
            {
                x = position.X - width + element.ActualWidth + 6;
            }
            else if (alignment == HorizontalAlignment.Center)
            {
                y = position.Y - 44;
                x = position.X - 8;
            }

            _popup.Child = this;
            _popup.Margin = new Thickness(x - 16, y - 36, 0, 0);
            _popup.ShouldConstrainToRootBounds = false;
            _popup.RequestedTheme = element.ActualTheme;
            _popup.IsLightDismissEnabled = true;
            _popup.IsOpen = true;
            //_popup.Opacity = 0.5;
            //_popup.Scale = new Vector3(28f / 32f);
            //_popup.CenterPoint = new Vector3(204, 148, 0);

            //return;

            var visualPill = ElementCompositionPreview.GetElementVisual(Pill);
            visualPill.CenterPoint = new Vector3(alignment == HorizontalAlignment.Left ? 36 / 2 : width - 36 / 2, yy + 36 / 2, 0);

            var visualExpand = ElementCompositionPreview.GetElementVisual(Expand);
            visualExpand.CenterPoint = new Vector3(32 / 2f, 24 / 2f, 0);

            var clip = compositor.CreateRoundedRectangleGeometry();
            clip.CornerRadius = new Vector2(8);
            clip.Offset = new Vector2(36, yy);

            var batch = compositor.CreateScopedBatch(CompositionBatchTypes.Animation);

            var opacity = compositor.CreateScalarKeyFrameAnimation();

            var drawer = ElementCompositionPreview.GetElementVisual(Presenter);

            opacity.InsertKeyFrame(0, 0);
            opacity.InsertKeyFrame(1, 0.24f);
            opacity.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;
            opacity.DelayTime = Constants.SoftAnimation;
            opacity.Duration = Constants.FastAnimation + TimeSpan.FromSeconds(0);

            pillShadow.StartAnimation("Opacity", opacity);

            var test = ElementCompositionPreview.GetElementVisual(LayoutRoot);
            test.CenterPoint = new Vector3(220, 138, 0); // 148
            drawer.Clip = compositor.CreateGeometricClip(clip);
            visualPill.Clip = compositor.CreateGeometricClip(clip);

            var ratio = 32f / 28f;

            var corner = compositor.CreateVector2KeyFrameAnimation();
            corner.InsertKeyFrame(0, new Vector2(20 * ratio));
            corner.InsertKeyFrame(1, new Vector2(8));
            corner.Duration = Constants.SoftAnimation + TimeSpan.FromSeconds(0);
            clip.StartAnimation("CornerRadius", corner);

            var scalePill = compositor.CreateVector3KeyFrameAnimation();
            scalePill.InsertKeyFrame(0, new Vector3(28f / 32f));
            scalePill.InsertKeyFrame(1, new Vector3(1));
            scalePill.Duration = Constants.FastAnimation + TimeSpan.FromSeconds(0);
            //visualPill.StartAnimation("Scale", scalePill);
            test.StartAnimation("Scale", scalePill);


            var resize = compositor.CreateVector2KeyFrameAnimation();
            if (alignment == HorizontalAlignment.Center)
            {
                resize.InsertKeyFrame(0, new Vector2(228, 40 * ratio));
                resize.InsertKeyFrame(1, new Vector2(width, height));
            }
            else
            {
                resize.InsertKeyFrame(0, new Vector2());
                resize.InsertKeyFrame(1, new Vector2(width, height + yy));
            }
            resize.Duration = Constants.SoftAnimation + TimeSpan.FromSeconds(0);

            clip.StartAnimation("Size", resize);

            var move = compositor.CreateVector2KeyFrameAnimation();
            if (alignment == HorizontalAlignment.Center)
            {
                move.InsertKeyFrame(0, new Vector2(0, yy + 82));
                move.InsertKeyFrame(1, new Vector2(0, yy));
            }
            else if (alignment == HorizontalAlignment.Right)
            {
                move.InsertKeyFrame(0, new Vector2(width - 36, yy));
                move.InsertKeyFrame(1, new Vector2());
            }
            else if (alignment == HorizontalAlignment.Left)
            {
                move.InsertKeyFrame(0, new Vector2(36, yy));
                move.InsertKeyFrame(1, new Vector2());
            }
            move.Duration = Constants.SoftAnimation + TimeSpan.FromSeconds(0);

            clip.StartAnimation("Offset", move);

            batch.End();




            // 3D experiment over here:
            return;

            var apothem = Apothem(24, 8);
            var angle = 360f / 24f;

            var centerX = width * 0.5f;
            var centerY = 8 * 0.5f;
            var depth = apothem;

            var rotation = 0f;

            var r = apothem;
            var d = apothem;

            var b = ToRadians(angle);
            var a = b * MathF.Abs(rotation);
            var x1 = r / d * 2 * MathF.Sin((b - a) / 2) * MathF.Sin(a / 2);

            var rotationFloat = rotation * angle;
            var scaleFloat = 1f - x1;

            var container = compositor.CreateContainerVisual();
            container.Size = new Vector2(width, 60);
            container.Offset = new Vector3(0, height - 8, 0);
            //container.CenterPoint = new Vector3(centerX, centerY, depth);

            for (int i = 0; i < 4; i++)
            {
                var surface1 = compositor.CreateVisualSurface();
                var surfaceBrush1 = compositor.CreateSurfaceBrush();

                surface1.SourceVisual = ElementCompositionPreview.GetElementVisual(view);
                surface1.SourceSize = new Vector2(width, 8);
                surface1.SourceOffset = new Vector2(0, height - (20 - (i * 8)));

                surfaceBrush1.Surface = surface1;
                surfaceBrush1.Stretch = CompositionStretch.None;

                var visual1 = compositor.CreateSpriteVisual();
                visual1.Size = new Vector2(width, 8);
                visual1.Offset = new Vector3(0, 0, 0);
                visual1.Brush = surfaceBrush1; //compositor.CreateColorBrush(Colors.Red); //surfaceBrush;
                visual1.CenterPoint = new Vector3(centerX, centerY, -depth);
                visual1.RotationAxis = new Vector3(1, 0, 0);
                visual1.RotationAngle = ToRadians(rotationFloat - angle * (i + 1));
                visual1.BackfaceVisibility = CompositionBackfaceVisibility.Hidden;

                container.Children.InsertAtBottom(visual1);
            }

            //var visual2 = compositor.CreateSpriteVisual();
            //visual2.Size = new Vector2(width, 20);
            //visual2.Offset = new Vector3(0, 0, 0);
            //visual2.Brush = surfaceBrush2; //compositor.CreateColorBrush(Colors.Green); //surfaceBrush;
            //visual2.CenterPoint = new Vector3(centerX, centerY, -depth);
            //visual2.RotationAxis = new Vector3(1, 0, 0);
            //visual2.RotationAngle = ToRadians(rotationFloat - angle * 2);
            //visual2.Scale = new Vector3(scaleFloat);
            //visual2.BackfaceVisibility = CompositionBackfaceVisibility.Hidden;

            //var visual3 = compositor.CreateSpriteVisual();
            //visual3.Size = new Vector2(width, 20);
            //visual3.Offset = new Vector3(0, 0, 0);
            //visual3.Brush = surfaceBrush3; //compositor.CreateColorBrush(Colors.Blue); //surfaceBrush;
            //visual3.CenterPoint = new Vector3(centerX, centerY, -depth);
            //visual3.RotationAxis = new Vector3(1, 0, 0);
            //visual3.RotationAngle = ToRadians(rotationFloat - angle * 3);
            //visual3.Scale = new Vector3(scaleFloat);
            //visual3.BackfaceVisibility = CompositionBackfaceVisibility.Hidden;

            //container.Children.InsertAtBottom(visual2);
            //container.Children.InsertAtBottom(visual3);

            var cover = compositor.CreateSpriteVisual();
            cover.Size = new Vector2(width, 54);
            cover.Offset = new Vector3(0, 8, 0);
            cover.Brush = compositor.CreateColorBrush(Colors.White);

            container.Children.InsertAtBottom(cover);

            ElementCompositionPreview.SetElementChildVisual(Presenter, container);
            Perspective.Depth = apothem * 20;
        }

        public static float ToRadians(float angleIn10thofaDegree)
        {
            // Angle in 10th of a degree
            return ((angleIn10thofaDegree * MathF.PI) / 180f);
        }

        static float Apothem(float n, float a)
        {
            return a / (2 * MathF.Tan(180 / n * MathF.PI / 180));
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

                if (_message != null)
                {
                    ToggleReaction(sticker.ToReactionType());
                }
                else if (_mode == EmojiDrawerMode.CustomEmojis && sticker.FullType is StickerFullTypeCustomEmoji customEmoji)
                {
                    _clientService.Send(new SetEmojiStatus(new EmojiStatus(customEmoji.CustomEmojiId, 0)));
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
                _clientService.Send(new SetEmojiStatus(new EmojiStatus(customEmoji.CustomEmojiId, item.Duration)));
            }
        }

        private async void ChooseStatus(Sticker sticker)
        {
            var popup = new ChooseStatusDurationPopup();

            var confirm = await popup.ShowQueuedAsync();
            if (confirm == ContentDialogResult.Primary && _mode == EmojiDrawerMode.CustomEmojis && sticker.FullType is StickerFullTypeCustomEmoji customEmoji)
            {
                _clientService.Send(new SetEmojiStatus(new EmojiStatus(customEmoji.CustomEmojiId, popup.Value)));
            }
        }

        private void ToggleReaction(ReactionType reaction)
        {
            if (_story != null)
            {
                StoryToggleReaction(reaction);
            }
            else if (_message != null)
            {
                MessageToggleReaction(reaction);
            }
        }

        private async void StoryToggleReaction(ReactionType reaction)
        {
            if (_story.ChosenReactionType != null && _story.ChosenReactionType.AreTheSame(reaction))
            {
                _story.ClientService.Send(new SetStoryReaction(_story.ChatId, _story.StoryId, null, true));
            }
            else
            {
                await _message.ClientService.SendAsync(new SetStoryReaction(_story.ChatId, _story.StoryId, reaction, true));

                if (_reserved != null && _reserved.IsLoaded)
                {
                    // TODO: UI feedback
                }
            }
        }

        private async void MessageToggleReaction(ReactionType reaction)
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

    }
}
