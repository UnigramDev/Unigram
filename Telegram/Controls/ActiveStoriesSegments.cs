using Microsoft.Graphics.Canvas.Geometry;
using System;
using System.Numerics;
using Telegram.Common;
using Telegram.Controls.Stories;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.ViewModels.Stories;
using Telegram.Views;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
using Point = Windows.Foundation.Point;

namespace Telegram.Controls
{
    public class ActiveStoriesSegments : HyperlinkButton
    {
        private static readonly Color _storyUnreadTopColor = Color.FromArgb(0xFF, 0x34, 0xC7, 0x6F);
        private static readonly Color _storyUnreadBottomColor = Color.FromArgb(0xFF, 0x3D, 0xA1, 0xFD);

        private static readonly Color _storyCloseFriendTopColor = Color.FromArgb(0xFF, 0x78, 0xd5, 0x38);
        private static readonly Color _storyCloseFriendBottomColor = Color.FromArgb(0xFF, 0x2a, 0xb5, 0x6d);

        private static readonly Color _storyDefaultColor = Color.FromArgb(0xFF, 0xD8, 0xD8, 0xE1);

        private bool _hasActiveStories;

        public bool HasActiveStories => _hasActiveStories;

        public ActiveStoriesSegments()
        {
            DefaultStyleKey = typeof(ActiveStoriesSegments);
        }

        public async void Open(INavigationService navigationService, IClientService clientService, Chat chat, int side, Func<ActiveStoriesViewModel, Rect> origin)
        {
            var transform = TransformToVisual(Window.Current.Content);
            var point = transform.TransformPoint(new Point());

            var pointz = new Rect(point.X + 4, point.Y + 4, side - 8, side - 8);

            if (clientService.TryGetActiveStories(chat.Id, out ChatActiveStories cached))
            {
                var unreadCount = cached.CountUnread(out bool closeFriends);
                ShowIndeterminate(side, unreadCount, closeFriends);
            }
            else
            {
                ShowIndeterminate(side, 1, false);
            }

            await clientService.SendAsync(new GetChatActiveStories(chat.Id));

            if (clientService.TryGetActiveStories(chat.Id, out ChatActiveStories chatActiveStories))
            {
                var settings = TLContainer.Current.Resolve<ISettingsService>(clientService.SessionId);
                var aggregator = TLContainer.Current.Resolve<IEventAggregator>(clientService.SessionId);

                var activeStories = new ActiveStoriesViewModel(clientService, settings, aggregator, chatActiveStories);
                await activeStories.Wait;

                var viewModel = new StoryListViewModel(clientService, settings, aggregator, activeStories);
                viewModel.NavigationService = navigationService;

                var window = new StoriesWindow();
                window.Update(viewModel, activeStories, StoryOrigin.ProfilePhoto, pointz, origin);
                _ = window.ShowAsync();
            }

            SetChat(clientService, chat, side);
        }

        public void SetUser(IClientService clientService, User user, int side)
        {
            if (user.Id != clientService.Options.MyId)
            {
                UpdateActiveStories(clientService, user, side);
            }
            else
            {
                UpdateActiveStories(clientService, null, side);
            }
        }

        public void SetChat(IClientService clientService, Chat chat, int side)
        {
            if (chat.Type is ChatTypePrivate privata && privata.UserId != clientService.Options.MyId && clientService.TryGetUser(privata.UserId, out User user))
            {
                UpdateActiveStories(clientService, user, side);
            }
            else
            {
                UpdateActiveStories(clientService, null, side);
            }
        }

        public void UpdateActiveStories(ChatActiveStories activeStories, int side, bool precise)
        {
            if (Content is UIElement element && !_hasActiveStories)
            {
                var visual = ElementCompositionPreview.GetElementVisual(element);
                visual.CenterPoint = new Vector3(side / 2);
                visual.Scale = new Vector3((side - 8f) / side);
            }

            _hasActiveStories = true;

            var unreadCount = activeStories.CountUnread(out bool closeFriends);

            if (precise)
            {
                UpdateSegments(side, closeFriends, activeStories.Stories.Count, unreadCount);
            }
            else if (unreadCount > 0)
            {
                UpdateSegments(side, closeFriends, 1, 1, false);
            }
            else
            {
                UpdateSegments(side, false, 1, 0, false);
            }
        }

        private void UpdateActiveStories(IClientService clientService, User user, int side)
        {
            if (user == null || !user.HasActiveStories)
            {
                if (_hasActiveStories && Content is UIElement element)
                {
                    var visual = ElementCompositionPreview.GetElementVisual(element);
                    visual.Scale = Vector3.One;

                    ElementCompositionPreview.SetElementChildVisual(this, null);
                }

                _hasActiveStories = false;
                IsEnabled = IsClickEnabled;

                return;
            }

            if (Content is UIElement elementa && !_hasActiveStories)
            {
                var visual = ElementCompositionPreview.GetElementVisual(elementa);
                visual.CenterPoint = new Vector3(side / 2);
                visual.Scale = new Vector3((side - 8f) / side);
            }

            _hasActiveStories = true;
            IsEnabled = true;

            if (clientService.TryGetActiveStoriesFromUser(user.Id, out ChatActiveStories activeStories))
            {
                var unreadCount = activeStories.CountUnread(out bool closeFriends);
                UpdateSegments(side, closeFriends, activeStories.Stories.Count, unreadCount);
            }
            else if (user.HasUnreadActiveStories)
            {
                UpdateSegments(side, false, 1, 1);
            }
            else
            {
                UpdateSegments(side, false, 1, 0);
            }
        }

        private void UpdateSegments(int side, bool closeFriends, int total, int unread, bool precise = true)
        {
            var compositor = Window.Current.Compositor;
            var read = total - unread;

            var unreadPath = GetSegments(compositor, side, total, 0, unread);
            var readPath = GetSegments(compositor, side, total, unread, read, precise ? 3 : 4);

            var segments = compositor.CreateShapeVisual();
            segments.Size = new Vector2(side);

            if (unreadPath != null)
            {
                var unreadStroke = compositor.CreateLinearGradientBrush();
                unreadStroke.ColorStops.Add(compositor.CreateColorGradientStop(0, closeFriends ? _storyCloseFriendTopColor : _storyUnreadTopColor));
                unreadStroke.ColorStops.Add(compositor.CreateColorGradientStop(1, closeFriends ? _storyCloseFriendBottomColor : _storyUnreadBottomColor));
                unreadStroke.EndPoint = new Vector2(0, 1);

                var unreadShape = compositor.CreateSpriteShape();
                unreadShape.Geometry = unreadPath;
                unreadShape.StrokeBrush = unreadStroke;
                unreadShape.StrokeThickness = precise ? 2 : 3;
                unreadShape.StrokeStartCap = CompositionStrokeCap.Round;
                unreadShape.StrokeEndCap = CompositionStrokeCap.Round;
                unreadShape.IsStrokeNonScaling = true;

                segments.Shapes.Add(unreadShape);
            }

            if (readPath != null)
            {
                var readStroke = compositor.CreateColorBrush(_storyDefaultColor);

                var readShape = compositor.CreateSpriteShape();
                readShape.Geometry = readPath;
                readShape.StrokeBrush = readStroke;
                readShape.StrokeThickness = precise ? 1 : 3;
                readShape.StrokeStartCap = CompositionStrokeCap.Round;
                readShape.StrokeEndCap = CompositionStrokeCap.Round;
                readShape.IsStrokeNonScaling = true;

                segments.Shapes.Add(readShape);
            }

            ElementCompositionPreview.SetElementChildVisual(this, segments);
        }

        private CompositionGeometry GetSegments(Compositor compositor, float side, float segments, int index, int length, float spacing = 4.0f)
        {
            var center = new Vector2(side * 0.5f);
            var radius = center.X - 1;

            if (length == 0)
            {
                return null;
            }
            else if (segments == 1)
            {
                var ellipse = compositor.CreateEllipseGeometry();
                ellipse.Center = center;
                ellipse.Radius = new Vector2(radius);

                return ellipse;
            }

            CanvasGeometry result;
            using (var builder = new CanvasPathBuilder(null))
            {
                var startAngle = MathFEx.ToRadians(360f / segments);

                //var spacing = 4.0f;
                var angularSpacing = spacing / radius;
                var circleLength = MathF.PI * 2.0f * radius;
                var segmentLength = (circleLength - spacing * segments) / segments;
                var segmentAngle = segmentLength / radius;

                var current = MathFEx.ToRadians(-90) + angularSpacing / 2;
                current -= index * startAngle;

                for (int i = 0; i < length; i++)
                {
                    var x = center.X + (radius * MathF.Cos(current - startAngle));
                    var y = center.Y + (radius * MathF.Sin(current - startAngle));

                    builder.BeginFigure(x, y);
                    builder.AddArc(center, radius, radius, current - startAngle, segmentAngle);
                    builder.EndFigure(CanvasFigureLoop.Open);

                    current -= startAngle;
                }

                result = CanvasGeometry.CreatePath(builder);
            }

            return compositor.CreatePathGeometry(new CompositionPath(result));
        }

        public void ShowIndeterminate(int side, int unreadCount, bool closeFriends)
        {
            var compositor = Window.Current.Compositor;

            var center = new Vector2(side * 0.5f);
            //var clip = compositor.CreateRectangleClip(0, 0, side, side, center, center, center, center);

            var indefiniteReplicatorLayer = compositor.CreateShapeVisual();
            indefiniteReplicatorLayer.Size = new Vector2(side, side);
            //indefiniteReplicatorLayer.RotationAngle = -MathF.PI;
            indefiniteReplicatorLayer.CenterPoint = new Vector3(center, 0);
            //indefiniteReplicatorLayer.Clip = clip;

            var count = 1.0f / 0.0333f;
            var angle = (2.0f * MathF.PI) / count;

            var linear = compositor.CreateLinearEasingFunction();

            var unreadStroke = compositor.CreateLinearGradientBrush();
            unreadStroke.ColorStops.Add(compositor.CreateColorGradientStop(0, unreadCount > 0 ? closeFriends ? _storyCloseFriendTopColor : _storyUnreadTopColor : _storyDefaultColor));
            unreadStroke.ColorStops.Add(compositor.CreateColorGradientStop(1, unreadCount > 0 ? closeFriends ? _storyCloseFriendBottomColor : _storyUnreadBottomColor : _storyDefaultColor));
            unreadStroke.MappingMode = CompositionMappingMode.Absolute;
            unreadStroke.StartPoint = new Vector2(0, 0);
            unreadStroke.EndPoint = new Vector2(0, side);
            unreadStroke.CenterPoint = center;

            for (int i = 0; i < count; i++)
            {
                var indefiniteDash = compositor.CreateEllipseGeometry();
                var indefiniteDashLayer = compositor.CreateSpriteShape();
                indefiniteDashLayer.Geometry = indefiniteDash;
                indefiniteDashLayer.FillBrush = null;
                indefiniteDashLayer.StrokeBrush = unreadStroke;
                indefiniteDashLayer.StrokeThickness = 2.0f;
                indefiniteDashLayer.StrokeStartCap = CompositionStrokeCap.Round;
                indefiniteDashLayer.StrokeEndCap = CompositionStrokeCap.Round;
                //indefiniteDashLayer.RotationAngle = angle * i;
                indefiniteDashLayer.CenterPoint = center;

                indefiniteDash.TrimOffset = 0.0333f * i;
                indefiniteDash.TrimEnd = 0.0333f;
                indefiniteDash.Center = center;
                indefiniteDash.Radius = new Vector2(center.X - 1, center.Y - 1);

                indefiniteReplicatorLayer.Shapes.Add(indefiniteDashLayer);

                var trim = compositor.CreateScalarKeyFrameAnimation();
                trim.InsertKeyFrame(0.00f, 0);
                trim.InsertKeyFrame(0.45f, 0.0333f, linear);
                trim.InsertKeyFrame(0.55f, 0.0333f, linear);
                trim.InsertKeyFrame(1.00f, 0, linear);
                trim.DelayTime = TimeSpan.FromMilliseconds(25 * i);
                trim.Duration = TimeSpan.FromMilliseconds(2500);
                trim.IterationBehavior = AnimationIterationBehavior.Forever;

                indefiniteDash.StartAnimation("TrimStart", trim);
                //indefiniteDash.StartAnimation("TrimOffset", rotation);
            }

            var rotation = compositor.CreateScalarKeyFrameAnimation();
            rotation.InsertKeyFrame(0, -MathF.PI / 2);
            rotation.InsertKeyFrame(1, -MathF.PI / 2 + MathF.PI * 2, linear);
            rotation.Duration = TimeSpan.FromMilliseconds(4000);
            rotation.IterationBehavior = AnimationIterationBehavior.Forever;

            indefiniteReplicatorLayer.StartAnimation("RotationAngle", rotation);

            rotation.InsertKeyFrame(1, MathF.PI / 2, linear);
            rotation.InsertKeyFrame(0, MathF.PI / 2 + MathF.PI * 2);

            unreadStroke.StartAnimation("RotationAngle", rotation);

            ElementCompositionPreview.SetElementChildVisual(this, indefiniteReplicatorLayer);
        }

        #region IsClickEnabled

        public bool IsClickEnabled
        {
            get { return (bool)GetValue(IsClickEnabledProperty); }
            set { SetValue(IsClickEnabledProperty, value); }
        }

        public static readonly DependencyProperty IsClickEnabledProperty =
            DependencyProperty.Register("IsClickEnabled", typeof(bool), typeof(ActiveStoriesSegments), new PropertyMetadata(true));

        #endregion
    }
}
