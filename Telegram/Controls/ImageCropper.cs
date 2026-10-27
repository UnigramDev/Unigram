//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Entities;
using Telegram.Navigation;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;

namespace Telegram.Controls
{
    public enum BitmapProportions
    {
        Custom,
        Original,
        Square,
        TwoOverThree,
        ThreeOverFive,
        ThreeOverFour,
        FourOverFive,
        FiveOverSeven,
        NineOverSixteen,
        ThreeOverTwo,
        FiveOverThree,
        FourOverThree,
        FiveOverFour,
        SevenOverFive,
        SixteenOverNine,
    }

    public enum ImageCropperMask
    {
        Rectangle,
        Ellipse
    }

    public sealed partial class ImageCropperThumb : Control
    {
        #region constructors
        public ImageCropperThumb()
        {
            DefaultStyleKey = typeof(ImageCropperThumb);
        }
        #endregion
    }

    /// <summary>
    /// The crop area stays put in the view and the image moves under it.
    ///
    /// The crop is held in oriented pixels — the source mirrored, then turned by whole quarters —
    /// as a centre and a size, plus a free angle the image turns by about that centre. That is
    /// <see cref="ImageGeneration"/>'s shape, so nothing is converted on the way out.
    /// </summary>
    /// <summary>
    /// A crop, its turns and its mirror, as <see cref="ImageCropper.GetState"/> took them.
    /// </summary>
    public sealed class ImageCropperState
    {
        internal ImageCropperState(int quarterTurns, bool mirrored, double angle, Point center, Size cropSize, BitmapProportions proportions)
        {
            QuarterTurns = quarterTurns;
            Mirrored = mirrored;
            Angle = angle;
            Center = center;
            CropSize = cropSize;
            Proportions = proportions;
        }

        internal int QuarterTurns { get; }

        internal bool Mirrored { get; }

        internal double Angle { get; }

        internal Point Center { get; }

        internal Size CropSize { get; }

        internal BitmapProportions Proportions { get; }

        public bool Matches(ImageCropperState other)
        {
            return other != null
                && QuarterTurns == other.QuarterTurns
                && Mirrored == other.Mirrored
                && Angle == other.Angle
                && Center == other.Center
                && CropSize == other.CropSize
                && Proportions == other.Proportions;
        }
    }

    public partial class ImageCropper : ContentControl
    {
        private const double MaximumAngle = 45;
        private const double MaximumZoom = 30;

        [Flags]
        private enum Edges
        {
            None = 0,
            Left = 1,
            Top = 2,
            Right = 4,
            Bottom = 8
        }

        private readonly Dictionary<uint, Point> m_pointers = new();

        private Grid LayoutRoot;
        private FrameworkElement ImageThumb;
        private Image ImageViewer;
        private FrameworkElement PanSurface;
        private XamlPath Clip;
        private Grid ThumbsContainer;
        private UIElement MajorGrid;
        private UIElement MinorGrid;

        private MatrixTransform m_imageTransform;
        private RectangleGeometry m_layoutClip;
        private RectangleGeometry m_outerClip;
        private Geometry m_innerClip;
        private MatrixTransform m_innerTransform;

        // The decoded preview, upright, before any edit. Every coordinate below is in its pixels.
        private Size m_sourceSize;
        private bool m_hasSource;

        private int m_quarterTurns;
        private bool m_mirrored;
        private double m_angle;
        private Point m_center;
        private Size m_cropSize;

        private BitmapProportions m_proportions = BitmapProportions.Custom;
        private ImageCropperMask m_mask = ImageCropperMask.Rectangle;
        private bool m_frame;

        // Where the crop sits in LayoutRoot. Fitted to the viewport, except while an edge is dragged.
        private Rect m_area;
        private Size m_viewport;

        private Rect m_dragArea;
        private Point m_dragStart;
        private Point m_dragCenter;
        private Size m_dragCropSize;
        private double m_dragScale;
        private uint? m_dragPointer;

        // While an edge is held past the viewport, the crop keeps growing and the view zooming out.
        private Edges m_dragEdges;
        private Point m_dragPosition;
        private TimeSpan? m_dragTime;
        private bool m_dragZooming;

        private Size m_rotationCropSize;
        private bool m_rotating;

        // Draw mode's magnification, applied over the crop's own view: it changes what is seen,
        // never the crop. The offset is in LayoutRoot pixels, after the zoom.
        private double m_viewZoom = 1;
        private Point m_viewOffset;

        private Matrix3x2 m_contentTransform = Matrix3x2.Identity;
        private Rect m_viewArea;

        private bool m_animating;
        private TimeSpan? m_animationStart;
        private double m_animationDuration;
        private double m_animationProgress;
        private Point m_fromPosition;
        private double m_fromScale;
        private double m_fromAngle;
        private Rect m_fromArea;
        private double m_fromMirror = 1;

        public ImageCropper()
        {
            DefaultStyleKey = typeof(ImageCropper);

            Unloaded += OnUnloaded;
        }

        public event EventHandler Changed;

        /// <summary>
        /// Raised once the source is shown, with the image as decoded for it: upright and at most
        /// 1280 pixels on its longer side.
        /// </summary>
        public event EventHandler<SoftwareBitmap> SourceDecoded;

        /// <summary>
        /// Raised whenever the content moves on screen, every frame of an animation included.
        /// </summary>
        public event EventHandler ViewChanged;

        /// <summary>
        /// The smallest the crop area may be dragged to, in view pixels.
        /// </summary>
        public double MinimumAreaSize { get; set; } = 32;

        protected override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            LayoutRoot = GetTemplateChild(nameof(LayoutRoot)) as Grid;
            ImageThumb = GetTemplateChild(nameof(ImageThumb)) as FrameworkElement;
            ImageViewer = GetTemplateChild(nameof(ImageViewer)) as Image;
            PanSurface = GetTemplateChild(nameof(PanSurface)) as FrameworkElement;
            ThumbsContainer = GetTemplateChild(nameof(ThumbsContainer)) as Grid;
            MajorGrid = GetTemplateChild(nameof(MajorGrid)) as UIElement;
            MinorGrid = GetTemplateChild(nameof(MinorGrid)) as UIElement;

            Clip = GetTemplateChild(nameof(Clip)) as XamlPath;

            ImageThumb.RenderTransform = m_imageTransform = new MatrixTransform();
            LayoutRoot.Clip = m_layoutClip = new RectangleGeometry();
            LayoutRoot.SizeChanged += LayoutRoot_SizeChanged;

            PanSurface.PointerPressed += PanSurface_PointerPressed;
            PanSurface.PointerMoved += PanSurface_PointerMoved;
            PanSurface.PointerReleased += PanSurface_PointerReleased;
            PanSurface.PointerCanceled += PanSurface_PointerReleased;
            PanSurface.PointerCaptureLost += PanSurface_PointerReleased;
            PanSurface.PointerWheelChanged += PanSurface_PointerWheelChanged;

            AttachThumb("LeftThumb", Edges.Left, WEThumb_PointerEntered);
            AttachThumb("TopThumb", Edges.Top, NSThumb_PointerEntered);
            AttachThumb("RightThumb", Edges.Right, WEThumb_PointerEntered);
            AttachThumb("BottomThumb", Edges.Bottom, NSThumb_PointerEntered);
            AttachThumb("TopLeftThumb", Edges.Top | Edges.Left, NWSEThumb_PointerEntered);
            AttachThumb("TopRightThumb", Edges.Top | Edges.Right, NESWThumb_PointerEntered);
            AttachThumb("BottomLeftThumb", Edges.Bottom | Edges.Left, NESWThumb_PointerEntered);
            AttachThumb("BottomRightThumb", Edges.Bottom | Edges.Right, NWSEThumb_PointerEntered);

            if (Clip != null)
            {
                SetMask(m_mask);
            }
        }

        private void AttachThumb(string name, Edges edges, PointerEventHandler entered)
        {
            if (GetTemplateChild(name) is ImageCropperThumb thumb)
            {
                thumb.Tag = edges;
                thumb.PointerEntered += entered;
                thumb.PointerExited += Thumb_PointerExited;
                thumb.PointerPressed += Thumb_PointerPressed;
                thumb.PointerMoved += Thumb_PointerMoved;
                thumb.PointerReleased += Thumb_PointerReleased;
                thumb.PointerCanceled += Thumb_PointerReleased;
                thumb.PointerCaptureLost += Thumb_PointerReleased;
            }
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            StopAnimation();
            StopDragZoom();
        }

        private void LayoutRoot_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            m_viewport = e.NewSize;

            UpdateLayoutClip();
            Refit();
        }

        // Cropping shows the whole image, the clip reaching into the padding so that a zoomed
        // image fills the control. Drawing shows only what the crop keeps, so that it is plain
        // what a stroke lands inside.
        private void UpdateLayoutClip()
        {
            if (m_layoutClip == null)
            {
                return;
            }

            if (_isCropEnabled)
            {
                var padding = Padding;
                m_layoutClip.Rect = new Rect(-padding.Left, -padding.Top, m_viewport.Width + padding.Left + padding.Right, m_viewport.Height + padding.Top + padding.Bottom);
            }
            else
            {
                m_layoutClip.Rect = m_viewArea;
            }
        }

        #region Geometry

        private Size OrientedSize => m_quarterTurns % 2 == 0
            ? m_sourceSize
            : new Size(m_sourceSize.Height, m_sourceSize.Width);

        private double ViewScale => m_area.Width / m_cropSize.Width;

        private Point AreaCenter => new(m_area.X + m_area.Width / 2, m_area.Y + m_area.Height / 2);

        // Shared with the export, so what is previewed is what is sent.
        private Matrix3x2 GetOrientation()
        {
            return ImageHelper.GetOrientationMatrix(Orientation, Flip, (float)m_sourceSize.Width, (float)m_sourceSize.Height);
        }

        private static Point Rotate(double x, double y, double degrees)
        {
            var radians = degrees * Math.PI / 180;
            var sin = Math.Sin(radians);
            var cos = Math.Cos(radians);

            return new Point(x * cos - y * sin, x * sin + y * cos);
        }

        private Rect FitArea(Size crop)
        {
            var ratio = crop.Width / crop.Height;

            double width;
            double height;

            if (m_viewport.Width / m_viewport.Height > ratio)
            {
                height = m_viewport.Height;
                width = height * ratio;
            }
            else
            {
                width = m_viewport.Width;
                height = width / ratio;
            }

            return new Rect((m_viewport.Width - width) / 2, (m_viewport.Height - height) / 2, width, height);
        }

        /// <summary>
        /// Keeps the image covering the crop: zooms in when the turned crop no longer fits inside
        /// the image, then slides the centre back inside. A turned rectangle fits inside an upright
        /// one exactly when its bounding box does.
        ///
        /// A frame is measured along its own axes instead, against the image's bounds there: it may
        /// be larger than them along either axis, up to holding all of them, and along such an axis
        /// the bounds stay inside the frame rather than covering it.
        /// </summary>
        private void Constrain()
        {
            var oriented = OrientedSize;

            var radians = m_angle * Math.PI / 180;
            var sin = Math.Abs(Math.Sin(radians));
            var cos = Math.Abs(Math.Cos(radians));

            if (m_frame)
            {
                var imageWidth = oriented.Width * cos + oriented.Height * sin;
                var imageHeight = oriented.Width * sin + oriented.Height * cos;

                var fit = Math.Max(imageWidth / m_cropSize.Width, imageHeight / m_cropSize.Height);
                if (fit < 1)
                {
                    m_cropSize = new Size(m_cropSize.Width * fit, m_cropSize.Height * fit);
                }

                // The image's centre from the frame's, turned into the frame's axes. Clamped in the
                // image's axes instead, a turned frame would stand in for its own bounding box, much
                // larger at 45 degrees: the image could leave the frame, and stop short of its edges.
                var offset = Rotate(oriented.Width / 2 - m_center.X, oriented.Height / 2 - m_center.Y, m_angle);
                var limitX = Math.Abs(imageWidth - m_cropSize.Width) / 2;
                var limitY = Math.Abs(imageHeight - m_cropSize.Height) / 2;

                var clamped = Rotate(Math.Clamp(offset.X, -limitX, limitX), Math.Clamp(offset.Y, -limitY, limitY), -m_angle);

                m_center = new Point(oriented.Width / 2 - clamped.X, oriented.Height / 2 - clamped.Y);
                return;
            }

            var boundsWidth = m_cropSize.Width * cos + m_cropSize.Height * sin;
            var boundsHeight = m_cropSize.Width * sin + m_cropSize.Height * cos;

            var shrink = Math.Min(oriented.Width / boundsWidth, oriented.Height / boundsHeight);
            if (shrink < 1)
            {
                m_cropSize = new Size(m_cropSize.Width * shrink, m_cropSize.Height * shrink);
                boundsWidth *= shrink;
                boundsHeight *= shrink;
            }

            var halfWidth = boundsWidth / 2;
            var halfHeight = boundsHeight / 2;

            // Past the image's edges the two bounds swap, and the clamp keeps the image inside.
            m_center.X = Math.Clamp(m_center.X, Math.Min(halfWidth, oriented.Width - halfWidth), Math.Max(halfWidth, oriented.Width - halfWidth));
            m_center.Y = Math.Clamp(m_center.Y, Math.Min(halfHeight, oriented.Height - halfHeight), Math.Max(halfHeight, oriented.Height - halfHeight));
        }

        private double GetLockedRatio()
        {
            if (m_mask == ImageCropperMask.Ellipse)
            {
                return 1;
            }

            return m_proportions == BitmapProportions.Custom
                ? 0
                : GetProportionsFactor(m_proportions, 0);
        }

        private Size GetDefaultCropSize()
        {
            var oriented = OrientedSize;
            var ratio = GetLockedRatio();

            if (ratio == 0)
            {
                return oriented;
            }

            // A frame fills with an image only once it is clearly portrait, 1.29 times taller than
            // wide; anything wider is shown whole across the frame's width.
            if (m_frame && oriented.Height / oriented.Width <= 1.29)
            {
                return new Size(oriented.Width, oriented.Width / ratio);
            }

            return oriented.Width / oriented.Height > ratio
                ? new Size(oriented.Height * ratio, oriented.Height)
                : new Size(oriented.Width, oriented.Width / ratio);
        }

        private void ResetCrop()
        {
            var oriented = OrientedSize;

            m_cropSize = GetDefaultCropSize();
            m_center = new Point(oriented.Width / 2, oriented.Height / 2);
        }

        private bool CanLayout => m_hasSource && m_viewport.Width > 0 && m_viewport.Height > 0;

        private void Refit()
        {
            if (CanLayout)
            {
                StopAnimation();

                m_area = FitArea(m_cropSize);
                m_viewOffset = ClampView(m_viewZoom, m_viewOffset);
                UpdateView();
            }
        }

        private void UpdateView()
        {
            if (!CanLayout || m_imageTransform == null)
            {
                return;
            }

            var position = AreaCenter;
            var scale = ViewScale;
            var angle = m_angle;
            var area = m_area;
            var mirror = 1d;

            if (m_animating)
            {
                var t = m_animationProgress;

                position = new Point(Lerp(m_fromPosition.X, position.X, t), Lerp(m_fromPosition.Y, position.Y, t));
                scale = Lerp(m_fromScale, scale, t);
                angle = Lerp(m_fromAngle, angle, t);
                mirror = Lerp(m_fromMirror, 1, t);
                area = new Rect(
                    Lerp(m_fromArea.X, area.X, t),
                    Lerp(m_fromArea.Y, area.Y, t),
                    Lerp(m_fromArea.Width, area.Width, t),
                    Lerp(m_fromArea.Height, area.Height, t));
            }

            var matrix = GetOrientation()
                * Matrix3x2.CreateTranslation(-(float)m_center.X, -(float)m_center.Y)
                * Matrix3x2.CreateScale((float)mirror, 1)
                * Matrix3x2.CreateRotation((float)(angle * Math.PI / 180))
                * Matrix3x2.CreateScale((float)scale)
                * Matrix3x2.CreateTranslation((float)position.X, (float)position.Y);

            if (m_viewZoom != 1 || m_viewOffset != default)
            {
                matrix *= Matrix3x2.CreateScale((float)m_viewZoom) * Matrix3x2.CreateTranslation((float)m_viewOffset.X, (float)m_viewOffset.Y);
                area = new Rect(area.X * m_viewZoom + m_viewOffset.X, area.Y * m_viewZoom + m_viewOffset.Y, area.Width * m_viewZoom, area.Height * m_viewZoom);
            }

            m_imageTransform.Matrix = new Matrix(matrix.M11, matrix.M12, matrix.M21, matrix.M22, matrix.M31, matrix.M32);

            m_contentTransform = matrix;
            m_viewArea = area;

            if (!_isCropEnabled)
            {
                UpdateLayoutClip();
            }

            ViewChanged?.Invoke(this, EventArgs.Empty);

            ThumbsContainer.Margin = new Thickness(area.X, area.Y, m_viewport.Width - area.Right, m_viewport.Height - area.Bottom);

            if (m_outerClip == null)
            {
                return;
            }

            m_outerClip.Rect = new Rect(0, 0, m_sourceSize.Width, m_sourceSize.Height);

            // Singular for the one frame a mirror animation passes through zero width.
            if (Matrix3x2.Invert(matrix, out Matrix3x2 inverse))
            {
                m_innerTransform.Matrix = new Matrix(inverse.M11, inverse.M12, inverse.M21, inverse.M22, inverse.M31, inverse.M32);
            }

            switch (m_innerClip)
            {
                case RectangleGeometry rectangle:
                    rectangle.Rect = area;
                    break;
                case EllipseGeometry ellipse:
                    ellipse.Center = new Point(area.X + area.Width / 2, area.Y + area.Height / 2);
                    ellipse.RadiusX = area.Width / 2;
                    ellipse.RadiusY = area.Height / 2;
                    break;
            }
        }

        private static double Lerp(double from, double to, double t)
        {
            return from + (to - from) * t;
        }

        #endregion

        #region Animation

        /// <summary>
        /// Animates from a view described in the current frame to the current state. The start
        /// position is where the current crop centre was drawn, so callers that move the centre
        /// pass the old one through <see cref="GetStartPosition"/>. A <paramref name="mirror"/> of
        /// -1 starts the image flipped about the crop's vertical axis, after the turn: a mirror
        /// is not a rotation, so it animates as a squeeze through zero instead.
        /// </summary>
        private void AnimateFrom(Point position, double scale, double angle, Rect area, double duration, double mirror = 1)
        {
            m_fromPosition = position;
            m_fromScale = scale;
            m_fromAngle = angle;
            m_fromArea = area;
            m_fromMirror = mirror;

            m_animationDuration = duration;
            m_animationStart = null;
            m_animationProgress = 0;

            if (!m_animating)
            {
                m_animating = true;
                CompositionTarget.Rendering += OnRendering;
            }

            UpdateView();
        }

        private Point GetStartPosition(Point position, double scale, double angle, Point previousCenter, double mirror = 1)
        {
            var offset = Rotate((m_center.X - previousCenter.X) * mirror, m_center.Y - previousCenter.Y, angle);
            return new Point(position.X + offset.X * scale, position.Y + offset.Y * scale);
        }

        private void OnRendering(object sender, object e)
        {
            if (e is not RenderingEventArgs args)
            {
                return;
            }

            m_animationStart ??= args.RenderingTime;

            var t = (args.RenderingTime - m_animationStart.Value).TotalMilliseconds / m_animationDuration;
            if (t >= 1)
            {
                StopAnimation();
                return;
            }

            m_animationProgress = Math.Cos((t + 1) * Math.PI) / 2 + 0.5;
            UpdateView();
        }

        private void StopAnimation()
        {
            if (m_animating)
            {
                m_animating = false;
                CompositionTarget.Rendering -= OnRendering;

                UpdateView();
            }
        }

        #endregion

        #region Grid

        private void ShowGrid(bool minor)
        {
            if (MajorGrid != null)
            {
                MajorGrid.Opacity = minor ? 0 : 1;
                MinorGrid.Opacity = minor ? 1 : 0;
            }
        }

        private void HideGrid()
        {
            if (MajorGrid != null)
            {
                MajorGrid.Opacity = 0;
                MinorGrid.Opacity = 0;
            }
        }

        #endregion

        #region Pan and zoom

        private void PanSurface_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (!CanLayout || (m_pointers.Count >= 2 && !m_pointers.ContainsKey(e.Pointer.PointerId)))
            {
                return;
            }

            if (PanSurface.CapturePointer(e.Pointer))
            {
                StopAnimation();

                m_pointers[e.Pointer.PointerId] = e.GetCurrentPoint(LayoutRoot).Position;
                ShowGrid(false);
            }

            e.Handled = true;
        }

        private void PanSurface_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (!m_pointers.TryGetValue(e.Pointer.PointerId, out Point previous))
            {
                return;
            }

            var position = e.GetCurrentPoint(LayoutRoot).Position;

            if (m_pointers.Count == 1)
            {
                Pan(position.X - previous.X, position.Y - previous.Y);
            }
            else
            {
                var other = previous;

                foreach (var pointer in m_pointers)
                {
                    if (pointer.Key != e.Pointer.PointerId)
                    {
                        other = pointer.Value;
                    }
                }

                var before = Distance(previous, other);
                var after = Distance(position, other);

                var previousMiddle = new Point((previous.X + other.X) / 2, (previous.Y + other.Y) / 2);
                var middle = new Point((position.X + other.X) / 2, (position.Y + other.Y) / 2);

                if (before > 0)
                {
                    ZoomAt(middle, after / before);
                }

                Pan(middle.X - previousMiddle.X, middle.Y - previousMiddle.Y);
            }

            m_pointers[e.Pointer.PointerId] = position;
            UpdateView();
        }

        private void PanSurface_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            if (m_pointers.Remove(e.Pointer.PointerId))
            {
                PanSurface.ReleasePointerCapture(e.Pointer);

                if (m_pointers.Count == 0)
                {
                    HideGrid();
                    Changed?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        private void PanSurface_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
        {
            var point = e.GetCurrentPoint(LayoutRoot);
            if (!CanLayout || point.Properties.IsHorizontalMouseWheel)
            {
                return;
            }

            StopAnimation();

            // 120 a notch, so a notch is about a fifth.
            ZoomAt(point.Position, Math.Pow(1.0015, point.Properties.MouseWheelDelta));
            UpdateView();

            Changed?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
        }

        private static double Distance(Point a, Point b)
        {
            var x = a.X - b.X;
            var y = a.Y - b.Y;

            return Math.Sqrt(x * x + y * y);
        }

        private void Pan(double x, double y)
        {
            var scale = ViewScale;
            var delta = Rotate(x, y, -m_angle);

            m_center.X -= delta.X / scale;
            m_center.Y -= delta.Y / scale;

            Constrain();
        }

        private void ZoomAt(Point point, double factor)
        {
            var oriented = OrientedSize;

            var scale = ViewScale;
            var maximum = MaximumZoom * Math.Min(m_viewport.Width / oriented.Width, m_viewport.Height / oriented.Height);
            var target = Math.Min(scale * factor, Math.Max(scale, maximum));

            // The image point under the pointer stays under it.
            var center = AreaCenter;
            var offset = Rotate(point.X - center.X, point.Y - center.Y, -m_angle);

            m_center.X += offset.X * (1 / scale - 1 / target);
            m_center.Y += offset.Y * (1 / scale - 1 / target);

            var ratio = scale / target;
            m_cropSize = new Size(m_cropSize.Width * ratio, m_cropSize.Height * ratio);

            Constrain();
        }

        #endregion

        #region View zoom

        /// <summary>
        /// Magnifies the view by <paramref name="factor"/> about a point in window coordinates,
        /// between the fitted crop and eight times it.
        /// </summary>
        public void ZoomView(Point windowPoint, double factor)
        {
            if (!CanLayout)
            {
                return;
            }

            var point = LayoutRoot.TransformToVisual(null).Inverse.TransformPoint(windowPoint);
            ZoomViewAt(point, factor);
        }

        public void PanView(double x, double y)
        {
            if (!CanLayout)
            {
                return;
            }

            m_viewOffset = ClampView(m_viewZoom, new Point(m_viewOffset.X + x, m_viewOffset.Y + y));
            UpdateView();
        }

        public void ResetView()
        {
            if (m_viewZoom == 1 && m_viewOffset == default)
            {
                return;
            }

            m_viewZoom = 1;
            m_viewOffset = default;

            UpdateView();
        }

        private void ZoomViewAt(Point point, double factor)
        {
            var zoom = Math.Clamp(m_viewZoom * factor, 1, 8);
            var ratio = zoom / m_viewZoom;

            // The point under the pointer stays under it.
            var offset = new Point(point.X - (point.X - m_viewOffset.X) * ratio, point.Y - (point.Y - m_viewOffset.Y) * ratio);

            m_viewZoom = zoom;
            m_viewOffset = ClampView(zoom, offset);

            UpdateView();
        }

        // The crop stays covering the view along an axis it overflows, and centred along one it fits.
        private Point ClampView(double zoom, Point offset)
        {
            var area = m_area;

            return new Point(
                Axis(area.X, area.Width, m_viewport.Width, offset.X),
                Axis(area.Y, area.Height, m_viewport.Height, offset.Y));

            double Axis(double start, double length, double viewport, double value)
            {
                var size = length * zoom;
                if (size <= viewport)
                {
                    return (viewport - size) / 2 - start * zoom;
                }

                return Math.Clamp(value, viewport - (start + length) * zoom, -start * zoom);
            }
        }

        // In draw mode the wheel moves the view: Ctrl, or a touchpad pinch, zooms; otherwise it pans.
        protected override void OnPointerWheelChanged(PointerRoutedEventArgs e)
        {
            base.OnPointerWheelChanged(e);

            if (_isCropEnabled || e.Handled || !CanLayout)
            {
                return;
            }

            var point = e.GetCurrentPoint(LayoutRoot);
            var delta = point.Properties.MouseWheelDelta;

            if ((e.KeyModifiers & VirtualKeyModifiers.Control) != 0)
            {
                ZoomViewAt(point.Position, Math.Pow(1.0015, delta));
            }
            else if (point.Properties.IsHorizontalMouseWheel || (e.KeyModifiers & VirtualKeyModifiers.Shift) != 0)
            {
                PanView(point.Properties.IsHorizontalMouseWheel ? -delta : delta, 0);
            }
            else
            {
                PanView(0, delta);
            }

            e.Handled = true;
        }

        #endregion

        #region Thumbs

        private void Thumb_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            this.SetPointerCursor(PointerCursorType.Arrow);
        }

        private void Thumb_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            // The same pointer pressing again means its release never arrived: let it start over.
            if (!CanLayout || (m_dragPointer != null && m_dragPointer != e.Pointer.PointerId))
            {
                return;
            }

            if (((UIElement)sender).CapturePointer(e.Pointer))
            {
                StopAnimation();

                // A crop restored from an older edit may sit a hair outside the image, which would
                // refuse the whole drag.
                Constrain();

                m_dragPointer = e.Pointer.PointerId;
                m_dragEdges = ((FrameworkElement)sender).Tag is Edges edges ? edges : default;

                BeginDrag(e.GetCurrentPoint(LayoutRoot).Position);
                ShowGrid(false);

                if (!m_dragZooming)
                {
                    m_dragZooming = true;
                    m_dragTime = null;
                    CompositionTarget.Rendering += OnDragRendering;
                }
            }

            e.Handled = true;
        }

        private void Thumb_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (m_dragPointer != e.Pointer.PointerId || sender is not FrameworkElement { Tag: Edges edges })
            {
                return;
            }

            var position = e.GetCurrentPoint(LayoutRoot).Position;
            m_dragPosition = position;

            ResizeCrop(edges, (position.X - m_dragStart.X) / m_dragScale, (position.Y - m_dragStart.Y) / m_dragScale);
            UpdateView();

            e.Handled = true;
        }

        // Every resize is measured from here, so starting over from the current state keeps the
        // dragged edge under the pointer after the view has zoomed out by itself.
        private void BeginDrag(Point position)
        {
            m_dragStart = position;
            m_dragPosition = position;
            m_dragArea = m_area;
            m_dragCenter = m_center;
            m_dragCropSize = m_cropSize;
            m_dragScale = ViewScale;
        }

        /// <summary>
        /// While a dragged edge is held past the viewport, grows the crop outwards by itself, faster
        /// the further past it the pointer is, so a small crop can grow back without letting go:
        /// the pointer alone soon runs out of room.
        /// </summary>
        private void OnDragRendering(object sender, object e)
        {
            if (e is not RenderingEventArgs args)
            {
                return;
            }

            var previous = m_dragTime ?? args.RenderingTime;
            m_dragTime = args.RenderingTime;

            var seconds = (args.RenderingTime - previous).TotalSeconds;
            if (seconds <= 0 || m_dragPointer == null)
            {
                return;
            }

            var position = m_dragPosition;

            var x = (m_dragEdges & Edges.Left) != 0 ? -Overshoot(-position.X)
                : (m_dragEdges & Edges.Right) != 0 ? Overshoot(position.X - m_viewport.Width)
                : 0;

            var y = (m_dragEdges & Edges.Top) != 0 ? -Overshoot(-position.Y)
                : (m_dragEdges & Edges.Bottom) != 0 ? Overshoot(position.Y - m_viewport.Height)
                : 0;

            if (x == 0 && y == 0)
            {
                return;
            }

            x *= m_cropSize.Width * seconds;
            y *= m_cropSize.Height * seconds;

            // A corner past the viewport on one axis only would grow along that axis alone, while
            // the zoom drew its other edge away from the pointer: that edge follows the pointer.
            var scale = ViewScale;

            if (x == 0 && (m_dragEdges & (Edges.Left | Edges.Right)) != 0)
            {
                var edge = (m_dragEdges & Edges.Left) != 0 ? m_area.Left : m_area.Right;
                x = (position.X - edge) / scale;
            }

            if (y == 0 && (m_dragEdges & (Edges.Top | Edges.Bottom)) != 0)
            {
                var edge = (m_dragEdges & Edges.Top) != 0 ? m_area.Top : m_area.Bottom;
                y = (position.Y - edge) / scale;
            }

            BeginDrag(position);

            ResizeCrop(m_dragEdges, x, y);
            UpdateView();
        }

        // The share of the crop's size it grows by each second, for a pointer this far past the
        // viewport: up to about seven times larger every second.
        private static double Overshoot(double distance)
        {
            return distance <= 0 ? 0 : 2 * Math.Min(distance / 64, 1);
        }

        private void StopDragZoom()
        {
            if (m_dragZooming)
            {
                m_dragZooming = false;
                CompositionTarget.Rendering -= OnDragRendering;
            }
        }

        private void Thumb_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            if (m_dragPointer != e.Pointer.PointerId)
            {
                return;
            }

            m_dragPointer = null;
            ((UIElement)sender).ReleasePointerCapture(e.Pointer);

            StopDragZoom();
            HideGrid();

            // The area grows back to fill the view once the edge is let go.
            var position = AreaCenter;
            var scale = ViewScale;
            var area = m_area;

            m_area = FitArea(m_cropSize);
            AnimateFrom(position, scale, m_angle, area, 300);

            Changed?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
        }

        /// <summary>
        /// Moves the dragged edges by an offset in image pixels, measured along the crop's own axes.
        ///
        /// The resize happens in the image rather than on screen: the area already fills the view
        /// in one direction, so an edge held against the view could otherwise never move outwards.
        /// Instead the view zooms out about the opposite edge for as long as there is image left to
        /// take in, and the image's own edges are what stop the drag.
        /// </summary>
        private void ResizeCrop(Edges edges, double x, double y)
        {
            var minimum = MinimumAreaSize / m_dragScale;

            // The crop frame: the area's axes, centred on the crop as it was when the drag began.
            var halfWidth = m_dragCropSize.Width / 2;
            var halfHeight = m_dragCropSize.Height / 2;

            var left = -halfWidth;
            var top = -halfHeight;
            var right = halfWidth;
            var bottom = halfHeight;

            if ((edges & Edges.Left) != 0)
            {
                left = Math.Min(left + x, right - minimum);
            }
            else if ((edges & Edges.Right) != 0)
            {
                right = Math.Max(right + x, left + minimum);
            }

            if ((edges & Edges.Top) != 0)
            {
                top = Math.Min(top + y, bottom - minimum);
            }
            else if ((edges & Edges.Bottom) != 0)
            {
                bottom = Math.Max(bottom + y, top + minimum);
            }

            var ratio = GetLockedRatio();
            if (ratio != 0)
            {
                var horizontal = (edges & (Edges.Left | Edges.Right)) != 0;
                var vertical = (edges & (Edges.Top | Edges.Bottom)) != 0;

                var width = right - left;
                var height = bottom - top;

                if (horizontal && vertical)
                {
                    if (width / height > ratio)
                    {
                        width = height * ratio;
                    }
                    else
                    {
                        height = width / ratio;
                    }
                }
                else if (horizontal)
                {
                    height = width / ratio;
                }
                else
                {
                    width = height * ratio;
                }

                // A side handle grows the other axis about the centre, a corner away from the
                // opposite corner.
                left = (edges & Edges.Left) != 0 ? right - width
                    : (edges & Edges.Right) != 0 ? left
                    : -width / 2;
                right = left + width;

                top = (edges & Edges.Top) != 0 ? bottom - height
                    : (edges & Edges.Bottom) != 0 ? top
                    : -height / 2;
                bottom = top + height;

                // Growing about the centre, a crop against a border could not grow at all from a
                // side handle: slide the other axis back inside the image instead, and back off
                // only as far as no slide can make up for. Not a frame, which may reach past the
                // image: Constrain settles it after the drag.
                if (horizontal != vertical && !m_frame && !Fits(left, top, right, bottom))
                {
                    var low = 0d;
                    var high = 1d;

                    if (!TrySlide(left, top, right, bottom, vertical, out _))
                    {
                        for (int i = 0; i < 20; i++)
                        {
                            var middle = (low + high) / 2;
                            if (TrySlide(Lerp(-halfWidth, left, middle), Lerp(-halfHeight, top, middle), Lerp(halfWidth, right, middle), Lerp(halfHeight, bottom, middle), vertical, out _))
                            {
                                low = middle;
                            }
                            else
                            {
                                high = middle;
                            }
                        }

                        left = Lerp(-halfWidth, left, low);
                        top = Lerp(-halfHeight, top, low);
                        right = Lerp(halfWidth, right, low);
                        bottom = Lerp(halfHeight, bottom, low);
                    }

                    TrySlide(left, top, right, bottom, vertical, out double shift);

                    if (vertical)
                    {
                        left += shift;
                        right += shift;
                    }
                    else
                    {
                        top += shift;
                        bottom += shift;
                    }
                }
            }

            // Past the image, back off along the drag to the furthest crop that still fits.
            if (!Fits(left, top, right, bottom))
            {
                if (ratio == 0)
                {
                    // An axis at a time: a corner held against one border must still slide along it.
                    var t = Reach(-halfWidth, -halfHeight, halfWidth, halfHeight, left, -halfHeight, right, halfHeight);
                    left = Lerp(-halfWidth, left, t);
                    right = Lerp(halfWidth, right, t);

                    t = Reach(left, -halfHeight, right, halfHeight, left, top, right, bottom);
                    top = Lerp(-halfHeight, top, t);
                    bottom = Lerp(halfHeight, bottom, t);
                }
                else
                {
                    // Together, which keeps the anchor and the ratio, since both ends of the search do.
                    var t = Reach(-halfWidth, -halfHeight, halfWidth, halfHeight, left, top, right, bottom);
                    left = Lerp(-halfWidth, left, t);
                    top = Lerp(-halfHeight, top, t);
                    right = Lerp(halfWidth, right, t);
                    bottom = Lerp(halfHeight, bottom, t);
                }
            }

            // The point that holds still, in the crop frame and on screen.
            var anchorX = (edges & Edges.Left) != 0 ? halfWidth : (edges & Edges.Right) != 0 ? -halfWidth : 0;
            var anchorY = (edges & Edges.Top) != 0 ? halfHeight : (edges & Edges.Bottom) != 0 ? -halfHeight : 0;

            var screenX = m_dragArea.X + m_dragArea.Width / 2 + anchorX * m_dragScale;
            var screenY = m_dragArea.Y + m_dragArea.Height / 2 + anchorY * m_dragScale;

            // Zoom out only as far as keeping the area inside the view needs.
            var scale = m_dragScale;

            if (left < anchorX)
            {
                scale = Math.Min(scale, screenX / (anchorX - left));
            }

            if (right > anchorX)
            {
                scale = Math.Min(scale, (m_viewport.Width - screenX) / (right - anchorX));
            }

            if (top < anchorY)
            {
                scale = Math.Min(scale, screenY / (anchorY - top));
            }

            if (bottom > anchorY)
            {
                scale = Math.Min(scale, (m_viewport.Height - screenY) / (bottom - anchorY));
            }

            var offset = Rotate((left + right) / 2, (top + bottom) / 2, -m_angle);

            m_center = new Point(m_dragCenter.X + offset.X, m_dragCenter.Y + offset.Y);
            m_cropSize = new Size(right - left, bottom - top);
            m_area = new Rect(screenX + (left - anchorX) * scale, screenY + (top - anchorY) * scale, (right - left) * scale, (bottom - top) * scale);

            // Fits allows a hair past the image, and the search can stop anywhere inside that. Kept,
            // the slack adds up over drags until the starting crop no longer fits and every move is
            // refused, so the crop is snapped back exactly inside.
            Constrain();
        }

        /// <summary>
        /// How far from one crop towards another, as a fraction, the crop can go and still fit.
        /// The first must fit.
        /// </summary>
        private double Reach(double fromLeft, double fromTop, double fromRight, double fromBottom, double toLeft, double toTop, double toRight, double toBottom)
        {
            if (Fits(toLeft, toTop, toRight, toBottom))
            {
                return 1;
            }

            var low = 0d;
            var high = 1d;

            for (int i = 0; i < 20; i++)
            {
                var middle = (low + high) / 2;
                if (Fits(Lerp(fromLeft, toLeft, middle), Lerp(fromTop, toTop, middle), Lerp(fromRight, toRight, middle), Lerp(fromBottom, toBottom, middle)))
                {
                    low = middle;
                }
                else
                {
                    high = middle;
                }
            }

            return low;
        }

        /// <summary>
        /// The smallest slide along one of the crop frame's axes that brings a crop, given in the
        /// drag's crop frame, inside the image; false when no slide can. Each corner keeps inside
        /// over an interval of slides, so the slides that work are where those intervals overlap.
        /// </summary>
        private bool TrySlide(double left, double top, double right, double bottom, bool alongX, out double shift)
        {
            var oriented = OrientedSize;

            var radians = -m_angle * Math.PI / 180;
            var sin = Math.Sin(radians);
            var cos = Math.Cos(radians);

            // How a unit of slide moves a corner in the image.
            var stepX = alongX ? cos : -sin;
            var stepY = alongX ? sin : cos;

            var minimum = double.NegativeInfinity;
            var maximum = double.PositiveInfinity;

            Limit(left, top);
            Limit(right, top);
            Limit(left, bottom);
            Limit(right, bottom);

            shift = minimum <= maximum ? Math.Clamp(0, minimum, maximum) : 0;
            return minimum <= maximum;

            void Limit(double x, double y)
            {
                Interval(m_dragCenter.X + x * cos - y * sin, stepX, oriented.Width);
                Interval(m_dragCenter.Y + x * sin + y * cos, stepY, oriented.Height);
            }

            void Interval(double value, double step, double size)
            {
                const double Epsilon = 1e-6;

                if (Math.Abs(step) < 1e-9)
                {
                    if (value < -Epsilon || value > size + Epsilon)
                    {
                        maximum = double.NegativeInfinity;
                    }

                    return;
                }

                var a = (-Epsilon - value) / step;
                var b = (size + Epsilon - value) / step;

                minimum = Math.Max(minimum, Math.Min(a, b));
                maximum = Math.Min(maximum, Math.Max(a, b));
            }
        }

        /// <summary>
        /// Whether a crop, given in the drag's crop frame, lies inside the image: all four corners do.
        /// </summary>
        private bool Fits(double left, double top, double right, double bottom)
        {
            const double Epsilon = 0.01;

            var oriented = OrientedSize;

            if (m_frame)
            {
                return FitsFrame(left, top, right, bottom, oriented, Epsilon);
            }

            var radians = -m_angle * Math.PI / 180;
            var sin = Math.Sin(radians);
            var cos = Math.Cos(radians);

            return Inside(left, top) && Inside(right, top) && Inside(left, bottom) && Inside(right, bottom);

            bool Inside(double x, double y)
            {
                var imageX = m_dragCenter.X + x * cos - y * sin;
                var imageY = m_dragCenter.Y + x * sin + y * cos;

                return imageX >= -Epsilon && imageX <= oriented.Width + Epsilon
                    && imageY >= -Epsilon && imageY <= oriented.Height + Epsilon;
            }
        }

        // Constrain's rule for a frame, measured along the frame's axes against the image's bounds
        // there: the frame may be larger than them along either axis, but not along both, and along
        // such an axis they stay inside it rather than covering it.
        private bool FitsFrame(double left, double top, double right, double bottom, Size oriented, double epsilon)
        {
            var radians = m_angle * Math.PI / 180;
            var sin = Math.Abs(Math.Sin(radians));
            var cos = Math.Abs(Math.Cos(radians));

            var imageWidth = oriented.Width * cos + oriented.Height * sin;
            var imageHeight = oriented.Width * sin + oriented.Height * cos;

            var width = right - left;
            var height = bottom - top;

            if (width > imageWidth + epsilon && height > imageHeight + epsilon)
            {
                return false;
            }

            // The image's centre from the frame's, in the frame's axes.
            var offset = Rotate(oriented.Width / 2 - m_dragCenter.X, oriented.Height / 2 - m_dragCenter.Y, m_angle);
            var x = offset.X - (left + right) / 2;
            var y = offset.Y - (top + bottom) / 2;

            return Math.Abs(x) <= Math.Abs(imageWidth - width) / 2 + epsilon
                && Math.Abs(y) <= Math.Abs(imageHeight - height) / 2 + epsilon;
        }

        private void NWSEThumb_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            this.SetPointerCursor(PointerCursorType.SizeNorthwestSoutheast);
        }

        private void NESWThumb_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            this.SetPointerCursor(PointerCursorType.SizeNortheastSouthwest);
        }

        private void WEThumb_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            this.SetPointerCursor(PointerCursorType.SizeWestEast);
        }

        private void NSThumb_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            this.SetPointerCursor(PointerCursorType.SizeNorthSouth);
        }

        #endregion

        #region Rotation

        public void BeginRotation()
        {
            StopAnimation();

            m_rotating = true;
            m_rotationCropSize = m_cropSize;

            ShowGrid(true);
        }

        /// <summary>
        /// Turns the image under the crop. The zoom grows only as far as covering
        /// the crop needs, and gives that back as the angle returns.
        /// </summary>
        public void SetAngle(double angle)
        {
            if (!CanLayout)
            {
                return;
            }

            m_angle = Math.Clamp(angle, -MaximumAngle, MaximumAngle);

            if (m_rotating)
            {
                m_cropSize = m_rotationCropSize;
            }

            Constrain();
            UpdateView();
        }

        public void EndRotation()
        {
            m_rotating = false;

            HideGrid();
            Changed?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// A quarter turn counter-clockwise. It clears the free angle and starts the crop over,
        /// inverting a locked ratio.
        /// </summary>
        public void RotateCounterclockwise()
        {
            if (!CanLayout)
            {
                return;
            }

            StopAnimation();

            var position = AreaCenter;
            var scale = ViewScale;
            var angle = m_angle;
            var area = m_area;

            var previous = GetOrientation();
            var source = Vector2.Transform(new Vector2((float)m_center.X, (float)m_center.Y), Invert(previous));

            m_quarterTurns = (m_quarterTurns + 3) % 4;
            m_angle = 0;

            // The frame keeps its shape; the image turns inside it.
            if (!m_frame)
            {
                m_proportions = RotateProportions(m_proportions);
            }

            var previousCenter = Vector2.Transform(source, GetOrientation());

            ResetCrop();
            m_area = FitArea(m_cropSize);

            // The old view is the new one turned a quarter clockwise.
            AnimateFrom(GetStartPosition(position, scale, angle + 90, new Point(previousCenter.X, previousCenter.Y)), scale, angle + 90, area, 250);

            Changed?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Mirrors the view about the crop's vertical axis. Mirroring after a turn is the same as
        /// mirroring the source before the opposite turn, so the quarter turns and the angle negate.
        /// </summary>
        public void Mirror()
        {
            if (!CanLayout)
            {
                return;
            }

            StopAnimation();

            var source = Vector2.Transform(new Vector2((float)m_center.X, (float)m_center.Y), Invert(GetOrientation()));

            var angle = m_angle;

            m_mirrored = !m_mirrored;
            m_quarterTurns = (4 - m_quarterTurns) % 4;
            m_angle = -m_angle;

            var center = Vector2.Transform(source, GetOrientation());
            m_center = new Point(center.X, center.Y);

            // The same source point stays under the crop centre, so only the flip and the angle move.
            AnimateFrom(AreaCenter, ViewScale, angle, m_area, 250, -1);

            Changed?.Invoke(this, EventArgs.Empty);
        }

        private static Matrix3x2 Invert(Matrix3x2 matrix)
        {
            Matrix3x2.Invert(matrix, out Matrix3x2 inverse);
            return inverse;
        }

        public static BitmapProportions RotateProportions(BitmapProportions proportions)
        {
            return proportions switch
            {
                BitmapProportions.TwoOverThree => BitmapProportions.ThreeOverTwo,
                BitmapProportions.ThreeOverFive => BitmapProportions.FiveOverThree,
                BitmapProportions.ThreeOverFour => BitmapProportions.FourOverThree,
                BitmapProportions.FourOverFive => BitmapProportions.FiveOverFour,
                BitmapProportions.FiveOverSeven => BitmapProportions.SevenOverFive,
                BitmapProportions.NineOverSixteen => BitmapProportions.SixteenOverNine,
                BitmapProportions.ThreeOverTwo => BitmapProportions.TwoOverThree,
                BitmapProportions.FiveOverThree => BitmapProportions.ThreeOverFive,
                BitmapProportions.FourOverThree => BitmapProportions.ThreeOverFour,
                BitmapProportions.FiveOverFour => BitmapProportions.FourOverFive,
                BitmapProportions.SevenOverFive => BitmapProportions.FiveOverSeven,
                BitmapProportions.SixteenOverNine => BitmapProportions.NineOverSixteen,
                _ => proportions
            };
        }

        #endregion

        #region Reset

        // A frame keeps the proportions it was given.
        private BitmapProportions DefaultProportions => m_frame
            ? m_proportions
            : m_mask == ImageCropperMask.Ellipse
            ? BitmapProportions.Square
            : BitmapProportions.Custom;

        public bool IsDefault
        {
            get
            {
                if (!m_hasSource)
                {
                    return true;
                }

                var oriented = OrientedSize;
                var crop = GetDefaultCropSize();

                return m_quarterTurns == 0
                    && !m_mirrored
                    && m_angle == 0
                    && m_proportions == DefaultProportions
                    && Math.Abs(m_cropSize.Width - crop.Width) < 0.5
                    && Math.Abs(m_cropSize.Height - crop.Height) < 0.5
                    && Math.Abs(m_center.X - oriented.Width / 2) < 0.5
                    && Math.Abs(m_center.Y - oriented.Height / 2) < 0.5;
            }
        }

        /// <summary>
        /// Back to the untouched image, animated: the old view is the new one
        /// turned by every quarter and the free angle, and mirrored if it was.
        /// </summary>
        public void Reset()
        {
            if (!m_hasSource)
            {
                return;
            }

            if (!CanLayout)
            {
                m_quarterTurns = 0;
                m_mirrored = false;
                m_angle = 0;
                m_proportions = DefaultProportions;

                ResetCrop();
                Changed?.Invoke(this, EventArgs.Empty);
                return;
            }

            StopAnimation();

            var position = AreaCenter;
            var scale = ViewScale;
            var area = m_area;
            var mirror = m_mirrored ? -1 : 1;

            // The shorter way round: three quarters clockwise is one counter-clockwise.
            var angle = m_angle + m_quarterTurns * 90;
            if (angle > 180)
            {
                angle -= 360;
            }

            // With no turns and no mirror, the new frame is the upright image itself.
            var source = Vector2.Transform(new Vector2((float)m_center.X, (float)m_center.Y), Invert(GetOrientation()));

            m_quarterTurns = 0;
            m_mirrored = false;
            m_angle = 0;
            m_proportions = DefaultProportions;

            ResetCrop();
            m_area = FitArea(m_cropSize);

            AnimateFrom(GetStartPosition(position, scale, angle, new Point(source.X, source.Y), mirror), scale, angle, area, 250, mirror);

            Changed?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// The crop, the turns and the mirror as they are now, for <see cref="SetState"/>.
        /// </summary>
        public ImageCropperState GetState()
        {
            return new ImageCropperState(m_quarterTurns, m_mirrored, m_angle, m_center, m_cropSize, m_proportions);
        }

        /// <summary>
        /// Puts back what <see cref="GetState"/> took, at once.
        /// </summary>
        public void SetState(ImageCropperState state)
        {
            if (!m_hasSource || state == null)
            {
                return;
            }

            StopAnimation();

            m_quarterTurns = state.QuarterTurns;
            m_mirrored = state.Mirrored;
            m_angle = state.Angle;
            m_center = state.Center;
            m_cropSize = state.CropSize;
            m_proportions = state.Proportions;

            Refit();

            Changed?.Invoke(this, EventArgs.Empty);
        }

        #endregion

        #region Proportions

        /// <summary>
        /// Locks the crop to a ratio, taking the largest crop of it the image allows, centred
        /// where the crop is now.
        /// </summary>
        public void SetProportions(BitmapProportions value, bool animate = true)
        {
            m_proportions = value;

            if (!m_hasSource || GetLockedRatio() == 0)
            {
                Changed?.Invoke(this, EventArgs.Empty);
                return;
            }

            StopAnimation();

            var position = AreaCenter;
            var scale = ViewScale;
            var area = m_area;
            var previousCenter = m_center;

            m_cropSize = GetDefaultCropSize();
            Constrain();

            if (CanLayout)
            {
                m_area = FitArea(m_cropSize);

                if (animate)
                {
                    AnimateFrom(GetStartPosition(position, scale, m_angle, previousCenter), scale, m_angle, area, 250);
                }
                else
                {
                    UpdateView();
                }
            }

            Changed?.Invoke(this, EventArgs.Empty);
        }

        private double GetProportionsFactor(BitmapProportions proportions, double defaultValue)
        {
            switch (proportions)
            {
                case BitmapProportions.Original:
                    var oriented = OrientedSize;
                    return oriented.Width / oriented.Height;
                case BitmapProportions.Square:
                    return 1.0;
                // Portrait
                case BitmapProportions.TwoOverThree:
                    return 2.0 / 3.0;
                case BitmapProportions.ThreeOverFive:
                    return 3.0 / 5.0;
                case BitmapProportions.ThreeOverFour:
                    return 3.0 / 4.0;
                case BitmapProportions.FourOverFive:
                    return 4.0 / 5.0;
                case BitmapProportions.FiveOverSeven:
                    return 5.0 / 7.0;
                case BitmapProportions.NineOverSixteen:
                    return 9.0 / 16.0;
                // Landscape
                case BitmapProportions.ThreeOverTwo:
                    return 3.0 / 2.0;
                case BitmapProportions.FiveOverThree:
                    return 5.0 / 3.0;
                case BitmapProportions.FourOverThree:
                    return 4.0 / 3.0;
                case BitmapProportions.FiveOverFour:
                    return 5.0 / 4.0;
                case BitmapProportions.SevenOverFive:
                    return 7.0 / 5.0;
                case BitmapProportions.SixteenOverNine:
                    return 16.0 / 9.0;
                default:
                    return defaultValue;
            }
        }

        /// <summary>
        /// The presets for the crop as it stands, portrait or landscape by the crop area's shape,
        /// not the image's.
        /// </summary>
        public IReadOnlyList<BitmapProportions> GetProportions()
        {
            return m_hasSource
                ? GetProportionsFor(m_cropSize.Width, m_cropSize.Height)
                : GetProportionsFor(1, 1);
        }

        public static IReadOnlyList<BitmapProportions> GetProportionsFor(double width, double height)
        {
            if (width > height)
            {
                return new List<BitmapProportions>
                {
                    BitmapProportions.Original,
                    BitmapProportions.Square,
                    BitmapProportions.ThreeOverTwo,
                    BitmapProportions.FiveOverThree,
                    BitmapProportions.FourOverThree,
                    BitmapProportions.FiveOverFour,
                    BitmapProportions.SevenOverFive,
                    BitmapProportions.SixteenOverNine
                };
            }

            return new List<BitmapProportions>
            {
                BitmapProportions.Original,
                BitmapProportions.Square,
                BitmapProportions.TwoOverThree,
                BitmapProportions.ThreeOverFive,
                BitmapProportions.ThreeOverFour,
                BitmapProportions.FourOverFive,
                BitmapProportions.FiveOverSeven,
                BitmapProportions.NineOverSixteen
            };
        }

        #endregion

        #region Mask

        public void SetMask(ImageCropperMask mask)
        {
            m_mask = mask;

            if (mask == ImageCropperMask.Ellipse)
            {
                m_proportions = BitmapProportions.Square;
            }

            var clip = Clip;
            if (clip == null)
            {
                return;
            }

            var group = new GeometryGroup();
            group.Children.Add(m_outerClip = new RectangleGeometry());
            group.Children.Add(m_innerClip = mask == ImageCropperMask.Rectangle
                ? new RectangleGeometry()
                : new EllipseGeometry());

            // The path lives in the image's own pixels; the hole is laid out in view pixels and
            // carried back through the inverse of the image's transform.
            m_innerClip.Transform = m_innerTransform = new MatrixTransform();

            clip.Data = group;
            UpdateView();
        }

        #endregion

        #region Properties

        public int PixelWidth => (int)OrientedSize.Width;
        public int PixelHeight => (int)OrientedSize.Height;

        public BitmapProportions Proportions => m_proportions;

        public ImageRotation Orientation => (ImageRotation)m_quarterTurns;

        public ImageFlip Flip => m_mirrored ? ImageFlip.Horizontal : ImageFlip.None;

        public double Angle => m_angle;

        /// <summary>
        /// View pixels per source pixel.
        /// </summary>
        public double ZoomFactor => CanLayout ? ViewScale * m_viewZoom : 1;

        /// <summary>
        /// The content's pixels to the view's, the draw mode's magnification included.
        /// </summary>
        public Matrix3x2 ContentTransform => m_contentTransform;

        /// <summary>
        /// The crop as it appears in the view, the draw mode's magnification included.
        /// </summary>
        public Rect ViewArea => m_viewArea;

        public Size ViewportSize => m_viewport;

        /// <summary>
        /// The crop before <see cref="Angle"/>, normalized to the oriented image. Snapped to the
        /// whole image when it is within rounding of it, so an untouched crop reads as no crop.
        /// </summary>
        public Rect CropRectangle
        {
            get
            {
                if (!m_hasSource)
                {
                    return new Rect(0, 0, 1, 1);
                }

                var oriented = OrientedSize;

                var x = Snap((m_center.X - m_cropSize.Width / 2) / oriented.Width);
                var y = Snap((m_center.Y - m_cropSize.Height / 2) / oriented.Height);
                var width = Snap(m_cropSize.Width / oriented.Width);
                var height = Snap(m_cropSize.Height / oriented.Height);

                return new Rect(x, y, width, height);
            }
        }

        private static double Snap(double value)
        {
            if (Math.Abs(value) < 1e-6)
            {
                return 0;
            }
            else if (Math.Abs(value - 1) < 1e-6)
            {
                return 1;
            }

            return value;
        }

        private bool _isCropEnabled = true;
        public bool IsCropEnabled
        {
            get => _isCropEnabled;
            set
            {
                _isCropEnabled = value;
                VisualStateManager.GoToState(this, value ? "Normal" : "Disabled", true);

                UpdateLayoutClip();

                // The magnification belongs to draw mode; the crop handles work on the fitted view.
                if (value)
                {
                    ResetView();
                }
            }
        }

        public ImageCropperMask Mask => m_mask;

        /// <summary>
        /// The crop is a frame of the locked proportions that the image moves and zooms under, and
        /// it may be zoomed out until the image fits inside it, leaving it empty around the image.
        /// Its handles resize it at those proportions, which is zooming by other means. Set before
        /// the source.
        /// </summary>
        public bool IsFrame
        {
            get => m_frame;
            set => m_frame = value;
        }

        #endregion

        #region Source

        public async Task SetSourceAsync(StorageFile file, ImageRotation rotation = ImageRotation.None, ImageFlip flip = ImageFlip.None, BitmapProportions proportions = BitmapProportions.Custom, Rect? cropRectangle = null, double angle = 0)
        {
            try
            {
                // Off the UI thread from the start: a large PNG has no reduced decode, so it is
                // read whole, and none of that should wait on the UI thread in between.
                var software = await Task.Run(() => DecodeAsync(file));

                var source = new SoftwareBitmapSource();
                await source.SetBitmapAsync(software);

                SetSource(source, software.PixelWidth, software.PixelHeight, rotation, flip, angle, proportions, cropRectangle);

                SourceDecoded?.Invoke(this, software);
            }
            catch
            {
                // TODO: decide how to handle failures in opening the image
            }
        }

        // Upright only: the edit's own turns and mirror are part of the view transform.
        private static async Task<SoftwareBitmap> DecodeAsync(StorageFile file)
        {
            using var fileStream = await ImageHelper.OpenReadAsync(file);

            var decoder = await BitmapDecoder.CreateAsync(fileStream);
            var transform = ImageHelper.ComputeScalingTransformForSourceImage(decoder);

            return await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied, transform, ExifOrientationMode.RespectExifOrientation, ColorManagementMode.DoNotColorManage);
        }

        private void SetSource(ImageSource source, double width, double height, ImageRotation rotation, ImageFlip flip, double angle, BitmapProportions proportions, Rect? cropRectangle)
        {
            ImageViewer.Source = source;
            ImageThumb.Width = width;
            ImageThumb.Height = height;

            m_sourceSize = new Size(width, height);
            m_hasSource = true;

            m_quarterTurns = (int)rotation & 3;
            m_mirrored = flip != ImageFlip.None;

            // A vertical flip is a horizontal one turned a half: states saved before mirroring
            // became horizontal-only still describe the same image.
            if (flip == ImageFlip.Vertical)
            {
                m_quarterTurns = (m_quarterTurns + 2) % 4;
            }

            m_angle = Math.Clamp(angle, -MaximumAngle, MaximumAngle);
            m_proportions = m_mask == ImageCropperMask.Ellipse
                ? BitmapProportions.Square
                : proportions;

            if (cropRectangle is Rect rectangle && rectangle.Width > 0 && rectangle.Height > 0)
            {
                var oriented = OrientedSize;

                m_cropSize = new Size(rectangle.Width * oriented.Width, rectangle.Height * oriented.Height);
                m_center = new Point((rectangle.X + rectangle.Width / 2) * oriented.Width, (rectangle.Y + rectangle.Height / 2) * oriented.Height);

                Constrain();
            }
            else
            {
                ResetCrop();
            }

            Refit();

            Changed?.Invoke(this, EventArgs.Empty);
        }

        #endregion
    }
}
