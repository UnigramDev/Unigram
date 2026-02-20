//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Telegram.Common;
using Telegram.Native;
using Telegram.Streams;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media;

namespace Telegram.Controls
{
    public sealed partial class ProfileGiftsCover : UserControl
    {
        private readonly uint _seed;

        private List<OrbitGenerator.Position> _positions;
        private long _gifts;
        private float _frameWidth;
        private float _frameHeight;

        public ProfileViewModel ViewModel => DataContext as ProfileViewModel;

        public ProfileGiftsCover()
        {
            InitializeComponent();

            _seed = (uint)DateTime.Now.ToTimestamp();
        }

        public UIElement TitleRoot { get; set; }

        private float _transitionFraction;
        public float TransitionFraction
        {
            get => _transitionFraction;
            set => Update(_transitionFraction = value, TitleRoot);
        }

        private void Update(float avatarTransitionFraction, UIElement titleRoot)
        {
            var newSize = new Vector2(ActualSize.X + 36, ActualSize.Y);
            var seed = _seed;

            var gifts = GetPinnedGifts(out long hash);

            var avatarSize = new Vector2(120, 120);
            var centerFrame = new RectangleF((-72 + newSize.X - avatarSize.X) / 2f, (-36 + 204 - avatarSize.Y) / 2f, avatarSize.X, avatarSize.Y);

            if (_gifts != hash || _positions == null || _frameWidth != newSize.X || _frameHeight != newSize.Y)
            {
                GeneratePositions(avatarTransitionFraction, titleRoot);
            }

            var i = 0;

            foreach (var child in RootGrid.Children)
            {
                if (_positions == null || _positions.Count <= i)
                {
                    child.Opacity = 0;
                    continue;
                }

                var iconPosition = _positions[i++];
                var itemDistanceFraction = Math.Max(0.0f, Math.Min(0.5f, (iconPosition.Distance - avatarSize.X / 2.0f) / 144.0f));
                var itemScaleFraction = OrbitGenerator.PatternScaleValueAt(fraction: Math.Min(1.0f, avatarTransitionFraction * 1.33f), t: itemDistanceFraction, reverse: false);

                var toAngle = MathF.PI * 0.18f;
                var centerPosition = new OrbitGenerator.Position(distance: 0.0f, angle: iconPosition.Angle + toAngle, scale: iconPosition.Scale);
                var effectivePosition = OrbitGenerator.InterpolatePosition(from: iconPosition, to: centerPosition, t: itemScaleFraction);
                var effectiveAngle = toAngle * itemScaleFraction;

                var absolutePosition = effectivePosition.GetAbsolutePosition(centerFrame.Center);

                var visual = ElementComposition.GetElementVisual(child);
                visual.Offset = new Vector3(absolutePosition, 0);
                visual.Scale = new Vector3(iconPosition.Scale * (1.0f - itemScaleFraction));
                visual.RotationAngle = effectiveAngle;
            }
        }

        private void GeneratePositions(float avatarTransitionFraction, UIElement titleRoot)
        {
            var newSize = new Vector2(ActualSize.X + 36, ActualSize.Y);
            var seed = _seed;

            var gifts = GetPinnedGifts(out long hash);

            var avatarSize = new Vector2(120, 120);
            var centerFrame = new RectangleF((-72 + newSize.X - avatarSize.X) / 2f, (-36 + 204 - avatarSize.Y) / 2f, avatarSize.X, avatarSize.Y);

            RectangleF[] excludeRects;
            if (titleRoot != null)
            {
                var titleTransform = titleRoot.TransformToVector2(this);

                excludeRects = new RectangleF[]
                {
                    new(titleTransform.X - 4, titleTransform.Y, titleRoot.ActualSize.X + 8, titleRoot.ActualSize.Y),
                };
            }
            else
            {
                excludeRects = Array.Empty<RectangleF>();
            }

            var positionGenerator = new OrbitGenerator(
                containerSize: newSize,
                centerFrame: centerFrame,
                exclusionZones: excludeRects,
                minimumDistance: 42.0f,
                edgePadding: 5.0f,
                seed: seed
            );

            _positions = positionGenerator.GeneratePositions(count: 12, itemSize: new Vector2(28));
            _gifts = hash;
            _frameWidth = newSize.X;
            _frameHeight = newSize.Y;

            RootGrid.Children.Clear();

            var iconPositions = _positions;
            if (iconPositions == null)
            {
                return;
            }

            for (int i = 0; i < Math.Max(iconPositions.Count, gifts.Count); i++)
            {
                if (i >= gifts.Count || i >= iconPositions.Count || gifts[i].Gift is not SentGiftUpgraded upgraded)
                {
                    continue;
                }

                OrbitGenerator.Position iconPosition = iconPositions[i];
                var itemDistanceFraction = Math.Max(0.0f, Math.Min(0.5f, (iconPosition.Distance - avatarSize.X / 2.0f) / 144.0f));
                var itemScaleFraction = OrbitGenerator.PatternScaleValueAt(fraction: Math.Min(1.0f, avatarTransitionFraction * 1.33f), t: itemDistanceFraction, reverse: false);

                var toAngle = MathF.PI * 0.18f;
                var centerPosition = new OrbitGenerator.Position(distance: 0.0f, angle: iconPosition.Angle + toAngle, scale: iconPosition.Scale);
                var effectivePosition = OrbitGenerator.InterpolatePosition(from: iconPosition, to: centerPosition, t: itemScaleFraction);
                var effectiveAngle = toAngle * itemScaleFraction;

                var absolutePosition = effectivePosition.GetAbsolutePosition(centerFrame.Center);

                var centerColor = upgraded.Gift.Backdrop.Colors.CenterColor.ToColor().WithBrightness(0.3f);

                var gradient = new RadialGradientBrush();
                gradient.Center = new Point(0.5, 0.5);
                gradient.GradientStops.Add(new GradientStop { Color = Color.FromArgb(166, centerColor.R, centerColor.G, centerColor.B) });
                gradient.GradientStops.Add(new GradientStop { Color = Color.FromArgb(166, centerColor.R, centerColor.G, centerColor.B), Offset = 0.3 });
                gradient.GradientStops.Add(new GradientStop { Color = Color.FromArgb(0, centerColor.R, centerColor.G, centerColor.B), Offset = 1 });

                var particles = new AnimatedImage
                {
                    Source = new ParticlesImageSource(Colors.White, ParticlesType.Status),
                    IsViewportAware = false,
                    Stretch = Stretch.UniformToFill,
                    DecodeFrameType = Windows.UI.Xaml.Media.Imaging.DecodePixelType.Logical,
                    FrameSize = new Size(36, 36),
                    Width = 36,
                    Height = 36,
                    Margin = new Thickness(-4)
                };

                var icon = new CustomEmojiIcon
                {
                    Source = DelayedFileSource.FromSticker(ViewModel.ClientService, upgraded.Gift.Model.Sticker),
                    Width = 28,
                    Height = 28,
                    FrameSize = new Size(28, 28),
                    IsViewportAware = false
                };

                icon.Ready += OnReady;

                var root = new Grid
                {
                    Opacity = 0,
                    Width = 28,
                    Height = 28,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Top
                };

                root.Children.Add(new Border
                {
                    Background = gradient,
                    Width = 32,
                    Height = 32,
                    Margin = new Thickness(-2)
                });

                root.Children.Add(particles);
                root.Children.Add(icon);

                RootGrid.Children.Add(root);

                var visual = ElementComposition.GetElementVisual(root);
                visual.Offset = new Vector3(absolutePosition, 0);
                visual.Scale = new Vector3(iconPosition.Scale * (1.0f - itemScaleFraction));
                visual.RotationAngle = effectiveAngle;
            }
        }

        private void OnReady(object sender, EventArgs e)
        {
            var icon = sender as CustomEmojiIcon;

            var root = icon.Parent as Grid;
            if (root == null)
            {
                return;
            }

            var visual = ElementComposition.GetElementVisual(root);

            var scale = visual.Compositor.CreateVector3KeyFrameAnimation();
            scale.InsertKeyFrame(0, Vector3.Zero);
            scale.InsertKeyFrame(1, visual.Scale);

            root.Opacity = 1;

            visual.CenterPoint = new Vector3(14);
            visual.StartAnimation("Scale", scale);
        }

        private IList<ReceivedGift> GetPinnedGifts(out long hash)
        {
            hash = 0;

            var itemsView = ViewModel?.GiftsTab?.Items;
            if (itemsView == null)
            {
                return Array.Empty<ReceivedGift>();
            }

            var items = new List<ReceivedGift>();

            foreach (var gift in itemsView)
            {
                if (gift.IsPinned && gift.Gift is SentGiftUpgraded upgraded)
                {
                    items.Add(gift);
                    hash = ((hash * 20261) + 0x80000000L + upgraded.Gift.Id) % 0x80000000L;
                }
            }

            return items;
        }
    }

    public class OrbitGenerator
    {
        public struct Position
        {
            public float Distance { get; }
            public float Angle { get; }
            public float Scale { get; }

            public Position(float distance, float angle, float scale)
            {
                Distance = distance;
                Angle = angle;
                Scale = scale;
            }

            public Vector2 RelativeCartesian
            {
                get
                {
                    return new Vector2(
                        Distance * (float)Math.Cos(Angle),
                        Distance * (float)Math.Sin(Angle)
                    );
                }
            }

            public Vector2 GetAbsolutePosition(Vector2 centerPoint)
            {
                return new Vector2(
                    centerPoint.X + Distance * (float)Math.Cos(Angle),
                    centerPoint.Y + Distance * (float)Math.Sin(Angle)
                );
            }
        }

        private readonly Vector2 containerSize;
        private readonly RectangleF centerFrame;
        private readonly RectangleF[] exclusionZones;
        private readonly float minimumDistance;
        private readonly float edgePadding;
        private readonly (float min, float max) scaleRange;

        private readonly (float min, float max) innerOrbitRange;
        private readonly (float min, float max) outerOrbitRange;
        private readonly int innerOrbitCount;

        private readonly LokiRng lokiRng;

        public OrbitGenerator(
            Vector2 containerSize,
            RectangleF centerFrame,
            RectangleF[] exclusionZones,
            float minimumDistance,
            float edgePadding,
            uint seed,
            (float min, float max) scaleRange = default,
            (float min, float max) innerOrbitRange = default,
            (float min, float max) outerOrbitRange = default,
            int innerOrbitCount = 4)
        {
            this.containerSize = containerSize;
            this.centerFrame = centerFrame;
            this.exclusionZones = exclusionZones;
            this.minimumDistance = minimumDistance;
            this.edgePadding = edgePadding;
            this.scaleRange = scaleRange == default ? (0.7f, 1.15f) : scaleRange;
            this.innerOrbitRange = innerOrbitRange == default ? (1.4f, 2.2f) : innerOrbitRange;
            this.outerOrbitRange = outerOrbitRange == default ? (2.5f, 3.6f) : outerOrbitRange;
            this.innerOrbitCount = innerOrbitCount;
            this.lokiRng = new LokiRng(seed, 0, 0);
        }

        public List<Position> GeneratePositions(int count, Vector2 itemSize)
        {
            var positions = new List<Position>();

            var centerPoint = new Vector2(
                centerFrame.X + centerFrame.Width / 2f,
                centerFrame.Y + centerFrame.Height / 2f
            );
            var centerRadius = Math.Min(centerFrame.Width, centerFrame.Height) / 2f;

            int maxAttempts = count * 200;
            int attempts = 0;

            int leftPositions = 0;
            int rightPositions = 0;

            int innerCount = Math.Min(innerOrbitCount, count);

            // Generate inner orbit positions
            while (positions.Count < innerCount && attempts < maxAttempts)
            {
                attempts++;

                bool placeOnLeftSide = rightPositions > leftPositions;

                float orbitRangeSize = innerOrbitRange.max - innerOrbitRange.min;
                float orbitDistanceFactor = innerOrbitRange.min + orbitRangeSize * lokiRng.Next();
                float distance = orbitDistanceFactor * centerRadius;

                float angleRange = (float)Math.PI;
                float angleOffset = placeOnLeftSide ? (float)Math.PI / 2 : -(float)Math.PI / 2;
                float angle = angleOffset + angleRange * lokiRng.Next();

                var absolutePosition = GetAbsolutePosition(distance, angle, centerPoint);

                if (absolutePosition.X - itemSize.X / 2 < edgePadding ||
                    absolutePosition.X + itemSize.X / 2 > containerSize.X - edgePadding ||
                    absolutePosition.Y - itemSize.Y / 2 < edgePadding ||
                    absolutePosition.Y + itemSize.Y / 2 > containerSize.Y - edgePadding)
                {
                    continue;
                }

                var itemRect = new RectangleF(
                    absolutePosition.X - itemSize.X / 2,
                    absolutePosition.Y - itemSize.Y / 2,
                    itemSize.X,
                    itemSize.Y
                );

                if (IsValidPosition(itemRect, positions.Select(p =>
                    GetAbsolutePosition(p.Distance, p.Angle, centerPoint)).ToList(), itemSize))
                {
                    float scaleRangeSize = Math.Max(scaleRange.min + 0.1f, 0.75f) - scaleRange.max;
                    float scale = scaleRange.max + scaleRangeSize * lokiRng.Next();
                    positions.Add(new Position(distance, angle, scale));

                    if (absolutePosition.X < centerPoint.X)
                        leftPositions++;
                    else
                        rightPositions++;
                }
            }

            float maxPossibleDistance = (float)Math.Sqrt(containerSize.X * containerSize.X +
                                                        containerSize.Y * containerSize.Y) / 2;

            // Generate outer orbit positions
            while (positions.Count < count && attempts < maxAttempts)
            {
                attempts++;

                bool placeOnLeftSide = rightPositions >= leftPositions;

                float orbitRangeSize = outerOrbitRange.max - outerOrbitRange.min;
                float orbitDistanceFactor = outerOrbitRange.min + orbitRangeSize * lokiRng.Next();
                float distance = orbitDistanceFactor * centerRadius;

                float angleRange = (float)Math.PI;
                float angleOffset = placeOnLeftSide ? (float)Math.PI / 2 : -(float)Math.PI / 2;
                float angle = angleOffset + angleRange * lokiRng.Next();

                var absolutePosition = GetAbsolutePosition(distance, angle, centerPoint);

                if (absolutePosition.X - itemSize.X / 2 < edgePadding ||
                    absolutePosition.X + itemSize.X / 2 > containerSize.X - edgePadding ||
                    absolutePosition.Y - itemSize.Y / 2 < edgePadding ||
                    absolutePosition.Y + itemSize.Y / 2 > containerSize.Y - edgePadding)
                {
                    continue;
                }

                var itemRect = new RectangleF(
                    absolutePosition.X - itemSize.X / 2,
                    absolutePosition.Y - itemSize.Y / 2,
                    itemSize.X,
                    itemSize.Y
                );

                if (IsValidPosition(itemRect, positions.Select(p =>
                    GetAbsolutePosition(p.Distance, p.Angle, centerPoint)).ToList(), itemSize))
                {
                    float normalizedDistance = Math.Min(distance / maxPossibleDistance, 1.0f);
                    float scale = scaleRange.max - normalizedDistance * (scaleRange.max - scaleRange.min);
                    positions.Add(new Position(distance, angle, scale));

                    if (absolutePosition.X < centerPoint.X)
                        leftPositions++;
                    else
                        rightPositions++;
                }
            }

            return positions;
        }

        public static Vector2 GetAbsolutePosition(float distance, float angle, Vector2 centerPoint)
        {
            return new Vector2(
                centerPoint.X + distance * (float)Math.Cos(angle),
                centerPoint.Y + distance * (float)Math.Sin(angle)
            );
        }

        private bool IsValidPosition(RectangleF rect, List<Vector2> existingPositions, Vector2 itemSize)
        {
            if (rect.Left < edgePadding || rect.Right > containerSize.X - edgePadding ||
                rect.Top < edgePadding || rect.Bottom > containerSize.Y - edgePadding)
            {
                return false;
            }

            foreach (var zone in exclusionZones)
            {
                if (rect.IntersectsWith(zone))
                {
                    return false;
                }
            }

            float effectiveMinDistance = existingPositions.Count > 5 ?
                Math.Max(minimumDistance * 0.7f, 10.0f) : minimumDistance;

            foreach (var existingPosition in existingPositions)
            {
                float distance = (float)Math.Sqrt(
                    Math.Pow(existingPosition.X - (rect.X + rect.Width / 2), 2) +
                    Math.Pow(existingPosition.Y - (rect.Y + rect.Height / 2), 2)
                );
                if (distance < effectiveMinDistance)
                {
                    return false;
                }
            }

            return true;
        }

        public static Position InterpolatePosition(Position from, Position to, float t)
        {
            var clampedT = Math.Max(0, Math.Min(1, t));

            var interpolatedDistance = from.Distance + (to.Distance - from.Distance) * clampedT;
            var interpolatedAngle = from.Angle + (to.Angle - from.Angle) * clampedT;

            return new OrbitGenerator.Position(distance: interpolatedDistance, angle: interpolatedAngle, scale: from.Scale);
        }

        public static float WindowFunction(float t)
        {
            return BezierPoint.Calculate(0.6f, 0.0f, 0.4f, 1.0f, t);
        }

        public static float PatternScaleValueAt(float fraction, float t, bool reverse)
        {
            float windowSize = 0.8f;

            float effectiveT;
            float windowStartOffset;
            float windowEndOffset;

            if (reverse)
            {
                effectiveT = 1.0f - t;
                windowStartOffset = 1.0f;
                windowEndOffset = -windowSize;
            }
            else
            {
                effectiveT = t;
                windowStartOffset = -windowSize;
                windowEndOffset = 1.0f;
            }

            float windowPosition = (1.0f - fraction) * windowStartOffset + fraction * windowEndOffset;
            float windowT = Math.Max(0.0f, Math.Min(windowSize, effectiveT - windowPosition)) / windowSize;
            float localT = 1.0f - WindowFunction(windowT);

            return localT;
        }
    }
}
