//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Effects;
using Microsoft.Graphics.Canvas.Geometry;
using Microsoft.Graphics.Canvas.Text;
using Microsoft.Graphics.Canvas.UI;
using Microsoft.Graphics.Canvas.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Controls.Media;
using Telegram.Controls.Messages;
using Telegram.Controls.Stories.Widgets;
using Telegram.Entities;
using Telegram.Native;
using Telegram.Navigation;
using Telegram.Services;
using Telegram.Streams;
using Telegram.Td.Api;
using Windows.Devices.Input;
using Windows.Foundation;
using Windows.Graphics.DirectX;
using Windows.System;
using Windows.UI;
using Windows.UI.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Shapes;

namespace Telegram.Controls
{
    /// <summary>
    /// The media editor's text and stickers. It sits in the cropper beside the drawing canvas, so
    /// it is laid out in the upright image's pixels and turns with it; everything measured on
    /// screen goes through the view transform the cropper hands it. Each object is an element of
    /// its own, so stickers play and objects stack in the order they were last selected; the
    /// selection and the guides are drawn on one canvas over them all.
    /// </summary>
    public sealed partial class PaintEntityLayer : Grid
    {
        private enum Gesture
        {
            None,
            Move,
            Handle,
            ShapeHandle,
            Pinch
        }

        // A shape's handles, each its own index in every shape.
        private const int ShapeCorner = 0;
        private const int ShapeRotate = 4;
        private const int ShapeTail = 5;
        private const int ShapeRadius = 6;
        private const int ArrowStart = 7;
        private const int ArrowBend = 8;
        private const int ArrowTip = 9;
        private const int ArrowHead = 10;

        // In view pixels.
        private const float ShapeHandleHitRadius = 20;
        private const float ShapeRotateGap = 24;
        private const float ShapeHandleRadius = 6;

        // Where on a star its handle sits: on the point clockwise from the top one.
        private const double StarHandleAngle = -Math.PI / 2 + 2 * Math.PI / 5;

        private static readonly float[] CornerX = { -1, 1, 1, -1 };
        private static readonly float[] CornerY = { -1, -1, 1, 1 };

        private readonly int[] _shapeHandleIds = new int[7];
        private readonly Vector2[] _shapeHandlePoints = new Vector2[7];
        private readonly List<Ellipse> _handleTargets = new();

        // In view pixels.
        private const float HandleHitRadius = 19.5f;
        private const float PanThreshold = 6;
        private const float HandleThreshold = 2;
        private const float FramePaddingX = 10;
        private const float FramePaddingY = 6;

        // How far the overlay reaches past the view on each side, into the cropper's padding.
        private const float OverlayMargin = 48;
        private const double StickyPositionTrigger = 12;
        private const double StickyPadding = 8;

        private const double StickyAngleTrigger = 4 * Math.PI / 180;
        private const double StickyAngleThreshold = 12 * Math.PI / 180;

        // In milliseconds.
        private const double SelectDuration = 280;
        private const double FlipDuration = 500;
        private const double MenuDelay = 500;

        private const float MinimumScale = 0.1f;
        private const float MaximumScale = 30;

        // A reaction's, against the size of a story area.
        private const float ReactionMinimumScale = 0.5f;
        private const float ReactionMaximumScale = 1.8f;

        private const float WeatherMaximumScale = 1.5f;
        private const float LinkMaximumScale = 1.5f;

        private static readonly double[] StickyAngles = { -Math.PI / 2, 0, Math.PI / 2, Math.PI, -Math.PI };

        private readonly List<PaintEntity> _entities = new();
        private readonly Dictionary<PaintEntity, PaintTextVisual> _visuals = new();
        private readonly Dictionary<PaintEntity, Border> _elements = new();
        private readonly Dictionary<PaintEntity, float> _rasters = new();

        // A link's layout and its preview's photo, on Win2D's shared device, which the elements'
        // canvases draw with too.
        private readonly Dictionary<PaintEntity, PaintLinkVisual> _links = new();
        private readonly Dictionary<PaintEntity, CanvasBitmap> _linkPhotos = new();
        private List<PaintEntity> _saved = new();

        private readonly Stopwatch _clock = Stopwatch.StartNew();
        private readonly SolidColorBrush _hitBrush = new(Colors.Transparent);

        private CanvasStrokeStyle _dashStyle;
        private CanvasStrokeStyle _guideStyle;

        private Matrix3x2 _view = Matrix3x2.Identity;
        private Matrix3x2 _inverse = Matrix3x2.Identity;
        private Rect _area;
        private Size _viewport;

        private PaintEntity _selected;
        private PaintEntity _editing;
        private bool _updatingEditor;

        private Gesture _gesture;
        private readonly Dictionary<uint, Vector2> _pointers = new();
        private Vector2 _pressPoint;
        private double _pressTime;
        private bool _pressedSelected;
        private bool _pressedEmpty;
        private bool _panned;

        private Vector2 _moveStart;
        private Vector2 _rawCenter;

        private bool _leftHandle;
        private int _shapeHandle;
        private float _handleScale;
        private float _handleDistance;
        private double _rawAngle;

        private readonly Sticky _stickyX = new();
        private readonly Sticky _stickyY = new();
        private readonly Sticky _stickyAngle = new();
        private readonly double[] _stickyTargets = new double[3];

        // A text's element is sized from its layout, which needs the canvas' device: any asked for
        // before it exists are redone on the next draw.
        private bool _elementsInvalid;

        private PaintEntity _flipping;
        private double _flipProgress = 1;

        private double _selectProgress = 1;
        private bool _rendering;
        private double _lastTick;

        public PaintEntityLayer()
        {
            InitializeComponent();
        }

        public event EventHandler SelectionChanged;

        /// <summary>
        /// Raised for objects the person adds, which undo can take away again.
        /// </summary>
        public event EventHandler<PaintEntityEventArgs> EntityCreated;

        /// <summary>
        /// Raised for objects the person deletes. Deleting cannot be undone, so whatever undo
        /// held for the object should be dropped.
        /// </summary>
        public event EventHandler<PaintEntityEventArgs> EntityDeleted;

        /// <summary>
        /// Raised for a link the person asks to edit, which is done outside this layer and then
        /// handed to <see cref="RefreshLink"/>.
        /// </summary>
        public event EventHandler<PaintEntityEventArgs> LinkEditRequested;

        public PaintEntity Selected => _selected;

        /// <summary>
        /// Downloads the files of stickers and GIFs as they are shown.
        /// </summary>
        public IClientService ClientService { get; set; }

        #region HandleBrush

        /// <summary>
        /// The fill of the selection's handles.
        /// </summary>
        public Brush HandleBrush
        {
            get => (Brush)GetValue(HandleBrushProperty);
            set => SetValue(HandleBrushProperty, value);
        }

        public static readonly DependencyProperty HandleBrushProperty =
            DependencyProperty.Register("HandleBrush", typeof(Brush), typeof(PaintEntityLayer), new PropertyMetadata(null));

        #endregion

        public bool IsEditing => _editing != null;

        public bool IsEditorFocused => Editor != null && Editor.FocusState != FocusState.Unfocused;

        /// <summary>
        /// The objects as of the last <see cref="SaveState"/>, or null when there are none.
        /// </summary>
        public IReadOnlyList<PaintEntity> Items
        {
            get => _saved.Count > 0 ? CloneAll(_saved) : null;
            set
            {
                _saved = value != null ? CloneAll(value) : new List<PaintEntity>();
                Load(_saved);
            }
        }

        public void SaveState()
        {
            EndEditing();
            Deselect();

            _saved = CloneAll(_entities);
        }

        public void RestoreState()
        {
            Load(_saved);
        }

        private static List<PaintEntity> CloneAll(IReadOnlyList<PaintEntity> entities)
        {
            var clones = new List<PaintEntity>(entities.Count);

            foreach (var entity in entities)
            {
                clones.Add(entity.Clone());
            }

            return clones;
        }

        private void Load(IReadOnlyList<PaintEntity> entities)
        {
            EndGesture();

            if (_editing != null)
            {
                _editing = null;
                Editor.Visibility = Visibility.Collapsed;
            }

            var selected = _selected != null;

            _selected = null;
            Background = null;

            foreach (var visual in _visuals.Values)
            {
                visual.Dispose();
            }

            foreach (var element in _elements.Values)
            {
                DetachElement(element);
            }

            ClearLinks();

            _entities.Clear();
            _visuals.Clear();
            _elements.Clear();
            _rasters.Clear();
            Objects.Children.Clear();

            foreach (var entity in entities)
            {
                AddEntity(entity.Clone());
            }

            Canvas.Invalidate();

            if (selected)
            {
                SelectionChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// Where the cropper shows the image: <paramref name="transform"/> takes this layer's
        /// pixels to the view, <paramref name="area"/> is the crop in the view, and
        /// <paramref name="viewport"/> is how much of the view is visible.
        /// </summary>
        public void SetView(Matrix3x2 transform, Rect area, Size viewport)
        {
            if (transform == _view && area == _area && viewport == _viewport)
            {
                return;
            }

            if (viewport != _viewport)
            {
                Canvas.Width = viewport.Width + OverlayMargin * 2;
                Canvas.Height = viewport.Height + OverlayMargin * 2;
            }

            _view = transform;
            _area = area;
            _viewport = viewport;

            if (!Matrix3x2.Invert(transform, out _inverse))
            {
                _inverse = Matrix3x2.Identity;
            }

            ((MatrixTransform)Canvas.RenderTransform).Matrix = ToMatrix(Matrix3x2.CreateTranslation(-OverlayMargin, -OverlayMargin) * _inverse);

            if (_editing != null)
            {
                UpdateEditor();
            }

            Canvas.Invalidate();
        }

        #region Geometry

        private Vector2 ImageSize => ActualSize;

        private float Zoom => (float)Math.Sqrt(Math.Abs(_view.GetDeterminant()));

        // Mirroring the image never mirrors text: an object turns over with the image while its
        // own content is flipped back.
        private bool Mirrored => _view.GetDeterminant() < 0;

        private int Sign => Mirrored ? -1 : 1;

        // The angle on screen of an unturned object's reading direction.
        private double BaseAngle
        {
            get
            {
                var axis = Vector2.TransformNormal(new Vector2(Mirrored ? -1 : 1, 0), _view);
                return Math.Atan2(axis.Y, axis.X);
            }
        }

        private double GetScreenAngle(PaintEntity entity)
        {
            return Normalize(BaseAngle + Sign * entity.Rotation);
        }

        private float GetRotation(double screenAngle)
        {
            return (float)Normalize(Sign * (screenAngle - BaseAngle));
        }

        private static double Normalize(double angle)
        {
            angle %= 2 * Math.PI;

            if (angle > Math.PI)
            {
                angle -= 2 * Math.PI;
            }
            else if (angle <= -Math.PI)
            {
                angle += 2 * Math.PI;
            }

            return angle;
        }

        private Vector2 ToView(Vector2 point)
        {
            return Vector2.Transform(point, _view);
        }

        private Vector2 FromView(Vector2 point)
        {
            return Vector2.Transform(point, _inverse);
        }

        private Vector2 GetViewCenter(PaintEntity entity)
        {
            return ToView(entity.Position * ImageSize);
        }

        private void SetViewCenter(PaintEntity entity, Vector2 center)
        {
            var size = ImageSize;
            if (size.X > 0 && size.Y > 0)
            {
                entity.Position = FromView(center) / size;
            }
        }

        private Matrix3x2 GetTransform(PaintEntity entity, Rect bounds)
        {
            return PaintEntityRenderer.GetTransform(entity, bounds, ImageSize, Mirrored);
        }

        // An object's own units per view pixel.
        private float GetUnit(PaintEntity entity)
        {
            var scale = entity.Scale * ImageSize.X / PaintEntityRenderer.Reference * Zoom;
            return scale > 0 ? 1 / scale : 1;
        }

        // What the object covers, in the renderer's units; false while a text has no layout yet.
        private bool TryGetBounds(PaintEntity entity, out Rect bounds)
        {
            if (entity.Type == PaintEntityType.Shape)
            {
                bounds = PaintEntityRenderer.GetShapeBounds(entity);
                return true;
            }
            else if (PaintEntityRenderer.IsSticker(entity))
            {
                bounds = PaintEntityRenderer.GetStickerBounds(entity);
                return true;
            }
            else if (entity.Type == PaintEntityType.Reaction)
            {
                bounds = PaintEntityRenderer.GetReactionBounds();
                return true;
            }
            else if (entity.Type == PaintEntityType.Weather)
            {
                bounds = PaintEntityRenderer.GetWidgetBounds(entity);
                return true;
            }
            else if (entity.Type == PaintEntityType.Link)
            {
                var size = GetLinkVisual(entity).Size * PaintLinkVisual.Unit;

                bounds = new Rect(0, 0, size.X, size.Y);
                return true;
            }

            var visual = GetVisual(entity);
            bounds = visual?.Bounds ?? default;

            return visual != null;
        }

        // The selection's frame: around a text or a shape, or the square around a sticker's or a
        // reaction's circle.
        private Rect GetFrame(PaintEntity entity, Rect bounds)
        {
            var unit = GetUnit(entity);

            if (IsRound(entity))
            {
                var radius = Math.Max(bounds.Width, bounds.Height) / 2 + FramePaddingX * unit;
                var x = bounds.X + bounds.Width / 2;
                var y = bounds.Y + bounds.Height / 2;

                return new Rect(x - radius, y - radius, radius * 2, radius * 2);
            }

            return new Rect(
                bounds.X - FramePaddingX * unit,
                bounds.Y - FramePaddingY * unit,
                bounds.Width + FramePaddingX * unit * 2,
                bounds.Height + FramePaddingY * unit * 2);
        }

        private static bool IsRound(PaintEntity entity)
        {
            return PaintEntityRenderer.IsSticker(entity) || entity.Type == PaintEntityType.Reaction;
        }

        private static float ClampScale(PaintEntity entity, float scale)
        {
            return entity.Type switch
            {
                PaintEntityType.Reaction => Math.Clamp(scale, ReactionMinimumScale, ReactionMaximumScale),
                PaintEntityType.Weather => Math.Clamp(scale, MinimumScale, WeatherMaximumScale),
                PaintEntityType.Link => Math.Clamp(scale, MinimumScale, LinkMaximumScale),
                _ => Math.Clamp(scale, MinimumScale, MaximumScale)
            };
        }

        private PaintTextVisual GetVisual(PaintEntity entity)
        {
            if (_visuals.TryGetValue(entity, out PaintTextVisual visual))
            {
                if (visual.Matches(entity, GetPlaceholder(entity)))
                {
                    return visual;
                }

                visual.Dispose();
                _visuals.Remove(entity);
            }

            if (!Canvas.ReadyToDraw)
            {
                return null;
            }

            visual = PaintEntityRenderer.CreateText(Canvas, entity, GetPlaceholder(entity));
            _visuals[entity] = visual;

            return visual;
        }

        // While being written an empty text takes the placeholder's size, so its frame shows.
        private string GetPlaceholder(PaintEntity entity)
        {
            return entity == _editing ? Strings.TextPlaceholder : null;
        }

        private bool Contains(PaintEntity entity, Vector2 point, bool padded)
        {
            if (!TryGetBounds(entity, out Rect bounds) || !Matrix3x2.Invert(GetTransform(entity, bounds), out Matrix3x2 inverse))
            {
                return false;
            }

            var local = Vector2.Transform(point, inverse);

            if (entity is { Type: PaintEntityType.Shape, Shape: PaintShapeKind.Arrow })
            {
                var reach = PaintEntityRenderer.GetShapeThickness(entity) + (padded ? FramePaddingX * 2 : FramePaddingX) * GetUnit(entity);
                return DistanceToSegment(local, entity.ArrowStart, entity.ArrowBend) <= reach
                    || DistanceToSegment(local, entity.ArrowBend, entity.ArrowTip) <= reach;
            }

            var area = padded ? GetFrame(entity, bounds) : bounds;

            return local.X >= area.Left && local.X <= area.Right && local.Y >= area.Top && local.Y <= area.Bottom;
        }

        private static float DistanceToSegment(Vector2 point, Vector2 from, Vector2 to)
        {
            var segment = to - from;
            var length = segment.LengthSquared();
            var t = length > 0 ? Math.Clamp(Vector2.Dot(point - from, segment) / length, 0, 1) : 0;

            return Vector2.Distance(point, from + segment * t);
        }

        private PaintEntity HitEntity(Vector2 point)
        {
            for (int i = _entities.Count - 1; i >= 0; i--)
            {
                var entity = _entities[i];
                if (Contains(entity, point, entity == _selected))
                {
                    return entity;
                }
            }

            return null;
        }

        private Vector2 GetHandle(PaintEntity entity, Rect bounds, bool left)
        {
            var frame = GetFrame(entity, bounds);
            var local = new Vector2((float)(left ? frame.Left : frame.Right), (float)(frame.Y + frame.Height / 2));

            return ToView(Vector2.Transform(local, GetTransform(entity, bounds)));
        }

        // -1 for the left handle, 1 for the right, 0 for neither.
        private int HitHandle(Vector2 view)
        {
            if (_selected == null || !TryGetBounds(_selected, out Rect bounds))
            {
                return 0;
            }

            if (Vector2.Distance(view, GetHandle(_selected, bounds, true)) <= HandleHitRadius)
            {
                return -1;
            }

            if (Vector2.Distance(view, GetHandle(_selected, bounds, false)) <= HandleHitRadius)
            {
                return 1;
            }

            return 0;
        }

        // Half the size, in the view, of the box the turned object fills.
        private Vector2 GetHalfExtents(PaintEntity entity)
        {
            if (!TryGetBounds(entity, out Rect bounds))
            {
                return Vector2.Zero;
            }

            var transform = GetTransform(entity, bounds) * _view;

            var a = Vector2.Transform(new Vector2((float)bounds.Left, (float)bounds.Top), transform);
            var b = Vector2.Transform(new Vector2((float)bounds.Right, (float)bounds.Top), transform);
            var c = Vector2.Transform(new Vector2((float)bounds.Right, (float)bounds.Bottom), transform);
            var d = Vector2.Transform(new Vector2((float)bounds.Left, (float)bounds.Bottom), transform);

            var min = Vector2.Min(Vector2.Min(a, b), Vector2.Min(c, d));
            var max = Vector2.Max(Vector2.Max(a, b), Vector2.Max(c, d));

            return (max - min) / 2;
        }

        #endregion

        #region Entities

        private void AddEntity(PaintEntity entity)
        {
            _entities.Add(entity);

            // Wrapped so that the whole box takes presses, not only what is drawn in it.
            var element = new Border
            {
                Background = _hitBrush,
                RenderTransform = new MatrixTransform()
            };

            if (entity.Type == PaintEntityType.Reaction)
            {
                element.Child = new StoryReactionWidget
                {
                    Child = new AnimatedImage
                    {
                        AutoPlay = true,
                        FrameSize = new Size(160, 160),
                        DecodeFrameType = DecodePixelType.Logical
                    }
                };
            }
            else if (entity.Type == PaintEntityType.Weather)
            {
                var widget = new StoryWeatherWidget();
                widget.Update(ClientService, entity.WeatherEmoji, entity.WeatherTemperature, PaintEntityRenderer.GetWidgetBackground(entity.WidgetStyle));

                // Measured once, when placed: its size then goes into the story area.
                if (entity.WidgetSize == Vector2.Zero)
                {
                    var size = widget.GetNaturalSize();
                    entity.WidgetSize = new Vector2((float)size.Width, (float)size.Height);
                }

                element.Child = new Viewbox
                {
                    Child = widget
                };
            }
            else if (entity.Type == PaintEntityType.Animation)
            {
                // Played as the GIF drawer plays them: uncached, at their own size.
                element.Child = new AnimatedImage
                {
                    AutoPlay = true,
                    IsCachingEnabled = false,
                    ResizeMode = AnimatedImageResizeMode.Fill,
                    FrameSize = new Size(0, 0),
                    DecodeFrameType = DecodePixelType.Logical,
                    Source = CreateSource(entity)
                };
            }
            else if (PaintEntityRenderer.IsSticker(entity))
            {
                element.Child = new AnimatedImage
                {
                    AutoPlay = true,
                    FrameSize = new Size(512, 512),
                    DecodeFrameType = DecodePixelType.Physical,
                    ReplacementColor = entity.StickerTinted ? new SolidColorBrush(Colors.White) : null,
                    Source = CreateSource(entity)
                };
            }
            else
            {
                var canvas = new CanvasControl
                {
                    Tag = entity
                };

                canvas.Draw += Element_Draw;

                if (entity is { Type: PaintEntityType.Shape, Shape: PaintShapeKind.Arrow })
                {
                    // Only the line takes presses: an arrow's box is mostly empty, and presses
                    // there belong to the drawing below.
                    element.Background = null;
                    canvas.IsHitTestVisible = false;

                    var grid = new Grid();
                    grid.Children.Add(canvas);
                    grid.Children.Add(new Polyline
                    {
                        Stroke = _hitBrush,
                        StrokeLineJoin = PenLineJoin.Round,
                        StrokeStartLineCap = PenLineCap.Round,
                        StrokeEndLineCap = PenLineCap.Round
                    });

                    element.Child = grid;
                }
                else
                {
                    element.Child = canvas;
                }
            }

            _elements[entity] = element;
            Objects.Children.Add(element);

            if (entity.Type == PaintEntityType.Link)
            {
                LoadLinkPhoto(entity);
            }

            UpdateElement(entity, true);
            Canvas.Invalidate();
        }

        // Laid out once, with its measured size kept on the object for its story area.
        private PaintLinkVisual GetLinkVisual(PaintEntity entity)
        {
            if (_links.TryGetValue(entity, out PaintLinkVisual visual))
            {
                return visual;
            }

            visual = PaintLinkVisual.Create(CanvasDevice.GetSharedDevice(), entity);
            entity.WidgetSize = visual.Size;

            _links[entity] = visual;
            return visual;
        }

        private async void LoadLinkPhoto(PaintEntity entity)
        {
            if (ClientService == null || string.IsNullOrEmpty(entity.LinkPhotoRemoteId))
            {
                return;
            }

            if (!await PaintEntityRenderer.LoadLinkPhotoFileAsync(ClientService, entity))
            {
                return;
            }

            CanvasBitmap bitmap;

            try
            {
                bitmap = await CanvasBitmap.LoadAsync(CanvasDevice.GetSharedDevice(), entity.LinkPhotoFile.Local.Path);
            }
            catch
            {
                return;
            }

            // Taken away, or edited to another photo, while it loaded.
            if (!_elements.ContainsKey(entity) || _linkPhotos.ContainsKey(entity))
            {
                bitmap.Dispose();
                return;
            }

            _linkPhotos[entity] = bitmap;
            Invalidate(entity);
        }

        private void ReleaseLink(PaintEntity entity)
        {
            if (_links.TryGetValue(entity, out PaintLinkVisual visual))
            {
                visual.Dispose();
                _links.Remove(entity);
            }

            if (_linkPhotos.TryGetValue(entity, out CanvasBitmap photo))
            {
                photo.Dispose();
                _linkPhotos.Remove(entity);
            }
        }

        private void ClearLinks()
        {
            foreach (var visual in _links.Values)
            {
                visual.Dispose();
            }

            foreach (var photo in _linkPhotos.Values)
            {
                photo.Dispose();
            }

            _links.Clear();
            _linkPhotos.Clear();
        }

        /// <summary>
        /// Lays a link out again after its address, text or preview changed.
        /// </summary>
        public void RefreshLink(PaintEntity entity)
        {
            if (!_elements.ContainsKey(entity))
            {
                return;
            }

            ReleaseLink(entity);
            LoadLinkPhoto(entity);

            UpdateElement(entity, true);
            Invalidate(entity);
        }

        // Shown as soon as the file is on disk, downloading it if it is not yet.
        private AnimatedImageSource CreateSource(PaintEntity entity)
        {
            if (ClientService == null || entity.StickerFile == null)
            {
                return null;
            }

            return new DelayedFileSource(ClientService, entity.StickerFile)
            {
                NeedsRepainting = entity.StickerTinted
            };
        }

        private static CanvasControl GetCanvas(Border element)
        {
            return element.Child as CanvasControl ?? (element.Child as Grid)?.Children[0] as CanvasControl;
        }

        private void DetachElement(Border element)
        {
            // Win2D holds on to a control's resources until it is told to let go.
            if (GetCanvas(element) is CanvasControl canvas)
            {
                canvas.Draw -= Element_Draw;
                canvas.RemoveFromVisualTree();
            }
        }

        private void RemoveEntity(PaintEntity entity, bool deleted)
        {
            if (entity == _editing)
            {
                _editing = null;
                Editor.Visibility = Visibility.Collapsed;
            }

            var selected = entity == _selected;
            if (selected)
            {
                EndGesture();

                _selected = null;
                Background = null;
            }

            _entities.Remove(entity);

            if (_visuals.TryGetValue(entity, out PaintTextVisual visual))
            {
                visual.Dispose();
                _visuals.Remove(entity);
            }

            ReleaseLink(entity);

            if (_elements.TryGetValue(entity, out Border element))
            {
                DetachElement(element);
                Objects.Children.Remove(element);

                _elements.Remove(entity);
                _rasters.Remove(entity);
            }

            Canvas.Invalidate();

            if (deleted)
            {
                EntityDeleted?.Invoke(this, new PaintEntityEventArgs(entity));
            }

            if (selected)
            {
                SelectionChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        // A text is drawn at the size it had in this layer's pixels when it last settled, and only
        // stretched in between, so a drag does not remake its surface on every move.
        private void UpdateElement(PaintEntity entity, bool settle = false)
        {
            if (!_elements.TryGetValue(entity, out Border element))
            {
                return;
            }

            var k = ImageSize.X / PaintEntityRenderer.Reference;

            if (!TryGetBounds(entity, out Rect bounds) || k <= 0)
            {
                _elementsInvalid = true;
                return;
            }

            float raster;

            if (IsRound(entity) || entity.Type == PaintEntityType.Weather)
            {
                raster = k;
            }
            else
            {
                var exact = entity.Scale * k;

                if (settle || !_rasters.TryGetValue(entity, out raster) || exact / raster is < 0.8f or > 1.25f)
                {
                    raster = exact;
                    _rasters[entity] = raster;

                    GetCanvas(element)?.Invalidate();
                }
            }

            element.Width = bounds.Width * raster;
            element.Height = bounds.Height * raster;

            var matrix = Matrix3x2.CreateScale(1 / raster)
                * Matrix3x2.CreateTranslation((float)bounds.X, (float)bounds.Y)
                * GetFlip(entity, bounds)
                * GetTransform(entity, bounds);

            ((MatrixTransform)element.RenderTransform).Matrix = ToMatrix(matrix);

            if (element.Child is StoryReactionWidget widget)
            {
                UpdateReaction(entity, widget);
            }
            else if (element.Child is Viewbox { Child: StoryWeatherWidget weather })
            {
                weather.UpdateBackground(PaintEntityRenderer.GetWidgetBackground(entity.WidgetStyle));
            }
            else if (element.Child is Grid grid && grid.Children.Count > 1 && grid.Children[1] is Polyline line)
            {
                var points = new PointCollection();

                foreach (var point in new[] { entity.ArrowStart, entity.ArrowBend, entity.ArrowTip })
                {
                    points.Add(new Point((point.X - bounds.X) * raster, (point.Y - bounds.Y) * raster));
                }

                line.Points = points;
                line.StrokeThickness = (PaintEntityRenderer.GetShapeThickness(entity) * 2 + FramePaddingX * 2 * GetUnit(entity)) * raster;
            }
        }

        // A flip turns the sticker over like a card: squeezed through its width with a slight
        // shear, from the side it was on to the new one. A reaction is never shown back to front:
        // its bubble turns over at the halfway point, while it is edge on.
        private Matrix3x2 GetFlip(PaintEntity entity, Rect bounds)
        {
            if (entity != _flipping || _flipProgress >= 1)
            {
                return Matrix3x2.Identity;
            }

            var t = GetFlipTurn();
            var squeeze = entity.Type == PaintEntityType.Reaction
                ? Math.Abs(2 * t - 1)
                : 2 * t - 1;

            var center = new Vector2((float)(bounds.X + bounds.Width / 2), (float)(bounds.Y + bounds.Height / 2));

            return Matrix3x2.CreateScale((float)squeeze, 1, center)
                * Matrix3x2.CreateSkew(0, (float)Math.Atan(t * (1 - t)), center);
        }

        private double GetFlipTurn()
        {
            return 1 - Math.Pow(1 - _flipProgress, 5);
        }

        private void UpdateReaction(PaintEntity entity, StoryReactionWidget widget)
        {
            var turning = entity == _flipping && _flipProgress < 1 && GetFlipTurn() < 0.5;

            widget.IsDark = entity.ReactionDark;
            widget.IsFlipped = entity.Mirror != turning;

            if (widget.Child is not AnimatedImage icon)
            {
                return;
            }

            var reaction = entity.GetReactionType();

            if (icon.Tag is not ReactionType shown || !shown.AreTheSame(reaction))
            {
                icon.Tag = reaction;

                // A custom emoji plays on, while an emoji's animation comes to rest.
                icon.LoopCount = reaction is ReactionTypeCustomEmoji ? 0 : 1;
                icon.Source = ClientService != null
                    ? new ReactionFileSource(ClientService, reaction) { UseCenterAnimation = true, IsAnimated = false }
                    : null;
            }

            // A reaction drawn in a single colour takes the bubble's.
            if (icon.ReplacementColor is not SolidColorBrush tint || tint.Color != widget.TintColor)
            {
                icon.ReplacementColor = new SolidColorBrush(widget.TintColor);
            }
        }

        private void UpdateElements()
        {
            foreach (var entity in _entities)
            {
                UpdateElement(entity, true);
            }
        }

        private static Matrix ToMatrix(Matrix3x2 matrix)
        {
            return new Matrix(matrix.M11, matrix.M12, matrix.M21, matrix.M22, matrix.M31, matrix.M32);
        }

        // Where a new object goes: the middle of what the view shows, stepped down and to the right
        // by a fraction of the crop until it lands on nothing else. The scale makes the renderer's
        // width as wide as the crop.
        private bool TryGetPlacement(out Vector2 position, out float scale)
        {
            position = default;
            scale = 1;

            var image = ImageSize;
            var zoom = Zoom;

            if (image.X <= 0 || zoom <= 0 || _area.Width <= 0)
            {
                return false;
            }

            var visibleLeft = Math.Max(_area.Left, 0);
            var visibleTop = Math.Max(_area.Top, 0);
            var visibleRight = Math.Min(_area.Right, _viewport.Width);
            var visibleBottom = Math.Min(_area.Bottom, _viewport.Height);

            var visible = visibleRight > visibleLeft && visibleBottom > visibleTop
                ? new Rect(visibleLeft, visibleTop, visibleRight - visibleLeft, visibleBottom - visibleTop)
                : _area;

            var step = (float)(_area.Width * 200 / 1280);
            var clearance = _area.Width * 100 / 1280;
            var center = new Vector2((float)(visible.X + visible.Width / 2), (float)(visible.Y + visible.Height / 2));

            for (int i = 0; i < 8 && IsOccupied(center, clearance); i++)
            {
                center += new Vector2(step, step);
            }

            position = FromView(center) / image;
            scale = (float)(_area.Width / (image.X * zoom));

            return true;
        }

        /// <summary>
        /// Places an empty text where the view is looking and opens it for writing.
        /// </summary>
        public void AddText(Color color, float size, PaintTextFrame frame, PaintTextAlignment alignment, PaintTextFont font)
        {
            EndEditing();

            if (!TryGetPlacement(out Vector2 position, out float scale))
            {
                return;
            }

            var entity = new PaintEntity
            {
                Type = PaintEntityType.Text,
                Text = string.Empty,
                Color = color,
                TextSize = size,
                Frame = frame,
                Alignment = alignment,
                Font = font,
                Position = position,
                Rotation = GetRotation(0),
                Scale = scale
            };

            AddEntity(entity);
            EntityCreated?.Invoke(this, new PaintEntityEventArgs(entity));

            BeginEditing(entity);
        }

        /// <summary>
        /// Places a sticker, a custom emoji or a GIF where the view is looking, its longer side
        /// three quarters of the crop's shorter one.
        /// </summary>
        public void AddSticker(PaintEntityType type, File file, int width, int height, bool tinted)
        {
            EndEditing();

            if (!TryGetPlacement(out Vector2 position, out float scale))
            {
                return;
            }

            // By the crop's shorter side: by its width alone, a wide and short crop would get a
            // sticker taller than itself. The box is half the renderer's width.
            scale *= (float)Math.Min(1, _area.Height / _area.Width) * 1.5f;

            var entity = new PaintEntity
            {
                Type = type,
                StickerFile = file,
                StickerRemoteId = file.Remote.Id,
                StickerWidth = width,
                StickerHeight = height,
                StickerTinted = tinted,
                Position = position,
                Rotation = GetRotation(0),
                Scale = scale
            };

            AddEntity(entity);
            EntityCreated?.Invoke(this, new PaintEntityEventArgs(entity));

            Select(entity);
        }

        /// <summary>
        /// Places a suggested reaction where the view is looking, at the size of a story area,
        /// unless the story already holds as many as it can.
        /// </summary>
        public void AddReaction(ReactionType reaction)
        {
            EndEditing();

            if (!CanAddReaction() || !TryGetPlacement(out Vector2 position, out float scale))
            {
                return;
            }

            var entity = new PaintEntity
            {
                Type = PaintEntityType.Reaction,
                Position = position,
                Rotation = GetRotation(0),
                Scale = Math.Clamp(scale, ReactionMinimumScale, ReactionMaximumScale)
            };

            entity.SetReactionType(reaction);

            AddEntity(entity);
            EntityCreated?.Invoke(this, new PaintEntityEventArgs(entity));

            Select(entity);
        }

        /// <summary>
        /// Places the weather where the view is looking, as large as the other clients first show
        /// it.
        /// </summary>
        public void AddWeather(CurrentWeather weather)
        {
            EndEditing();

            if (!TryGetPlacement(out Vector2 position, out float scale))
            {
                return;
            }

            var entity = new PaintEntity
            {
                Type = PaintEntityType.Weather,
                WeatherEmoji = weather.Emoji,
                WeatherTemperature = weather.Temperature,
                Position = position,
                Rotation = GetRotation(0),
                Scale = Math.Min(scale, WeatherMaximumScale)
            };

            AddEntity(entity);
            EntityCreated?.Invoke(this, new PaintEntityEventArgs(entity));

            Select(entity);
        }

        public bool HasAny(PaintEntityType type)
        {
            foreach (var entity in _entities)
            {
                if (entity.Type == type)
                {
                    return true;
                }
            }

            return false;
        }

        private void CycleSelectedStyle()
        {
            if (_selected is { Type: PaintEntityType.Link, LinkHasPreview: true })
            {
                _selected.LinkDark = !_selected.LinkDark;
                Invalidate(_selected);
            }
            else if (_selected?.Type is PaintEntityType.Weather or PaintEntityType.Link)
            {
                _selected.WidgetStyle = (PaintWidgetStyle)(((int)_selected.WidgetStyle + 1) % 3);
                Invalidate(_selected);
            }
        }

        /// <summary>
        /// Places a link where the view is looking, as large as the other clients first show it,
        /// unless the story already holds as many as it can.
        /// </summary>
        public void AddLink(PaintEntity entity)
        {
            EndEditing();

            if (!CanAddLink() || !TryGetPlacement(out Vector2 position, out float scale))
            {
                return;
            }

            entity.Type = PaintEntityType.Link;
            entity.Position = position;
            entity.Rotation = GetRotation(0);
            entity.Scale = Math.Min(scale, LinkMaximumScale);

            AddEntity(entity);
            EntityCreated?.Invoke(this, new PaintEntityEventArgs(entity));

            Select(entity);
        }

        public bool CanAddLink()
        {
            var maximum = ClientService?.Options.StoryLinkAreaCountMax ?? 0;
            var count = 0;

            foreach (var entity in _entities)
            {
                if (entity.Type == PaintEntityType.Link)
                {
                    count++;
                }
            }

            if (count < maximum)
            {
                return true;
            }

            ToastPopup.Show(XamlRoot, Locale.Declension(Strings.R.StoryLinkLimitMessage, maximum), ToastPopupIcon.Info);
            return false;
        }

        private void EditSelectedLink()
        {
            if (_selected?.Type == PaintEntityType.Link)
            {
                LinkEditRequested?.Invoke(this, new PaintEntityEventArgs(_selected));
            }
        }

        private bool CanAddReaction()
        {
            var maximum = ClientService?.Options.StorySuggestedReactionAreaCountMax ?? 0;
            var count = 0;

            foreach (var entity in _entities)
            {
                if (entity.Type == PaintEntityType.Reaction)
                {
                    count++;
                }
            }

            if (count < maximum)
            {
                return true;
            }

            ToastPopup.Show(XamlRoot, Locale.Declension(Strings.R.StoryReactionsWidgetLimit2, maximum), ToastPopupIcon.Info);
            return false;
        }

        private bool IsOccupied(Vector2 center, double clearance)
        {
            foreach (var entity in _entities)
            {
                if (Vector2.Distance(GetViewCenter(entity), center) < clearance)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Takes away an object that undo is reversing, without raising
        /// <see cref="EntityDeleted"/>.
        /// </summary>
        public void Remove(PaintEntity entity)
        {
            RemoveEntity(entity, false);
        }

        /// <summary>
        /// Puts back an object that redo is restoring.
        /// </summary>
        public void Restore(PaintEntity entity)
        {
            if (!_entities.Contains(entity))
            {
                AddEntity(entity);
            }
        }

        public void DeleteSelected()
        {
            if (_selected != null)
            {
                RemoveEntity(_selected, true);
            }
        }

        public void EditSelected()
        {
            if (_selected?.Type == PaintEntityType.Text)
            {
                BeginEditing(_selected);
            }
        }

        public void DuplicateSelected()
        {
            var source = _selected;
            if (source == null)
            {
                return;
            }

            EndEditing();

            // Ending the edit takes away a text left empty.
            if (!_entities.Contains(source))
            {
                return;
            }

            if (source.Type == PaintEntityType.Weather || (source.Type == PaintEntityType.Reaction && !CanAddReaction()) || (source.Type == PaintEntityType.Link && !CanAddLink()))
            {
                return;
            }

            var offset = (float)(_area.Width * 200 / 1280);
            var copy = source.Clone();

            AddEntity(copy);
            SetViewCenter(copy, GetViewCenter(source) + new Vector2(offset, offset));
            UpdateElement(copy, true);

            EntityCreated?.Invoke(this, new PaintEntityEventArgs(copy));
            Select(copy);
        }

        /// <summary>
        /// Places a shape where the view is looking, a fifth of the crop's shorter side across.
        /// </summary>
        public void AddShape(PaintShapeKind kind, bool filled, Color color, float weight)
        {
            EndEditing();

            if (!TryGetPlacement(out Vector2 position, out float scale))
            {
                return;
            }

            // The renderer's width is the crop's at this scale.
            var height = (float)(PaintEntityRenderer.Reference * _area.Height / _area.Width);
            var radius = Math.Min(PaintEntityRenderer.Reference, height) / 5;

            var entity = new PaintEntity
            {
                Type = PaintEntityType.Shape,
                Shape = kind,
                ShapeFilled = filled,
                ShapeWeight = weight,
                Color = color,
                ShapeSize = new Vector2(radius),
                Position = position,
                Rotation = GetRotation(0),
                Scale = scale
            };

            entity.ShapeTail = new Vector2(radius * 0.8f, radius * 1.2f + PaintEntityRenderer.GetShapeThickness(entity));

            entity.ArrowTip = new Vector2(0, -height / 6);
            entity.ArrowBend = new Vector2(1, 0);
            entity.ArrowStart = new Vector2(0, height / 6);
            entity.ArrowHead = height / 6;

            AddEntity(entity);
            EntityCreated?.Invoke(this, new PaintEntityEventArgs(entity));

            Select(entity);
        }

        /// <summary>
        /// Places a shape found in a stroke where the stroke was drawn, in the canvas' pixels,
        /// which are this layer's. It is left unselected, so drawing goes on.
        /// </summary>
        public void AddShape(RecognizedShape shape, Color color, float weight)
        {
            var image = ImageSize;
            if (image.X <= 0 || image.Y <= 0)
            {
                return;
            }

            var entity = shape.ToEntity(image.X / PaintEntityRenderer.Reference, color, weight);
            entity.Position = shape.Center / image;

            // Its own content is flipped back in a mirrored view, so its points are flipped first
            // to land where they were drawn.
            if (Mirrored)
            {
                entity.ArrowStart *= new Vector2(-1, 1);
                entity.ArrowBend *= new Vector2(-1, 1);
                entity.ArrowTip *= new Vector2(-1, 1);
            }

            AddEntity(entity);
            EntityCreated?.Invoke(this, new PaintEntityEventArgs(entity));
        }

        public void SetSelectedShapeWeight(float weight)
        {
            if (_selected?.Type == PaintEntityType.Shape)
            {
                _selected.ShapeWeight = weight;
                Invalidate(_selected);
            }
        }

        public void SetSelectedShapeFilled(bool filled)
        {
            if (_selected?.Type == PaintEntityType.Shape)
            {
                _selected.ShapeFilled = filled;
                Invalidate(_selected);
            }
        }

        /// <summary>
        /// Turns a selected sticker over. Like moving it, this is not undone.
        /// </summary>
        public void FlipSelected()
        {
            if (_selected == null || !IsRound(_selected))
            {
                return;
            }

            _selected.Mirror = !_selected.Mirror;

            _flipping = _selected;
            _flipProgress = 0;

            StartRendering();
            UpdateElement(_selected);
        }

        public void SetSelectedColor(Color color)
        {
            if (_selected != null)
            {
                _selected.Color = color;
                Invalidate(_selected);
            }
        }

        public void SetSelectedTextSize(float size)
        {
            if (_selected != null)
            {
                _selected.TextSize = size;
                Invalidate(_selected);
            }
        }

        /// <summary>
        /// Redraws the selected object after its look was changed directly.
        /// </summary>
        public void InvalidateSelected()
        {
            if (_selected != null)
            {
                Invalidate(_selected);
            }
        }

        private void Invalidate(PaintEntity entity)
        {
            if (entity == _editing)
            {
                UpdateEditor();
            }

            UpdateElement(entity);

            if (_elements.TryGetValue(entity, out Border element))
            {
                GetCanvas(element)?.Invalidate();
            }

            Canvas.Invalidate();
        }

        #endregion

        #region Selection

        private void Select(PaintEntity entity)
        {
            if (_selected == entity)
            {
                return;
            }

            if (_editing != null && _editing != entity)
            {
                EndEditing();
            }

            _selected = entity;

            // Selecting brings an object to the front for good.
            _entities.Remove(entity);
            _entities.Add(entity);

            if (_elements.TryGetValue(entity, out Border element))
            {
                var index = Objects.Children.IndexOf(element);
                var last = Objects.Children.Count - 1;

                if (index >= 0 && index < last)
                {
                    Objects.Children.Move((uint)index, (uint)last);
                }
            }

            // While a text or a sticker is selected the whole layer takes presses: a tap elsewhere
            // puts it away rather than drawing, and a drag beside it moves it. A shape keeps the
            // drawing tools, so a press beside it draws, which puts it away.
            Background = entity.Type == PaintEntityType.Shape ? null : _hitBrush;

            _selectProgress = 0;
            StartRendering();

            Canvas.Invalidate();
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }

        public void Deselect()
        {
            if (_selected == null)
            {
                return;
            }

            var entity = _selected;

            EndEditing();

            // Ending the edit may have taken the object away, and the selection with it.
            if (_selected != entity)
            {
                return;
            }

            EndGesture();

            _selected = null;
            Background = null;

            Canvas.Invalidate();
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }

        private void ShowMenu(PaintEntity entity, Point position)
        {
            var flyout = new MenuFlyout();

            if (entity.Type == PaintEntityType.Text)
            {
                flyout.CreateFlyoutItem(EditSelected, Strings.PaintEdit, Icons.Edit);
            }
            else if (PaintEntityRenderer.IsSticker(entity))
            {
                flyout.CreateFlyoutItem(FlipSelected, Strings.AccDescrFlip, Icons.FlipHorizontal);
            }
            else if (entity.Type == PaintEntityType.Reaction)
            {
                var dark = new ToggleMenuFlyoutItem
                {
                    Text = Strings.ThemeDark,
                    IsChecked = entity.ReactionDark
                };

                dark.Click += Dark_Click;

                flyout.Items.Add(dark);
                flyout.CreateFlyoutItem(FlipSelected, Strings.AccDescrFlip, Icons.FlipHorizontal);

                // The reactions to choose from go above the menu, as over a message's.
                flyout.Opened += ReactionMenu_Opened;
            }
            else if (entity.Type == PaintEntityType.Link)
            {
                flyout.CreateFlyoutItem(EditSelectedLink, Strings.PaintEdit, Icons.Edit);
            }

            // A story takes one weather.
            if (entity.Type != PaintEntityType.Weather)
            {
                flyout.CreateFlyoutItem(DuplicateSelected, Strings.PaintDuplicate, Icons.DocumentCopy, VirtualKey.D);
            }

            flyout.CreateFlyoutItem(DeleteSelected, Strings.PaintDelete, Icons.Delete, VirtualKey.Delete, VirtualKeyModifiers.None, destructive: true);

            flyout.ShowAt(this, position);
        }

        private void Dark_Click(object sender, RoutedEventArgs e)
        {
            if (_selected?.Type == PaintEntityType.Reaction)
            {
                _selected.ReactionDark = !_selected.ReactionDark;
                UpdateElement(_selected);
            }
        }

        private async void ReactionMenu_Opened(object sender, object e)
        {
            var flyout = sender as MenuFlyout;
            flyout.Opened -= ReactionMenu_Opened;

            if (ClientService == null)
            {
                return;
            }

            var response = await ClientService.SendAsync(new GetStoryAvailableReactions(8));
            if (response is AvailableReactions reactions && flyout.IsOpen && reactions.TopReactions.Count + reactions.RecentReactions.Count + reactions.PopularReactions.Count > 0)
            {
                var picker = ReactionsMenuFlyout.ShowAt(ClientService, reactions, flyout);
                picker.ItemClick += Picker_ItemClick;
            }
        }

        private void Picker_ItemClick(object sender, AvailableReaction e)
        {
            if (_selected?.Type != PaintEntityType.Reaction)
            {
                return;
            }

            if (e.NeedsPremium && !ClientService.IsPremium)
            {
                ToastPopup.ShowFeaturePromo(WindowContext.GetNavigationService(this), new PremiumFeatureUniqueReactions());
                return;
            }

            _selected.SetReactionType(e.Type);
            UpdateElement(_selected);
        }

        #endregion

        #region Editing

        public void BeginEditing(PaintEntity entity)
        {
            if (entity.Type != PaintEntityType.Text)
            {
                return;
            }
            else if (entity == _editing)
            {
                Editor.Focus(FocusState.Programmatic);
                return;
            }

            Select(entity);

            if (Editor == null)
            {
                FindName(nameof(Editor));
            }

            _editing = entity;

            _updatingEditor = true;
            Editor.Text = entity.Text ?? string.Empty;
            _updatingEditor = false;

            UpdateEditor();

            Editor.Visibility = Visibility.Visible;
            Editor.Focus(FocusState.Programmatic);
            Editor.Select(Editor.Text.Length, 0);

            Invalidate(entity);
        }

        /// <summary>
        /// Stops writing; a text left empty is taken away.
        /// </summary>
        public void EndEditing()
        {
            var entity = _editing;
            if (entity == null)
            {
                return;
            }

            _editing = null;
            Editor.Visibility = Visibility.Collapsed;

            if (string.IsNullOrWhiteSpace(entity.Text))
            {
                RemoveEntity(entity, true);
            }
            else
            {
                Invalidate(entity);
            }
        }

        // The box is laid out in this layer's pixels, where the renderer's unit is k of them, and
        // placed over the drawn frame by the same transform the frame is drawn with. It is only as
        // wide as its text, wrapping at the same width the layout does, so the handles beside it
        // stay out from under it.
        private void UpdateEditor()
        {
            var entity = _editing;
            var visual = GetVisual(entity);

            if (visual == null)
            {
                return;
            }

            var k = ImageSize.X / PaintEntityRenderer.Reference;
            if (k <= 0)
            {
                return;
            }

            PaintEntityRenderer.GetColors(entity, out Color foreground, out _);
            PaintEntityRenderer.GetFont(entity.Font, out string family, out FontWeight weight, out FontStyle style, out FontStretch stretch);

            if (Editor.FontFamily?.Source != family)
            {
                Editor.FontFamily = new FontFamily(family);
            }

            Editor.FontWeight = weight;
            Editor.FontStyle = style;
            Editor.FontStretch = stretch;
            Editor.FontSize = PaintEntityRenderer.GetFontSize(entity) * k;
            Editor.MaxWidth = PaintEntityRenderer.MaxWidth * k;
            Editor.TextAlignment = entity.Alignment switch
            {
                PaintTextAlignment.Center => TextAlignment.Center,
                PaintTextAlignment.Right => TextAlignment.Right,
                _ => TextAlignment.Left
            };

            Editor.Foreground = new SolidColorBrush(foreground);
            Editor.SelectionHighlightColor = new SolidColorBrush(Color.FromArgb(0x66, foreground.R, foreground.G, foreground.B));

            // Its left edge is where the widest line starts, whatever the alignment.
            var origin = new Vector2((float)visual.Layout.LayoutBounds.X, 0);

            var matrix = Matrix3x2.CreateScale(1 / k) * Matrix3x2.CreateTranslation(origin) * GetTransform(entity, visual.Bounds);
            ((MatrixTransform)Editor.RenderTransform).Matrix = ToMatrix(matrix);
        }

        private void Editor_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_updatingEditor || _editing == null)
            {
                return;
            }

            _editing.Text = Editor.Text;
            Invalidate(_editing);
        }

        #endregion

        #region Input

        private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
        {
            var point = e.GetCurrentPoint(this);

            if (e.Pointer.PointerDeviceType == PointerDeviceType.Mouse && !point.Properties.IsLeftButtonPressed)
            {
                return;
            }

            var view = ToView(point.Position.ToVector2());

            // A second finger on a selected object turns the drag into a pinch.
            if (_pointers.Count > 0)
            {
                if (_pointers.Count == 1 && _selected != null && _gesture != Gesture.Handle && e.Pointer.PointerDeviceType == PointerDeviceType.Touch)
                {
                    _pointers[point.PointerId] = view;
                    CapturePointer(e.Pointer);

                    _gesture = Gesture.Pinch;
                    _panned = true;

                    StartRendering();
                    Canvas.Invalidate();
                }

                e.Handled = true;
                return;
            }

            // The text box keeps its own presses, so this one landed outside it: on a handle or the
            // text's own frame writing goes on, anywhere else it ends.
            var handle = HitHandle(view);
            if (_selected?.Type == PaintEntityType.Shape && HitShapeHandle(view) is int shapeHandle)
            {
                BeginShapeHandle(shapeHandle);
            }
            else if (handle != 0)
            {
                BeginHandle(handle < 0, view);
            }
            else
            {
                var hit = HitEntity(point.Position.ToVector2());
                if (hit != _editing)
                {
                    EndEditing();
                }

                if (hit != null)
                {
                    _pressedSelected = hit == _selected;
                    _pressedEmpty = false;

                    Select(hit);
                }
                else if (_selected != null)
                {
                    // Dragging beside the selected object still moves it.
                    _pressedSelected = false;
                    _pressedEmpty = true;
                }
                else
                {
                    return;
                }

                BeginMove();
            }

            _pointers[point.PointerId] = view;
            _pressPoint = view;
            _pressTime = _clock.Elapsed.TotalMilliseconds;
            _panned = false;

            CapturePointer(e.Pointer);
            e.Handled = true;
        }

        private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
        {
            var id = e.Pointer.PointerId;
            if (!_pointers.TryGetValue(id, out Vector2 previous) || _selected == null)
            {
                return;
            }

            var view = ToView(e.GetCurrentPoint(this).Position.ToVector2());

            if (_gesture == Gesture.Pinch)
            {
                MovePinch(id, previous, view);
                return;
            }

            _pointers[id] = view;

            if (!_panned)
            {
                var threshold = _gesture is Gesture.Handle or Gesture.ShapeHandle ? HandleThreshold : PanThreshold;
                if (Vector2.Distance(view, _pressPoint) < threshold)
                {
                    return;
                }

                _panned = true;
                StartRendering();
            }

            if (_gesture == Gesture.Move)
            {
                _rawCenter = _moveStart + (view - _pressPoint);
                ApplyMove();
            }
            else if (_gesture == Gesture.Handle)
            {
                MoveHandle(view);
            }
            else if (_gesture == Gesture.ShapeHandle)
            {
                MoveShapeHandle(view);
            }
        }

        private void OnPointerReleased(object sender, PointerRoutedEventArgs e)
        {
            if (!_pointers.Remove(e.Pointer.PointerId))
            {
                return;
            }

            ReleasePointerCapture(e.Pointer);

            if (_gesture == Gesture.Pinch)
            {
                // The finger left behind does nothing until it lifts too.
                if (_pointers.Count == 0)
                {
                    EndGesture();
                }

                return;
            }

            if (!_panned && _gesture == Gesture.Move)
            {
                if (_pressedEmpty)
                {
                    Deselect();
                }
                else if (_pressedSelected && (e.Pointer.PointerDeviceType == PointerDeviceType.Mouse || _clock.Elapsed.TotalMilliseconds - _pressTime < MenuDelay))
                {
                    // A second tap writes in a text and turns the weather or a link to its next fill;
                    // anything else gets its menu. A long press is left to RightTapped, or the menu
                    // would open twice.
                    if (_selected.Type == PaintEntityType.Text)
                    {
                        BeginEditing(_selected);
                    }
                    else if (_selected.Type is PaintEntityType.Weather or PaintEntityType.Link)
                    {
                        CycleSelectedStyle();
                    }
                    else
                    {
                        ShowMenu(_selected, e.GetCurrentPoint(this).Position);
                    }
                }
            }

            EndGesture();
        }

        private void OnPointerCanceled(object sender, PointerRoutedEventArgs e)
        {
            if (_pointers.Remove(e.Pointer.PointerId) && _pointers.Count == 0)
            {
                EndGesture();
            }
        }

        private void OnRightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            var position = e.GetPosition(this);
            var hit = HitEntity(position.ToVector2());

            if (hit != null)
            {
                Select(hit);
                ShowMenu(hit, position);

                e.Handled = true;
            }
        }

        private void BeginMove()
        {
            _gesture = Gesture.Move;
            _moveStart = _rawCenter = GetViewCenter(_selected);

            _stickyX.Reset();
            _stickyY.Reset();

            // An object already sitting on a guide, as a new one does, starts held there.
            var extents = GetHalfExtents(_selected);

            if (FindTarget(_rawCenter.X, GetStickyTargetsX(extents.X), 0.01) is double x)
            {
                _stickyX.Stick(x, Array.IndexOf(_stickyTargets, x));
            }

            if (FindTarget(_rawCenter.Y, GetStickyTargetsY(extents.Y), 0.01) is double y)
            {
                _stickyY.Stick(y, Array.IndexOf(_stickyTargets, y));
            }
        }

        private void ApplyMove()
        {
            var now = _clock.Elapsed.TotalMilliseconds;
            var extents = GetHalfExtents(_selected);

            _stickyX.Update(_rawCenter.X, _stickyTargets, GetStickyTargetsX(extents.X), StickyPositionTrigger, StickyPositionTrigger, now);
            _stickyY.Update(_rawCenter.Y, _stickyTargets, GetStickyTargetsY(extents.Y), StickyPositionTrigger, StickyPositionTrigger, now);

            var center = new Vector2((float)_stickyX.Apply(_rawCenter.X), (float)_stickyY.Apply(_rawCenter.Y));

            SetViewCenter(_selected, center);
            UpdateSelected();
        }

        // The crop's centre line, then its two edges less a margin: written into the shared
        // buffer, whose first entry is always the centre.
        private int GetStickyTargetsX(float extent)
        {
            _stickyTargets[0] = _area.X + _area.Width / 2;
            _stickyTargets[1] = _area.Left + StickyPadding + extent;
            _stickyTargets[2] = _area.Right - StickyPadding - extent;

            return _stickyTargets[1] < _stickyTargets[2] ? 3 : 1;
        }

        private int GetStickyTargetsY(float extent)
        {
            _stickyTargets[0] = _area.Y + _area.Height / 2;
            _stickyTargets[1] = _area.Top + StickyPadding + extent;
            _stickyTargets[2] = _area.Bottom - StickyPadding - extent;

            return _stickyTargets[1] < _stickyTargets[2] ? 3 : 1;
        }

        private double? FindTarget(double value, int count, double tolerance)
        {
            for (int i = 0; i < count; i++)
            {
                if (Math.Abs(value - _stickyTargets[i]) <= tolerance)
                {
                    return _stickyTargets[i];
                }
            }

            return null;
        }

        private void BeginHandle(bool left, Vector2 view)
        {
            _gesture = Gesture.Handle;
            _pressedSelected = false;
            _pressedEmpty = false;

            var center = GetViewCenter(_selected);

            _leftHandle = left;
            _handleScale = _selected.Scale;
            _handleDistance = Math.Max(1, Vector2.Distance(view, center));
            _rawAngle = GetScreenAngle(_selected);

            _stickyAngle.Reset();

            for (int i = 0; i < StickyAngles.Length; i++)
            {
                if (Math.Abs(_rawAngle - StickyAngles[i]) < 0.0001)
                {
                    _stickyAngle.Stick(StickyAngles[i], i);
                    break;
                }
            }
        }

        // Scales by how far the handle is from the centre and turns to face it.
        private void MoveHandle(Vector2 view)
        {
            var offset = view - GetViewCenter(_selected);
            var distance = offset.Length();

            if (distance < 1)
            {
                return;
            }

            _selected.Scale = ClampScale(_selected, _handleScale * distance / _handleDistance);
            _rawAngle = Normalize(Math.Atan2(offset.Y, offset.X) - (_leftHandle ? Math.PI : 0));

            ApplyAngle();
        }

        private void ApplyAngle()
        {
            _stickyAngle.Update(_rawAngle, StickyAngles, StickyAngles.Length, StickyAngleTrigger, StickyAngleThreshold, _clock.Elapsed.TotalMilliseconds);
            _selected.Rotation = GetRotation(_stickyAngle.Apply(_rawAngle));

            UpdateSelected();
        }

        #region Shapes

        // The handles of a shape, in its units, into the shared buffers: a circle has its radius, a
        // star its radius on one point, which also turns it, a rectangle or a bubble its corners and
        // a rotation handle above, a bubble its tail too, and an arrow its three points and its
        // head's size.
        private int GetShapeHandles(PaintEntity entity)
        {
            var count = 0;
            var size = Vector2.Abs(entity.ShapeSize);
            var radius = Math.Min(size.X, size.Y);

            void Add(int id, Vector2 point)
            {
                _shapeHandleIds[count] = id;
                _shapeHandlePoints[count] = point;
                count++;
            }

            switch (entity.Shape)
            {
                case PaintShapeKind.Circle:
                    Add(ShapeRadius, new Vector2(radius, 0));
                    break;

                case PaintShapeKind.Star:
                    Add(ShapeRadius, new Vector2((float)Math.Cos(StarHandleAngle), (float)Math.Sin(StarHandleAngle)) * radius);
                    break;

                case PaintShapeKind.Rectangle:
                case PaintShapeKind.Bubble:
                    for (int i = 0; i < 4; i++)
                    {
                        Add(ShapeCorner + i, new Vector2(CornerX[i] * size.X, CornerY[i] * size.Y));
                    }

                    Add(ShapeRotate, new Vector2(0, -size.Y - ShapeRotateGap * GetUnit(entity)));

                    if (entity.Shape == PaintShapeKind.Bubble)
                    {
                        Add(ShapeTail, entity.ShapeTail);
                    }
                    break;

                case PaintShapeKind.Arrow:
                    Add(ArrowStart, entity.ArrowStart);
                    Add(ArrowBend, entity.ArrowBend);
                    Add(ArrowTip, entity.ArrowTip);
                    Add(ArrowHead, entity.ArrowTip + GetArrowBack(entity) * entity.ArrowHead / 5.5f);
                    break;
            }

            return count;
        }

        // As large as a handle's reach on screen, over each of the selected shape's handles.
        private void UpdateHandleTargets()
        {
            var count = 0;
            var entity = _selected;

            if (entity?.Type == PaintEntityType.Shape && _gesture != Gesture.Pinch && TryGetBounds(entity, out Rect bounds))
            {
                var transform = GetTransform(entity, bounds);
                var size = ShapeHandleHitRadius * 2 / Math.Max(Zoom, 0.0001f);

                count = GetShapeHandles(entity);

                for (int i = 0; i < count; i++)
                {
                    if (i == _handleTargets.Count)
                    {
                        var created = new Ellipse
                        {
                            Fill = _hitBrush,
                            RenderTransform = new TranslateTransform()
                        };

                        _handleTargets.Add(created);
                        HandleTargets.Children.Add(created);
                    }

                    var target = _handleTargets[i];
                    var point = Vector2.Transform(_shapeHandlePoints[i], transform);

                    target.Width = size;
                    target.Height = size;
                    target.Visibility = Visibility.Visible;

                    var translate = (TranslateTransform)target.RenderTransform;
                    translate.X = point.X - size / 2;
                    translate.Y = point.Y - size / 2;
                }
            }

            for (int i = count; i < _handleTargets.Count; i++)
            {
                _handleTargets[i].Visibility = Visibility.Collapsed;
            }
        }

        // From an arrow's tip back along its curve.
        private static Vector2 GetArrowBack(PaintEntity entity)
        {
            var back = entity.ArrowBend - entity.ArrowTip;
            return back.LengthSquared() > 0 ? Vector2.Normalize(back) : new Vector2(0, 1);
        }

        private static bool TurnsShape(PaintEntity entity, int handle)
        {
            return handle == ShapeRotate || (handle == ShapeRadius && entity?.Shape == PaintShapeKind.Star);
        }

        private int? HitShapeHandle(Vector2 view)
        {
            var entity = _selected;
            if (entity == null || !TryGetBounds(entity, out Rect bounds))
            {
                return null;
            }

            var transform = GetTransform(entity, bounds) * _view;
            var count = GetShapeHandles(entity);

            int? hit = null;
            var best = ShapeHandleHitRadius;

            for (int i = 0; i < count; i++)
            {
                var distance = Vector2.Distance(view, Vector2.Transform(_shapeHandlePoints[i], transform));
                if (distance <= best)
                {
                    best = distance;
                    hit = _shapeHandleIds[i];
                }
            }

            return hit;
        }

        private void BeginShapeHandle(int handle)
        {
            _gesture = Gesture.ShapeHandle;
            _shapeHandle = handle;
            _pressedSelected = false;
            _pressedEmpty = false;

            _rawAngle = GetScreenAngle(_selected);
            _stickyAngle.Reset();

            for (int i = 0; i < StickyAngles.Length; i++)
            {
                if (Math.Abs(_rawAngle - StickyAngles[i]) < 0.0001)
                {
                    _stickyAngle.Stick(StickyAngles[i], i);
                    break;
                }
            }
        }

        private void MoveShapeHandle(Vector2 view)
        {
            var entity = _selected;
            if (!TryGetBounds(entity, out Rect bounds) || !Matrix3x2.Invert(GetTransform(entity, bounds), out Matrix3x2 inverse))
            {
                return;
            }

            var local = Vector2.Transform(FromView(view), inverse);
            var minimum = 8 * GetUnit(entity);

            switch (_shapeHandle)
            {
                case ShapeRadius:
                    entity.ShapeSize = new Vector2(Math.Max(minimum, local.Length()));

                    // A star's handle sits on one of its points, so the star turns to keep it there.
                    if (entity.Shape == PaintShapeKind.Star)
                    {
                        _rawAngle = GetHandleAngle(view, StarHandleAngle);
                        ApplyAngle();
                        return;
                    }
                    break;

                case ShapeRotate:
                    _rawAngle = GetHandleAngle(view, -Math.PI / 2);
                    ApplyAngle();
                    return;

                case ShapeTail:
                    entity.ShapeTail = local;
                    break;

                case ArrowStart:
                    entity.ArrowStart = local;
                    break;

                case ArrowBend:
                    entity.ArrowBend = local;
                    break;

                case ArrowTip:
                    entity.ArrowTip = local;
                    break;

                case ArrowHead:
                    var thickness = PaintEntityRenderer.GetShapeThickness(entity);
                    var along = Vector2.Dot(local - entity.ArrowTip, GetArrowBack(entity)) * 5.5f;
                    var longest = Math.Max(thickness * 4, Vector2.Distance(entity.ArrowTip, entity.ArrowBend) * 5.5f / 2);

                    entity.ArrowHead = Math.Clamp(along, thickness * 4, longest);
                    break;

                default:
                    // A corner moves while the opposite one stays, and never past it.
                    var corner = _shapeHandle - ShapeCorner;
                    var other = (corner + 2) % 4;

                    var size = Vector2.Abs(entity.ShapeSize);
                    var opposite = new Vector2(CornerX[other] * size.X, CornerY[other] * size.Y);
                    var span = local - opposite;

                    span = new Vector2(
                        CornerX[corner] * Math.Max(minimum * 2, CornerX[corner] * span.X),
                        CornerY[corner] * Math.Max(minimum * 2, CornerY[corner] * span.Y));

                    var middle = opposite + span / 2;

                    // The origin moves to the new middle; the tail is kept where it was.
                    entity.Position = Vector2.Transform(middle, GetTransform(entity, bounds)) / ImageSize;
                    entity.ShapeSize = Vector2.Abs(span) / 2;
                    entity.ShapeTail -= middle;
                    break;
            }

            UpdateSelected();
        }

        // The object's rotation, as a screen angle, that puts a handle sitting at this angle in
        // its own units under the pointer. Mirroring flips an object's turn but not its handle's
        // place in it, since its content is flipped back.
        private double GetHandleAngle(Vector2 view, double handle)
        {
            var offset = view - GetViewCenter(_selected);
            return Normalize(Math.Atan2(offset.Y, offset.X) - handle);
        }

        // In view pixels, so they keep one size however the shape and the view are scaled.
        private void DrawShapeHandles(CanvasDrawingSession session, PaintEntity entity, Rect bounds)
        {
            var progress = 1 - Math.Pow(1 - _selectProgress, 5);
            var alpha = (byte)(255 * progress);

            var accent = (HandleBrush as SolidColorBrush)?.Color ?? Colors.White;
            var fill = Color.FromArgb(alpha, accent.R, accent.G, accent.B);
            var white = Color.FromArgb(alpha, 255, 255, 255);

            var previous = session.Transform;
            session.Transform = Matrix3x2.CreateTranslation(OverlayMargin, OverlayMargin);

            var transform = GetTransform(entity, bounds) * _view;
            var count = GetShapeHandles(entity);

            if (entity.Shape == PaintShapeKind.Arrow)
            {
                _guideStyle ??= new CanvasStrokeStyle
                {
                    CustomDashStyle = new float[] { 8, 8 }
                };

                var start = Vector2.Transform(entity.ArrowStart, transform);
                var bend = Vector2.Transform(entity.ArrowBend, transform);
                var tip = Vector2.Transform(entity.ArrowTip, transform);
                var guide = Color.FromArgb((byte)(alpha * 0.8), 255, 255, 255);

                session.DrawLine(tip, bend, guide, 1, _guideStyle);
                session.DrawLine(start, bend, guide, 1, _guideStyle);
            }

            for (int i = 0; i < count; i++)
            {
                var point = Vector2.Transform(_shapeHandlePoints[i], transform);

                // The rotation handle hangs from the shape's top edge, and is drawn the other way
                // round from the rest.
                if (_shapeHandleIds[i] == ShapeRotate)
                {
                    var edge = Vector2.Transform(new Vector2(0, -Math.Abs(entity.ShapeSize.Y)), transform);

                    session.DrawLine(edge, point, white, 1);
                    session.FillCircle(point, ShapeHandleRadius, white);
                    session.DrawCircle(point, ShapeHandleRadius, fill, 1.5f);
                }
                else
                {
                    session.FillCircle(point, ShapeHandleRadius, fill);
                    session.DrawCircle(point, ShapeHandleRadius, white, 1.5f);
                }
            }

            session.Transform = previous;
        }

        #endregion

        // The midpoint moves the object, the spread scales it and the turn of the line between the
        // fingers turns it. A pinch never snaps.
        private void MovePinch(uint id, Vector2 previous, Vector2 view)
        {
            _pointers[id] = view;

            Vector2? other = null;

            foreach (var pointer in _pointers)
            {
                if (pointer.Key != id)
                {
                    other = pointer.Value;
                    break;
                }
            }

            if (other is not Vector2 anchor)
            {
                return;
            }

            var before = previous - anchor;
            var after = view - anchor;

            if (before.Length() < 1 || after.Length() < 1)
            {
                return;
            }

            var center = GetViewCenter(_selected) + (view - previous) / 2;

            _selected.Scale = ClampScale(_selected, _selected.Scale * after.Length() / before.Length());
            _selected.Rotation = (float)Normalize(_selected.Rotation + Sign * (Math.Atan2(after.Y, after.X) - Math.Atan2(before.Y, before.X)));

            SetViewCenter(_selected, center);
            UpdateSelected();
        }

        private void UpdateSelected()
        {
            if (_selected == _editing)
            {
                UpdateEditor();
            }

            UpdateElement(_selected);
            Canvas.Invalidate();
        }

        private void EndGesture()
        {
            _gesture = Gesture.None;
            _pressedEmpty = false;
            _pressedSelected = false;

            // Drawn sharp again at the size it came to rest at.
            if (_selected != null)
            {
                UpdateElement(_selected, true);
            }

            Canvas.Invalidate();
        }

        #endregion

        #region Rendering

        private void StartRendering()
        {
            if (!_rendering)
            {
                _rendering = true;
                _lastTick = _clock.Elapsed.TotalMilliseconds;

                CompositionTarget.Rendering += OnRendering;
            }
        }

        private void StopRendering()
        {
            if (_rendering)
            {
                _rendering = false;
                CompositionTarget.Rendering -= OnRendering;
            }
        }

        private void OnRendering(object sender, object e)
        {
            var now = _clock.Elapsed.TotalMilliseconds;
            var elapsed = now - _lastTick;

            _lastTick = now;

            var animating = false;

            if (_selectProgress < 1)
            {
                _selectProgress = Math.Min(1, _selectProgress + elapsed / SelectDuration);
                animating = true;
            }

            if (_flipProgress < 1)
            {
                _flipProgress = Math.Min(1, _flipProgress + elapsed / FlipDuration);
                animating = true;

                if (_flipping != null)
                {
                    UpdateElement(_flipping);
                }
            }

            // Run every frame, not only on moves, so a snap still lands while the pointer holds
            // still inside its zone.
            if (_selected != null && _panned)
            {
                if (_gesture == Gesture.Move)
                {
                    ApplyMove();
                }
                else if (_gesture == Gesture.Handle || (_gesture == Gesture.ShapeHandle && TurnsShape(_selected, _shapeHandle)))
                {
                    ApplyAngle();
                }
            }

            animating |= _stickyX.Advance(elapsed);
            animating |= _stickyY.Advance(elapsed);
            animating |= _stickyAngle.Advance(elapsed);

            Canvas.Invalidate();

            if (!animating && _gesture == Gesture.None)
            {
                StopRendering();
            }
        }

        private void Canvas_CreateResources(CanvasControl sender, CanvasCreateResourcesEventArgs args)
        {
            // Raised again after a device loss, when every layout and geometry is dead.
            foreach (var visual in _visuals.Values)
            {
                visual.Dispose();
            }

            _visuals.Clear();
            _elementsInvalid = true;
        }

        private void Canvas_Draw(CanvasControl sender, CanvasDrawEventArgs args)
        {
            if (_elementsInvalid)
            {
                _elementsInvalid = false;
                UpdateElements();
            }

            // Drawn whenever the selection or the view changes, which is when they move.
            UpdateHandleTargets();

            // Drawn in this layer's pixels, as everything else is, then taken to the view, and kept
            // to the photo like the objects themselves.
            var session = args.DrawingSession;
            session.Transform = _view * Matrix3x2.CreateTranslation(OverlayMargin, OverlayMargin);

            var size = ImageSize;

            using (var clip = CanvasGeometry.CreateRectangle(session, 0, 0, size.X, size.Y))
            using (session.CreateLayer(1, clip))
            {
                if (_gesture != Gesture.Pinch && _selected != null && TryGetBounds(_selected, out Rect bounds))
                {
                    DrawSelection(session, _selected, bounds);
                }

                if ((_gesture is Gesture.Move or Gesture.Handle) && _panned)
                {
                    DrawGuides(session);
                }
            }
        }

        private void Element_Draw(CanvasControl sender, CanvasDrawEventArgs args)
        {
            if (sender.Tag is not PaintEntity entity || !_rasters.TryGetValue(entity, out float raster) || !TryGetBounds(entity, out Rect bounds))
            {
                return;
            }

            var session = args.DrawingSession;
            session.TextAntialiasing = CanvasTextAntialiasing.Grayscale;
            session.Transform = Matrix3x2.CreateTranslation(-(float)bounds.X, -(float)bounds.Y) * Matrix3x2.CreateScale(raster);

            if (entity.Type == PaintEntityType.Shape)
            {
                PaintEntityRenderer.DrawShape(session, entity);
            }
            else if (entity.Type == PaintEntityType.Link)
            {
                _linkPhotos.TryGetValue(entity, out CanvasBitmap photo);

                session.Transform = Matrix3x2.CreateScale(PaintLinkVisual.Unit) * session.Transform;
                GetLinkVisual(entity).Draw(session, entity, photo);
            }
            else if (GetVisual(entity) is PaintTextVisual visual)
            {
                // The text box draws the glyphs of the text being written.
                PaintEntityRenderer.DrawContent(session, entity, visual, entity != _editing);
            }
        }

        // A dashed frame with a handle on each side, rounded around a text and round around a
        // sticker, one size on screen however the object and the view are scaled.
        private void DrawSelection(CanvasDrawingSession session, PaintEntity entity, Rect bounds)
        {
            if (entity.Type == PaintEntityType.Shape)
            {
                DrawShapeHandles(session, entity, bounds);
                return;
            }

            var unit = GetUnit(entity);
            var frame = GetFrame(entity, bounds);
            var center = new Vector2((float)(frame.X + frame.Width / 2), (float)(frame.Y + frame.Height / 2));

            var progress = 1 - Math.Pow(1 - _selectProgress, 5);
            var alpha = (byte)(255 * progress);
            var grow = (float)(0.9 + 0.1 * progress);

            _dashStyle ??= new CanvasStrokeStyle
            {
                CustomDashStyle = new float[] { 5, 5 },
                DashCap = CanvasCapStyle.Round,
                StartCap = CanvasCapStyle.Round,
                EndCap = CanvasCapStyle.Round
            };

            var previous = session.Transform;
            session.Transform = Matrix3x2.CreateScale(grow, center) * GetTransform(entity, bounds) * previous;

            var shadowColor = Color.FromArgb((byte)(0x50 * progress), 0, 0, 0);
            var frameColor = Color.FromArgb(alpha, 255, 255, 255);

            if (IsRound(entity))
            {
                var circle = (float)frame.Width / 2;

                session.DrawCircle(center + new Vector2(0.75f * unit), circle, shadowColor, 2 * unit, _dashStyle);
                session.DrawCircle(center, circle, frameColor, 2 * unit, _dashStyle);
            }
            else
            {
                var radius = 12 * unit;
                var shadow = new Rect(frame.X + 0.75 * unit, frame.Y + 0.75 * unit, frame.Width, frame.Height);

                session.DrawRoundedRectangle(shadow, radius, radius, shadowColor, 2 * unit, _dashStyle);
                session.DrawRoundedRectangle(frame, radius, radius, frameColor, 2 * unit, _dashStyle);
            }

            var y = center.Y;
            var accent = (HandleBrush as SolidColorBrush)?.Color ?? Colors.White;
            var fill = Color.FromArgb(alpha, accent.R, accent.G, accent.B);
            var ring = Color.FromArgb(alpha, 255, 255, 255);

            session.FillCircle((float)frame.Left, y, 5.66f * unit, fill);
            session.DrawCircle((float)frame.Left, y, 5.66f * unit, ring, 2.66f * unit);
            session.FillCircle((float)frame.Right, y, 5.66f * unit, fill);
            session.DrawCircle((float)frame.Right, y, 5.66f * unit, ring, 2.66f * unit);

            session.Transform = previous;
        }

        // The crop's centre lines, while an object is held on them.
        private void DrawGuides(CanvasDrawingSession session)
        {
            var width = 2 / Zoom;

            if (_stickyX.Index == 0 && _stickyX.Progress > 0)
            {
                var x = (float)(_area.X + _area.Width / 2);
                var color = Color.FromArgb((byte)(255 * _stickyX.Progress), 255, 255, 255);

                session.DrawLine(FromView(new Vector2(x, (float)_area.Top)), FromView(new Vector2(x, (float)_area.Bottom)), color, width);
            }

            if (_stickyY.Index == 0 && _stickyY.Progress > 0)
            {
                var y = (float)(_area.Y + _area.Height / 2);
                var color = Color.FromArgb((byte)(255 * _stickyY.Progress), 255, 255, 255);

                session.DrawLine(FromView(new Vector2((float)_area.Left, y)), FromView(new Vector2((float)_area.Right, y)), color, width);
            }
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            ObjectsClip.Rect = new Rect(0, 0, e.NewSize.Width, e.NewSize.Height);

            UpdateElements();

            if (_editing != null)
            {
                UpdateEditor();
            }

            Canvas.Invalidate();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            StopRendering();

            foreach (var visual in _visuals.Values)
            {
                visual.Dispose();
            }

            _visuals.Clear();

            _dashStyle?.Dispose();
            _dashStyle = null;

            _guideStyle?.Dispose();
            _guideStyle = null;
        }

        #endregion

        /// <summary>
        /// Holds a value on a target once it has stayed near it for a moment, and lets go as soon
        /// as it moves away. Both ways it slides rather than jumps.
        /// </summary>
        private sealed class Sticky
        {
            private const double Delay = 250;
            private const double Duration = 150;

            private double? _since;
            private int _candidateIndex;

            public double Target { get; private set; }

            public int Index { get; private set; } = -1;

            public bool IsStuck { get; private set; }

            public double Progress { get; private set; }

            public void Reset()
            {
                IsStuck = false;
                Progress = 0;
                Index = -1;
                _since = null;
            }

            public void Stick(double target, int index)
            {
                IsStuck = true;
                Progress = 1;
                Target = target;
                Index = index;
                _since = null;
            }

            public void Update(double value, double[] targets, int count, double trigger, double threshold, double now)
            {
                if (IsStuck)
                {
                    if (Math.Abs(value - Target) >= threshold)
                    {
                        IsStuck = false;
                        _since = null;
                    }

                    return;
                }

                var nearest = -1;
                var best = trigger;

                for (int i = 0; i < count; i++)
                {
                    var distance = Math.Abs(value - targets[i]);
                    if (distance < best)
                    {
                        best = distance;
                        nearest = i;
                    }
                }

                if (nearest < 0)
                {
                    _since = null;
                    return;
                }

                if (_since == null || _candidateIndex != nearest)
                {
                    _since = now;
                    _candidateIndex = nearest;
                }
                else if (now - _since.Value >= Delay)
                {
                    IsStuck = true;
                    Target = targets[nearest];
                    Index = nearest;
                    _since = null;
                }
            }

            public bool Advance(double elapsed)
            {
                var goal = IsStuck ? 1 : 0;
                if (Progress == goal)
                {
                    return false;
                }

                Progress = goal > Progress
                    ? Math.Min(1, Progress + elapsed / Duration)
                    : Math.Max(0, Progress - elapsed / Duration);

                return true;
            }

            public double Apply(double value)
            {
                if (Progress <= 0)
                {
                    return value;
                }

                var t = 1 - Math.Pow(1 - Progress, 3);
                return value + (Target - value) * t;
            }
        }
    }

    public sealed partial class PaintEntityEventArgs : EventArgs
    {
        public PaintEntityEventArgs(PaintEntity entity)
        {
            Entity = entity;
        }

        public PaintEntity Entity { get; }
    }

    /// <summary>
    /// A text's layout and frame, made once and kept until what it was made from changes.
    /// </summary>
    public sealed partial class PaintTextVisual : IDisposable
    {
        private readonly string _text;
        private readonly float _size;
        private readonly PaintTextAlignment _alignment;
        private readonly PaintTextFont _font;

        public PaintTextVisual(string text, float size, PaintTextAlignment alignment, PaintTextFont font, CanvasTextLayout layout, CanvasGeometry frame, Rect bounds)
        {
            _text = text;
            _size = size;
            _alignment = alignment;
            _font = font;

            Layout = layout;
            Frame = frame;
            Bounds = bounds;
        }

        public CanvasTextLayout Layout { get; }

        /// <summary>
        /// The background behind the lines, or null when there is no text to put one behind.
        /// </summary>
        public CanvasGeometry Frame { get; }

        /// <summary>
        /// What the object covers, in the renderer's units.
        /// </summary>
        public Rect Bounds { get; }

        public bool Matches(PaintEntity entity, string placeholder)
        {
            return _text == PaintEntityRenderer.GetDisplayText(entity, placeholder)
                && _size == entity.TextSize
                && _alignment == entity.Alignment
                && _font == entity.Font;
        }

        public void Dispose()
        {
            Layout.Dispose();
            Frame?.Dispose();
        }
    }

    /// <summary>
    /// Draws text objects, for the editor and for the sent photo alike.
    /// </summary>
    public static class PaintEntityRenderer
    {
        /// <summary>
        /// Text is laid out in a fixed space this many units across the upright image, so it
        /// wraps the same in the editor and in an export of any size.
        /// </summary>
        public const float Reference = 1000;

        public const float MaxWidth = Reference * (1 - 20f / 1280);

        private const float BaseFontSize = Reference / 9;

        public static float GetFontSize(PaintEntity entity)
        {
            return BaseFontSize * entity.TextSize;
        }

        /// <summary>
        /// The Windows fonts the typefaces are set in. None is bundled: the text is drawn into the
        /// sent photo, so whoever receives it never needs the font.
        /// </summary>
        public static void GetFont(PaintTextFont font, out string family, out FontWeight weight, out FontStyle style, out FontStretch stretch)
        {
            family = font switch
            {
                PaintTextFont.Serif => "Times New Roman",
                PaintTextFont.Condensed => "Bahnschrift",
                PaintTextFont.Mono => "Consolas",
                PaintTextFont.Display => "Georgia",
                _ => "Segoe UI"
            };

            weight = font is PaintTextFont.Regular or PaintTextFont.Italic
                ? FontWeights.SemiBold
                : FontWeights.Bold;

            style = font == PaintTextFont.Italic
                ? FontStyle.Italic
                : FontStyle.Normal;

            stretch = font == PaintTextFont.Condensed
                ? FontStretch.Condensed
                : FontStretch.Normal;
        }

        public static string GetDisplayText(PaintEntity entity, string placeholder)
        {
            return string.IsNullOrEmpty(entity.Text) ? placeholder ?? string.Empty : entity.Text;
        }

        public static PaintTextVisual CreateText(ICanvasResourceCreator creator, PaintEntity entity, string placeholder = null)
        {
            var text = GetDisplayText(entity, placeholder);
            var fontSize = GetFontSize(entity);

            GetFont(entity.Font, out string family, out FontWeight weight, out FontStyle style, out FontStretch stretch);

            CanvasTextLayout layout;

            using (var format = new CanvasTextFormat
            {
                FontFamily = family,
                FontWeight = weight,
                FontStyle = style,
                FontStretch = stretch,
                FontSize = fontSize,
                WordWrapping = CanvasWordWrapping.Wrap,
                Options = CanvasDrawTextOptions.EnableColorFont,
                HorizontalAlignment = entity.Alignment switch
                {
                    PaintTextAlignment.Center => CanvasHorizontalAlignment.Center,
                    PaintTextAlignment.Right => CanvasHorizontalAlignment.Right,
                    _ => CanvasHorizontalAlignment.Left
                }
            })
            {
                layout = new CanvasTextLayout(creator, text, format, MaxWidth, 0);
            }

            var lines = GetLines(layout, fontSize / 3);
            var frame = CreateFrame(creator, lines, fontSize / 3);

            double left = double.MaxValue;
            double top = double.MaxValue;
            double right = double.MinValue;
            double bottom = double.MinValue;

            foreach (var line in lines)
            {
                if (line is Rect rect)
                {
                    left = Math.Min(left, rect.Left);
                    top = Math.Min(top, rect.Top);
                    right = Math.Max(right, rect.Right);
                    bottom = Math.Max(bottom, rect.Bottom);
                }
            }

            Rect bounds;

            if (right > left)
            {
                bounds = new Rect(left, top, right - left, bottom - top);
            }
            else
            {
                var layoutBounds = layout.LayoutBounds;
                bounds = new Rect(layoutBounds.X, layoutBounds.Y, Math.Max(layoutBounds.Width, fontSize / 2), Math.Max(layoutBounds.Height, fontSize));
            }

            return new PaintTextVisual(text, entity.TextSize, entity.Alignment, entity.Font, layout, frame, bounds);
        }

        // Each line's ink widened by the padding, or null for a line with nothing visible on it.
        private static Rect?[] GetLines(CanvasTextLayout layout, float padding)
        {
            var metrics = layout.LineMetrics;
            var lines = new Rect?[metrics.Length];

            var start = 0;
            var top = 0f;

            for (int i = 0; i < metrics.Length; i++)
            {
                var line = metrics[i];
                var visible = line.CharacterCount - line.TrailingWhitespaceCount;

                if (visible > 0)
                {
                    var left = double.MaxValue;
                    var right = double.MinValue;

                    foreach (var region in layout.GetCharacterRegions(start, visible))
                    {
                        left = Math.Min(left, region.LayoutBounds.Left);
                        right = Math.Max(right, region.LayoutBounds.Right);
                    }

                    if (right > left)
                    {
                        lines[i] = new Rect(left - padding, top, right - left + padding * 2, line.Height);
                    }
                }

                start += line.CharacterCount;
                top += line.Height;
            }

            return lines;
        }

        // One rounded outline around each run of consecutive lines, its inner corners rounded too.
        // Edges closer than one and a half radii are evened out first, so ragged lines read as one
        // block.
        private static CanvasGeometry CreateFrame(ICanvasResourceCreator creator, Rect?[] lines, float radius)
        {
            CanvasPathBuilder builder = null;
            var block = new List<Rect>(lines.Length);

            for (int i = 0; i <= lines.Length; i++)
            {
                if (i < lines.Length && lines[i] is Rect line)
                {
                    block.Add(line);
                    continue;
                }

                if (block.Count > 0)
                {
                    builder ??= new CanvasPathBuilder(creator);

                    AlignEdges(block, radius * 1.5);
                    AddOutline(builder, block, radius);

                    block.Clear();
                }
            }

            return builder != null
                ? CanvasGeometry.CreatePath(builder)
                : null;
        }

        private static void AlignEdges(List<Rect> block, double tolerance)
        {
            bool changed;

            do
            {
                changed = false;

                for (int i = 1; i < block.Count; i++)
                {
                    var above = block[i - 1];
                    var below = block[i];

                    if (above.Left != below.Left && Math.Abs(above.Left - below.Left) < tolerance)
                    {
                        var left = Math.Min(above.Left, below.Left);

                        block[i - 1] = above = WithEdges(above, left, above.Right);
                        block[i] = below = WithEdges(below, left, below.Right);
                        changed = true;
                    }

                    if (above.Right != below.Right && Math.Abs(above.Right - below.Right) < tolerance)
                    {
                        var right = Math.Max(above.Right, below.Right);

                        block[i - 1] = WithEdges(above, above.Left, right);
                        block[i] = WithEdges(below, below.Left, right);
                        changed = true;
                    }
                }
            }
            while (changed);
        }

        private static Rect WithEdges(Rect rect, double left, double right)
        {
            return new Rect(left, rect.Y, right - left, rect.Height);
        }

        private static void AddOutline(CanvasPathBuilder builder, List<Rect> block, float radius)
        {
            var points = new List<Vector2>(block.Count * 4);
            var last = block.Count - 1;

            // Clockwise: across the top, down the right side, back along the bottom and up the left.
            points.Add(new Vector2((float)block[0].Left, (float)block[0].Top));
            points.Add(new Vector2((float)block[0].Right, (float)block[0].Top));

            for (int i = 0; i < last; i++)
            {
                points.Add(new Vector2((float)block[i].Right, (float)block[i].Bottom));
                points.Add(new Vector2((float)block[i + 1].Right, (float)block[i].Bottom));
            }

            points.Add(new Vector2((float)block[last].Right, (float)block[last].Bottom));
            points.Add(new Vector2((float)block[last].Left, (float)block[last].Bottom));

            for (int i = last; i > 0; i--)
            {
                points.Add(new Vector2((float)block[i].Left, (float)block[i].Top));
                points.Add(new Vector2((float)block[i - 1].Left, (float)block[i].Top));
            }

            RemoveStraightPoints(points);

            if (points.Count < 3)
            {
                return;
            }

            for (int i = 0; i < points.Count; i++)
            {
                var previous = points[(i + points.Count - 1) % points.Count];
                var point = points[i];
                var next = points[(i + 1) % points.Count];

                var incoming = point - previous;
                var outgoing = next - point;

                var round = Math.Min(radius, Math.Min(incoming.Length(), outgoing.Length()) / 2);

                incoming = Vector2.Normalize(incoming);
                outgoing = Vector2.Normalize(outgoing);

                var from = point - incoming * round;
                var to = point + outgoing * round;

                // Turning right is an outer corner, turning left an inner one.
                var direction = incoming.X * outgoing.Y - incoming.Y * outgoing.X > 0
                    ? CanvasSweepDirection.Clockwise
                    : CanvasSweepDirection.CounterClockwise;

                if (i == 0)
                {
                    builder.BeginFigure(from);
                }
                else
                {
                    builder.AddLine(from);
                }

                builder.AddArc(to, round, round, 0, direction, CanvasArcSize.Small);
            }

            builder.EndFigure(CanvasFigureLoop.Closed);
        }

        // Drops repeated points and points in the middle of a straight run, which have no corner
        // to round.
        private static void RemoveStraightPoints(List<Vector2> points)
        {
            bool removed;

            do
            {
                removed = false;

                for (int i = 0; i < points.Count && points.Count > 2; i++)
                {
                    var previous = points[(i + points.Count - 1) % points.Count];
                    var point = points[i];
                    var next = points[(i + 1) % points.Count];

                    var incoming = point - previous;
                    var outgoing = next - point;

                    if (incoming.LengthSquared() < 0.0001f || outgoing.LengthSquared() < 0.0001f || Math.Abs(incoming.X * outgoing.Y - incoming.Y * outgoing.X) < 0.0001f)
                    {
                        points.RemoveAt(i);
                        removed = true;
                        break;
                    }
                }
            }
            while (removed);
        }

        /// <summary>
        /// The text's colour and its frame's, for its frame style. Light colours get dark
        /// partners and dark ones light, by perceived brightness.
        /// </summary>
        public static void GetColors(PaintEntity entity, out Color foreground, out Color? background)
        {
            var color = entity.Color;
            var brightness = (color.R * 0.2126 + color.G * 0.7152 + color.B * 0.0722) / 255;

            switch (entity.Frame)
            {
                case PaintTextFrame.Filled:
                    foreground = brightness >= 0.721 ? Colors.Black : Colors.White;
                    background = color;
                    break;
                case PaintTextFrame.Translucent:
                    foreground = color;
                    background = brightness >= 0.25 ? Color.FromArgb(0x99, 0, 0, 0) : Color.FromArgb(0x99, 255, 255, 255);
                    break;
                case PaintTextFrame.Solid:
                    foreground = color;
                    background = brightness >= 0.25 ? Colors.Black : Colors.White;
                    break;
                default:
                    foreground = color;
                    background = null;
                    break;
            }
        }

        /// <summary>
        /// The renderer's units to the image's pixels. The text is flipped back when the image is
        /// mirrored, so it always reads the right way.
        /// </summary>
        public static Matrix3x2 GetTransform(PaintEntity entity, Rect bounds, Vector2 imageSize, bool mirrored)
        {
            // A shape turns about its own origin, so reshaping it never moves what is left alone.
            var center = entity.Type == PaintEntityType.Shape
                ? Vector2.Zero
                : new Vector2((float)(bounds.X + bounds.Width / 2), (float)(bounds.Y + bounds.Height / 2));

            var scale = entity.Scale * imageSize.X / Reference;

            // A reaction's flip turns its bubble, which the bubble draws itself.
            var mirror = entity.Mirror && entity.Type != PaintEntityType.Reaction;

            return Matrix3x2.CreateTranslation(-center)
                * Matrix3x2.CreateScale(mirrored != mirror ? -1 : 1, 1)
                * Matrix3x2.CreateScale(scale)
                * Matrix3x2.CreateRotation(entity.Rotation)
                * Matrix3x2.CreateTranslation(entity.Position * imageSize);
        }

        public static void Draw(CanvasDrawingSession session, PaintEntity entity, PaintTextVisual visual, Vector2 imageSize, bool mirrored, bool glyphs = true)
        {
            var previous = session.Transform;
            session.Transform = GetTransform(entity, visual.Bounds, imageSize, mirrored) * previous;

            DrawContent(session, entity, visual, glyphs);

            session.Transform = previous;
        }

        /// <summary>
        /// Draws a text in the renderer's units, through whatever transform the session has.
        /// </summary>
        public static void DrawContent(CanvasDrawingSession session, PaintEntity entity, PaintTextVisual visual, bool glyphs = true)
        {
            GetColors(entity, out Color foreground, out Color? background);

            if (background is Color fill && visual.Frame != null)
            {
                session.FillGeometry(visual.Frame, fill);
            }

            if (glyphs)
            {
                session.DrawTextLayout(visual.Layout, 0, 0, foreground);
            }
        }

        // Every colour white, every alpha kept.
        private static ColorMatrixEffect CreateWhite(ICanvasImage source)
        {
            return new ColorMatrixEffect
            {
                Source = source,
                ColorMatrix = new Matrix5x4
                {
                    M44 = 1,
                    M51 = 1,
                    M52 = 1,
                    M53 = 1
                }
            };
        }

        /// <summary>
        /// A sticker, a custom emoji or a GIF: drawn from a file.
        /// </summary>
        public static bool IsSticker(PaintEntity entity)
        {
            return entity.Type is PaintEntityType.Sticker or PaintEntityType.Animation;
        }

        private const float ShapeRounding = Reference * 0.04f;

        // A star's inner points, as a share of its outer ones.
        private const float StarInner = 0.4316f;

        /// <summary>
        /// Half the width of a shape's line, in the renderer's units: the drawing tools' width for
        /// the same weight, the line being drawn twice as wide, centred on the edge.
        /// </summary>
        public static float GetShapeThickness(PaintEntity entity)
        {
            return (8 + 90 * entity.ShapeWeight) / 2048 * Reference;
        }

        public static Rect GetShapeBounds(PaintEntity entity)
        {
            float left, top, right, bottom;
            var pad = GetShapeThickness(entity) + 1;

            if (entity.Shape == PaintShapeKind.Arrow)
            {
                left = Math.Min(entity.ArrowStart.X, Math.Min(entity.ArrowBend.X, entity.ArrowTip.X));
                top = Math.Min(entity.ArrowStart.Y, Math.Min(entity.ArrowBend.Y, entity.ArrowTip.Y));
                right = Math.Max(entity.ArrowStart.X, Math.Max(entity.ArrowBend.X, entity.ArrowTip.X));
                bottom = Math.Max(entity.ArrowStart.Y, Math.Max(entity.ArrowBend.Y, entity.ArrowTip.Y));

                pad += entity.ArrowHead / 2;
            }
            else
            {
                var size = Vector2.Abs(entity.ShapeSize);

                if (entity.Shape is PaintShapeKind.Circle or PaintShapeKind.Star)
                {
                    size = new Vector2(Math.Min(size.X, size.Y));
                }

                left = -size.X;
                top = -size.Y;
                right = size.X;
                bottom = size.Y;

                if (entity.Shape == PaintShapeKind.Bubble)
                {
                    left = Math.Min(left, entity.ShapeTail.X);
                    top = Math.Min(top, entity.ShapeTail.Y);
                    right = Math.Max(right, entity.ShapeTail.X);
                    bottom = Math.Max(bottom, entity.ShapeTail.Y);
                }
            }

            return new Rect(left - pad, top - pad, right - left + pad * 2, bottom - top + pad * 2);
        }

        /// <summary>
        /// A shape's outline, in its units around its origin, and an arrow's head beside it.
        /// </summary>
        public static CanvasGeometry CreateShape(ICanvasResourceCreator creator, PaintEntity entity, out CanvasGeometry head)
        {
            head = null;

            var size = Vector2.Abs(entity.ShapeSize);
            var radius = Math.Min(size.X, size.Y);

            switch (entity.Shape)
            {
                case PaintShapeKind.Circle:
                    return CanvasGeometry.CreateCircle(creator, Vector2.Zero, radius);

                case PaintShapeKind.Rectangle:
                    var rounding = Math.Min(ShapeRounding, radius);
                    return CanvasGeometry.CreateRoundedRectangle(creator, -size.X, -size.Y, size.X * 2, size.Y * 2, rounding, rounding);

                case PaintShapeKind.Star:
                    // Five points, the first straight up.
                    var points = new Vector2[10];

                    for (int i = 0; i < points.Length; i++)
                    {
                        var angle = -MathF.PI / 2 + i * MathF.PI / 5;
                        var distance = i % 2 == 0 ? radius : radius * StarInner;

                        points[i] = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * distance;
                    }

                    return CanvasGeometry.CreatePolygon(creator, points);

                case PaintShapeKind.Bubble:
                    // A tail as wide as half the shorter side, its base across the line to its tip.
                    var tail = entity.ShapeTail;
                    var across = MathF.Atan2(tail.X, -tail.Y);
                    var half = new Vector2(MathF.Cos(across), MathF.Sin(across)) * radius / 2;
                    var corner = Math.Min(ShapeRounding, radius);

                    using (var body = CanvasGeometry.CreateRoundedRectangle(creator, -size.X, -size.Y, size.X * 2, size.Y * 2, corner, corner))
                    using (var triangle = CanvasGeometry.CreatePolygon(creator, new[] { -half, half, tail }))
                    {
                        return body.CombineWith(triangle, Matrix3x2.Identity, CanvasGeometryCombine.Union);
                    }

                default:
                    var start = entity.ArrowStart;
                    var bend = entity.ArrowBend;
                    var tip = entity.ArrowTip;

                    // Its head is a triangle pointing along the curve's last direction.
                    var direction = MathF.Atan2(tip.Y - bend.Y, tip.X - bend.X);
                    var spread = MathF.PI / 6;
                    var length = MathF.Sin(spread) * entity.ArrowHead;
                    var apex = tip + new Vector2(MathF.Cos(direction), MathF.Sin(direction)) * length / 2;

                    head = CanvasGeometry.CreatePolygon(creator, new[]
                    {
                        apex,
                        apex + new Vector2(MathF.Cos(direction + MathF.PI - spread), MathF.Sin(direction + MathF.PI - spread)) * length,
                        apex + new Vector2(MathF.Cos(direction + MathF.PI + spread), MathF.Sin(direction + MathF.PI + spread)) * length
                    });

                    using (var builder = new CanvasPathBuilder(creator))
                    {
                        builder.BeginFigure(start);
                        builder.AddQuadraticBezier(bend, tip);
                        builder.EndFigure(CanvasFigureLoop.Open);

                        return CanvasGeometry.CreatePath(builder);
                    }
            }
        }

        /// <summary>
        /// Draws a shape in its units around its origin, through whatever transform the session
        /// has.
        /// </summary>
        public static void DrawShape(CanvasDrawingSession session, PaintEntity entity)
        {
            using var geometry = CreateShape(session, entity, out CanvasGeometry head);
            using var style = new CanvasStrokeStyle
            {
                StartCap = CanvasCapStyle.Round,
                EndCap = CanvasCapStyle.Round,
                LineJoin = CanvasLineJoin.Round
            };

            var width = GetShapeThickness(entity) * 2;
            var color = entity.Color;
            var opaque = Color.FromArgb(255, color.R, color.G, color.B);

            // One layer for a translucent colour, so the line does not darken where it overlaps
            // the fill or the head.
            using (color.A < 255 ? session.CreateLayer(color.A / 255f) : null)
            {
                if (entity.ShapeFilled && entity.Shape != PaintShapeKind.Arrow)
                {
                    session.FillGeometry(geometry, opaque);
                }

                session.DrawGeometry(geometry, opaque, width, style);

                if (head != null)
                {
                    session.FillGeometry(head, opaque);
                    session.DrawGeometry(head, opaque, width, style);
                    head.Dispose();
                }
            }
        }

        /// <summary>
        /// A sticker sits in a square half the image wide, as large as its shape allows.
        /// </summary>
        public static Rect GetStickerBounds(PaintEntity entity)
        {
            const float Side = Reference / 2;

            var width = Math.Max(1f, entity.StickerWidth);
            var height = Math.Max(1f, entity.StickerHeight);

            return width >= height
                ? new Rect(0, 0, Side, Side * height / width)
                : new Rect(0, 0, Side * width / height, Side);
        }

        /// <summary>
        /// A reaction's bubble, without its tail: 84 dp on a phone 411 dp wide, as large as its
        /// story area.
        /// </summary>
        public static Rect GetReactionBounds()
        {
            const float Side = Reference * 84 / 411;

            return new Rect(0, 0, Side, Side);
        }

        /// <summary>
        /// A widget at the size it was measured at, in the renderer's units.
        /// </summary>
        public static Rect GetWidgetBounds(PaintEntity entity)
        {
            var unit = entity.Type == PaintEntityType.Link
                ? PaintLinkVisual.Unit
                : Reference / 240;

            return new Rect(0, 0, entity.WidgetSize.X * unit, entity.WidgetSize.Y * unit);
        }

        public static Color GetWidgetBackground(PaintWidgetStyle style)
        {
            return style switch
            {
                PaintWidgetStyle.Translucent => Color.FromArgb(0x4C, 0, 0, 0),
                PaintWidgetStyle.White => Colors.White,
                _ => Colors.Black
            };
        }

        /// <summary>
        /// A story widget, which has no tools of its own.
        /// </summary>
        public static bool IsWidget(PaintEntity entity)
        {
            return entity.Type is PaintEntityType.Reaction or PaintEntityType.Weather or PaintEntityType.Link;
        }

        /// <summary>
        /// Left out of the posted image: viewers draw it from its story area.
        /// </summary>
        public static bool IsDrawnByViewers(PaintEntity entity)
        {
            return entity.Type is PaintEntityType.Reaction or PaintEntityType.Weather;
        }

        /// <summary>
        /// Brings every sticker's and GIF's file to disk, looking it up by its remote id when the
        /// edit was read back without it. False when one cannot be had.
        /// </summary>
        public static async Task<bool> LoadFilesAsync(IClientService clientService, IReadOnlyList<PaintEntity> entities)
        {
            if (entities == null)
            {
                return true;
            }

            foreach (var entity in entities)
            {
                // A link goes out without its preview's photo rather than not at all.
                if (entity.Type == PaintEntityType.Link)
                {
                    await LoadLinkPhotoFileAsync(clientService, entity);
                    continue;
                }
                else if (!IsSticker(entity))
                {
                    continue;
                }

                if (entity.StickerFile == null && !string.IsNullOrEmpty(entity.StickerRemoteId))
                {
                    var response = await clientService.SendAsync(new GetRemoteFile(entity.StickerRemoteId, null));
                    entity.StickerFile = response as File;
                }

                var file = entity.StickerFile;
                if (file == null)
                {
                    return false;
                }

                if (!file.Local.IsDownloadingCompleted)
                {
                    file = await clientService.DownloadFileAsync(file, 32);
                }

                if (file?.Local.IsDownloadingCompleted != true)
                {
                    return false;
                }

                entity.StickerFile = file;
            }

            return true;
        }

        /// <summary>
        /// Brings a link preview's photo to disk, looking it up by its remote id when the edit was
        /// read back without it. False when it cannot be had.
        /// </summary>
        public static async Task<bool> LoadLinkPhotoFileAsync(IClientService clientService, PaintEntity entity)
        {
            if (entity.LinkPhotoFile == null && !string.IsNullOrEmpty(entity.LinkPhotoRemoteId))
            {
                var response = await clientService.SendAsync(new GetRemoteFile(entity.LinkPhotoRemoteId, null));
                entity.LinkPhotoFile = response as File;
            }

            var file = entity.LinkPhotoFile;
            if (file == null)
            {
                return false;
            }

            if (!file.Local.IsDownloadingCompleted)
            {
                file = await clientService.DownloadFileAsync(file, 32);
            }

            if (file?.Local.IsDownloadingCompleted != true)
            {
                return false;
            }

            entity.LinkPhotoFile = file;
            return true;
        }

        /// <summary>
        /// Decodes each sticker's first frame at about the size it will be drawn, given how many
        /// output pixels a unit of the renderer's comes to at a scale of one, and each link
        /// preview's photo.
        /// </summary>
        public static async Task<Dictionary<PaintEntity, CanvasBitmap>> LoadStickersAsync(ICanvasResourceCreator creator, IReadOnlyList<PaintEntity> entities, float pixelsPerUnit)
        {
            Dictionary<PaintEntity, CanvasBitmap> bitmaps = null;

            foreach (var entity in entities)
            {
                if (entity.Type == PaintEntityType.Link)
                {
                    if (entity.LinkPhotoFile?.Local.IsDownloadingCompleted == true)
                    {
                        try
                        {
                            var photo = await CanvasBitmap.LoadAsync(creator, entity.LinkPhotoFile.Local.Path);

                            bitmaps ??= new Dictionary<PaintEntity, CanvasBitmap>();
                            bitmaps[entity] = photo;
                        }
                        catch
                        {
                            // Drawn without it.
                        }
                    }

                    continue;
                }

                var file = entity.StickerFile;
                if (!IsSticker(entity) || file?.Local.IsDownloadingCompleted != true)
                {
                    continue;
                }

                var bounds = GetStickerBounds(entity);
                var side = (int)Math.Clamp(Math.Max(bounds.Width, bounds.Height) * entity.Scale * pixelsPerUnit, 64, 1024);

                var path = file.Local.Path;
                var size = file.Size > 0 ? file.Size : file.Local.DownloadedSize;

                var bitmap = await Task.Run(() => DecodeSticker(creator, path, size, side));
                if (bitmap != null)
                {
                    bitmaps ??= new Dictionary<PaintEntity, CanvasBitmap>();
                    bitmaps[entity] = bitmap;
                }
            }

            return bitmaps;
        }

        // A sticker that cannot be read is left out of the photo rather than failing it.
        private static CanvasBitmap DecodeSticker(ICanvasResourceCreator creator, string path, long size, int side)
        {
            try
            {
                if (path.HasExtension(".tgs", ".json"))
                {
                    using var animation = LottieAnimation.LoadFromFile(path, side, side, false, null);
                    if (animation == null)
                    {
                        return null;
                    }

                    var buffer = BufferSurface.Create((uint)(side * side * 4));
                    animation.RenderSync(buffer, 0);

                    return CanvasBitmap.CreateFromBytes(creator, buffer, side, side, DirectXPixelFormat.B8G8R8A8UIntNormalized);
                }
                else if (path.HasExtension(".webp"))
                {
                    var buffer = Direct2DDevice.DrawWebP(path, side, out int width, out int height);
                    if (buffer == null || width <= 0 || height <= 0)
                    {
                        return null;
                    }

                    return CanvasBitmap.CreateFromBytes(creator, buffer, width, height, DirectXPixelFormat.B8G8R8A8UIntNormalized);
                }
                else
                {
                    using var animation = VideoAnimation.LoadFromFile(new LocalFileSource(path, size), false, false, false);
                    if (animation == null || animation.PixelWidth <= 0 || animation.PixelHeight <= 0)
                    {
                        return null;
                    }

                    var fit = Math.Min(1, (double)side / Math.Max(animation.PixelWidth, animation.PixelHeight));
                    var width = Math.Max(1, (int)(animation.PixelWidth * fit));
                    var height = Math.Max(1, (int)(animation.PixelHeight * fit));

                    var buffer = BufferSurface.Create((uint)(width * height * 4));
                    if (!animation.RenderSync(buffer, width, height, true, out _))
                    {
                        return null;
                    }

                    return CanvasBitmap.CreateFromBytes(creator, buffer, width, height, DirectXPixelFormat.B8G8R8A8UIntNormalized);
                }
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Draws <paramref name="entities"/> over an image of <paramref name="imageSize"/> upright
        /// pixels, through whatever transform <paramref name="session"/> already has.
        /// </summary>
        public static void Draw(CanvasDrawingSession session, IReadOnlyList<PaintEntity> entities, Vector2 imageSize, bool mirrored, IReadOnlyDictionary<PaintEntity, CanvasBitmap> stickers)
        {
            session.TextAntialiasing = CanvasTextAntialiasing.Grayscale;

            foreach (var entity in entities)
            {
                if (IsDrawnByViewers(entity))
                {
                    continue;
                }
                else if (entity.Type == PaintEntityType.Link)
                {
                    using var link = PaintLinkVisual.Create(session, entity);

                    var size = link.Size * PaintLinkVisual.Unit;
                    var previous = session.Transform;

                    CanvasBitmap photo = null;
                    stickers?.TryGetValue(entity, out photo);

                    session.Transform = Matrix3x2.CreateScale(PaintLinkVisual.Unit) * GetTransform(entity, new Rect(0, 0, size.X, size.Y), imageSize, mirrored) * previous;
                    link.Draw(session, entity, photo);
                    session.Transform = previous;

                    continue;
                }
                else if (entity.Type == PaintEntityType.Shape)
                {
                    var previous = session.Transform;

                    session.Transform = GetTransform(entity, GetShapeBounds(entity), imageSize, mirrored) * previous;
                    DrawShape(session, entity);
                    session.Transform = previous;

                    continue;
                }
                else if (IsSticker(entity))
                {
                    if (stickers != null && stickers.TryGetValue(entity, out CanvasBitmap bitmap))
                    {
                        var bounds = GetStickerBounds(entity);
                        var previous = session.Transform;

                        session.Transform = GetTransform(entity, bounds, imageSize, mirrored) * previous;

                        if (entity.StickerTinted)
                        {
                            using var white = CreateWhite(bitmap);
                            session.DrawImage(white, bounds, bitmap.Bounds);
                        }
                        else
                        {
                            session.DrawImage(bitmap, bounds, bitmap.Bounds);
                        }

                        session.Transform = previous;
                    }

                    continue;
                }

                if (string.IsNullOrEmpty(entity.Text))
                {
                    continue;
                }

                using var visual = CreateText(session, entity);
                Draw(session, entity, visual, imageSize, mirrored);
            }
        }
    }
}
