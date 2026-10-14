//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Geometry;
using Microsoft.Graphics.Canvas.Text;
using Microsoft.Graphics.Canvas.UI;
using Microsoft.Graphics.Canvas.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using Telegram.Common;
using Telegram.Controls.Media;
using Telegram.Entities;
using Windows.Devices.Input;
using Windows.Foundation;
using Windows.System;
using Windows.UI;
using Windows.UI.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;

namespace Telegram.Controls
{
    /// <summary>
    /// The media editor's text and stickers. It sits in the cropper beside the drawing canvas, so
    /// it is laid out in the upright image's pixels and turns with it; everything measured on
    /// screen goes through the view transform the cropper hands it.
    /// </summary>
    public sealed partial class PaintEntityLayer : Grid
    {
        private enum Gesture
        {
            None,
            Move,
            Handle,
            Pinch
        }

        // In view pixels.
        private const float HandleHitRadius = 19.5f;
        private const float PanThreshold = 6;
        private const float HandleThreshold = 2;
        private const float FramePaddingX = 10;
        private const float FramePaddingY = 6;
        private const double StickyPositionTrigger = 12;
        private const double StickyPadding = 8;

        private const double StickyAngleTrigger = 4 * Math.PI / 180;
        private const double StickyAngleThreshold = 12 * Math.PI / 180;

        // In milliseconds.
        private const double SelectDuration = 280;
        private const double MenuDelay = 500;

        private const float MinimumScale = 0.1f;
        private const float MaximumScale = 30;

        private static readonly double[] StickyAngles = { -Math.PI / 2, 0, Math.PI / 2, Math.PI, -Math.PI };

        private readonly List<PaintEntity> _entities = new();
        private readonly Dictionary<PaintEntity, PaintTextVisual> _visuals = new();
        private readonly Dictionary<PaintEntity, Border> _targets = new();
        private List<PaintEntity> _saved = new();

        private readonly Stopwatch _clock = Stopwatch.StartNew();
        private readonly SolidColorBrush _hitBrush = new(Colors.Transparent);

        private CanvasStrokeStyle _dashStyle;

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
        private float _handleScale;
        private float _handleDistance;
        private double _rawAngle;

        private readonly Sticky _stickyX = new();
        private readonly Sticky _stickyY = new();
        private readonly Sticky _stickyAngle = new();
        private readonly double[] _stickyTargets = new double[3];

        // Hit boxes are sized from a layout, which needs the canvas' device: any asked for before
        // it exists are redone on the next draw.
        private bool _targetsInvalid;

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

        public PaintEntity Selected => _selected;

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

            _entities.Clear();
            _visuals.Clear();
            _targets.Clear();
            Targets.Children.Clear();

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

            _view = transform;
            _area = area;
            _viewport = viewport;

            if (!Matrix3x2.Invert(transform, out _inverse))
            {
                _inverse = Matrix3x2.Identity;
            }

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

        private Matrix3x2 GetTransform(PaintEntity entity, PaintTextVisual visual)
        {
            return PaintEntityRenderer.GetTransform(entity, visual.Bounds, ImageSize, Mirrored);
        }

        // An object's own units per view pixel.
        private float GetUnit(PaintEntity entity)
        {
            var scale = entity.Scale * ImageSize.X / PaintEntityRenderer.Reference * Zoom;
            return scale > 0 ? 1 / scale : 1;
        }

        private Rect GetFrame(PaintEntity entity, PaintTextVisual visual)
        {
            var unit = GetUnit(entity);
            var bounds = visual.Bounds;

            return new Rect(
                bounds.X - FramePaddingX * unit,
                bounds.Y - FramePaddingY * unit,
                bounds.Width + FramePaddingX * unit * 2,
                bounds.Height + FramePaddingY * unit * 2);
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
            var visual = GetVisual(entity);
            if (visual == null || !Matrix3x2.Invert(GetTransform(entity, visual), out Matrix3x2 inverse))
            {
                return false;
            }

            var local = Vector2.Transform(point, inverse);
            var bounds = padded ? GetFrame(entity, visual) : visual.Bounds;

            return local.X >= bounds.Left && local.X <= bounds.Right && local.Y >= bounds.Top && local.Y <= bounds.Bottom;
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

        private Vector2 GetHandle(PaintEntity entity, PaintTextVisual visual, bool left)
        {
            var frame = GetFrame(entity, visual);
            var local = new Vector2((float)(left ? frame.Left : frame.Right), (float)(frame.Y + frame.Height / 2));

            return ToView(Vector2.Transform(local, GetTransform(entity, visual)));
        }

        // -1 for the left handle, 1 for the right, 0 for neither.
        private int HitHandle(Vector2 view)
        {
            var visual = _selected != null ? GetVisual(_selected) : null;
            if (visual == null)
            {
                return 0;
            }

            if (Vector2.Distance(view, GetHandle(_selected, visual, true)) <= HandleHitRadius)
            {
                return -1;
            }

            if (Vector2.Distance(view, GetHandle(_selected, visual, false)) <= HandleHitRadius)
            {
                return 1;
            }

            return 0;
        }

        // Half the size, in the view, of the box the turned object fills.
        private Vector2 GetHalfExtents(PaintEntity entity)
        {
            var visual = GetVisual(entity);
            if (visual == null)
            {
                return Vector2.Zero;
            }

            var transform = GetTransform(entity, visual) * _view;
            var bounds = visual.Bounds;

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

            var target = new Border
            {
                Background = _hitBrush,
                RenderTransform = new MatrixTransform()
            };

            _targets[entity] = target;
            Targets.Children.Add(target);

            UpdateTarget(entity);
            Canvas.Invalidate();
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

            if (_targets.TryGetValue(entity, out Border target))
            {
                Targets.Children.Remove(target);
                _targets.Remove(entity);
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

        // The box under an object is its unpadded bounds: only a selected object grows a margin
        // for grabbing, and while one is selected the whole layer takes presses anyway.
        private void UpdateTarget(PaintEntity entity)
        {
            if (!_targets.TryGetValue(entity, out Border target))
            {
                return;
            }

            var visual = GetVisual(entity);
            if (visual == null)
            {
                _targetsInvalid = true;
                return;
            }

            var scale = entity.Scale * ImageSize.X / PaintEntityRenderer.Reference;
            var width = (float)visual.Bounds.Width * scale;
            var height = (float)visual.Bounds.Height * scale;

            target.Width = width;
            target.Height = height;

            // A centred box is its own mirror image, so the content's flip can be left out.
            var matrix = Matrix3x2.CreateTranslation(-width / 2, -height / 2)
                * Matrix3x2.CreateRotation(entity.Rotation)
                * Matrix3x2.CreateTranslation(entity.Position * ImageSize);

            ((MatrixTransform)target.RenderTransform).Matrix = ToMatrix(matrix);
        }

        private void UpdateTargets()
        {
            foreach (var entity in _entities)
            {
                UpdateTarget(entity);
            }
        }

        private static Matrix ToMatrix(Matrix3x2 matrix)
        {
            return new Matrix(matrix.M11, matrix.M12, matrix.M21, matrix.M22, matrix.M31, matrix.M32);
        }

        /// <summary>
        /// Places an empty text where the view is looking and opens it for writing.
        /// </summary>
        public void AddText(Color color, float size, PaintTextFrame frame, PaintTextAlignment alignment)
        {
            EndEditing();

            var image = ImageSize;
            var zoom = Zoom;

            if (image.X <= 0 || zoom <= 0 || _area.Width <= 0)
            {
                return;
            }

            var visibleLeft = Math.Max(_area.Left, 0);
            var visibleTop = Math.Max(_area.Top, 0);
            var visibleRight = Math.Min(_area.Right, _viewport.Width);
            var visibleBottom = Math.Min(_area.Bottom, _viewport.Height);

            var visible = visibleRight > visibleLeft && visibleBottom > visibleTop
                ? new Rect(visibleLeft, visibleTop, visibleRight - visibleLeft, visibleBottom - visibleTop)
                : _area;

            // A new text never lands on top of another: it steps down and to the right, by a
            // fraction of the crop, until it is clear.
            var step = (float)(_area.Width * 200 / 1280);
            var clearance = _area.Width * 100 / 1280;
            var center = new Vector2((float)(visible.X + visible.Width / 2), (float)(visible.Y + visible.Height / 2));

            for (int i = 0; i < 8 && IsOccupied(center, clearance); i++)
            {
                center += new Vector2(step, step);
            }

            var entity = new PaintEntity
            {
                Type = PaintEntityType.Text,
                Text = string.Empty,
                Color = color,
                TextSize = size,
                Frame = frame,
                Alignment = alignment,
                Position = FromView(center) / image,
                Rotation = GetRotation(0),

                // As wide as the crop at full width, so the text wraps at its edges.
                Scale = (float)(_area.Width / (image.X * zoom))
            };

            AddEntity(entity);
            EntityCreated?.Invoke(this, new PaintEntityEventArgs(entity));

            BeginEditing(entity);
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

            var offset = (float)(_area.Width * 200 / 1280);
            var copy = source.Clone();

            AddEntity(copy);
            SetViewCenter(copy, GetViewCenter(source) + new Vector2(offset, offset));
            UpdateTarget(copy);

            EntityCreated?.Invoke(this, new PaintEntityEventArgs(copy));
            Select(copy);
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

        private void Invalidate(PaintEntity entity)
        {
            if (entity == _editing)
            {
                UpdateEditor();
            }

            UpdateTarget(entity);
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

            // While something is selected the whole layer takes presses: a tap elsewhere puts the
            // selection away rather than drawing.
            Background = _hitBrush;

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

            flyout.CreateFlyoutItem(DuplicateSelected, Strings.PaintDuplicate, Icons.DocumentCopy, VirtualKey.D);
            flyout.CreateFlyoutItem(DeleteSelected, Strings.PaintDelete, Icons.Delete, VirtualKey.Delete, VirtualKeyModifiers.None, destructive: true);

            flyout.ShowAt(this, position);
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

            Canvas.Invalidate();
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
                UpdateTarget(entity);
                Canvas.Invalidate();
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

            var matrix = Matrix3x2.CreateScale(1 / k) * Matrix3x2.CreateTranslation(origin) * GetTransform(entity, visual);
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
            if (handle != 0)
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
                var threshold = _gesture == Gesture.Handle ? HandleThreshold : PanThreshold;
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
                    // A second tap writes in a text; anything else gets its menu. A long press is
                    // left to RightTapped, or the menu would open twice.
                    if (_selected.Type == PaintEntityType.Text)
                    {
                        BeginEditing(_selected);
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

            _selected.Scale = Math.Clamp(_handleScale * distance / _handleDistance, MinimumScale, MaximumScale);
            _rawAngle = Normalize(Math.Atan2(offset.Y, offset.X) - (_leftHandle ? Math.PI : 0));

            ApplyAngle();
        }

        private void ApplyAngle()
        {
            _stickyAngle.Update(_rawAngle, StickyAngles, StickyAngles.Length, StickyAngleTrigger, StickyAngleThreshold, _clock.Elapsed.TotalMilliseconds);
            _selected.Rotation = GetRotation(_stickyAngle.Apply(_rawAngle));

            UpdateSelected();
        }

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

            _selected.Scale = Math.Clamp(_selected.Scale * after.Length() / before.Length(), MinimumScale, MaximumScale);
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

            UpdateTarget(_selected);
            Canvas.Invalidate();
        }

        private void EndGesture()
        {
            _gesture = Gesture.None;
            _pressedEmpty = false;
            _pressedSelected = false;

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

            // Run every frame, not only on moves, so a snap still lands while the pointer holds
            // still inside its zone.
            if (_selected != null && _panned)
            {
                if (_gesture == Gesture.Move)
                {
                    ApplyMove();
                }
                else if (_gesture == Gesture.Handle)
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
            UpdateTargets();
        }

        private void Canvas_Draw(CanvasControl sender, CanvasDrawEventArgs args)
        {
            if (_targetsInvalid)
            {
                _targetsInvalid = false;
                UpdateTargets();
            }

            var session = args.DrawingSession;
            session.TextAntialiasing = CanvasTextAntialiasing.Grayscale;

            var size = sender.Size.ToVector2();
            var mirrored = Mirrored;

            foreach (var entity in _entities)
            {
                if (GetVisual(entity) is PaintTextVisual visual)
                {
                    // The text box draws the glyphs of the text being written.
                    PaintEntityRenderer.Draw(session, entity, visual, size, mirrored, entity != _editing);
                }
            }

            if (_gesture != Gesture.Pinch && _selected != null && GetVisual(_selected) is PaintTextVisual selected)
            {
                DrawSelection(session, _selected, selected);
            }

            if ((_gesture is Gesture.Move or Gesture.Handle) && _panned)
            {
                DrawGuides(session);
            }
        }

        // A dashed rounded frame with a handle on each side, one size on screen however the
        // object and the view are scaled.
        private void DrawSelection(CanvasDrawingSession session, PaintEntity entity, PaintTextVisual visual)
        {
            var unit = GetUnit(entity);
            var frame = GetFrame(entity, visual);
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
            session.Transform = Matrix3x2.CreateScale(grow, center) * GetTransform(entity, visual) * previous;

            var radius = 12 * unit;
            var shadow = new Rect(frame.X + 0.75 * unit, frame.Y + 0.75 * unit, frame.Width, frame.Height);

            session.DrawRoundedRectangle(shadow, radius, radius, Color.FromArgb((byte)(0x50 * progress), 0, 0, 0), 2 * unit, _dashStyle);
            session.DrawRoundedRectangle(frame, radius, radius, Color.FromArgb(alpha, 255, 255, 255), 2 * unit, _dashStyle);

            var y = center.Y;
            var fill = Color.FromArgb(alpha, 0x1A, 0x9C, 0xFF);
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
            UpdateTargets();

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

        public PaintTextVisual(string text, float size, PaintTextAlignment alignment, CanvasTextLayout layout, CanvasGeometry frame, Rect bounds)
        {
            _text = text;
            _size = size;
            _alignment = alignment;

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
                && _alignment == entity.Alignment;
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

        public static string GetDisplayText(PaintEntity entity, string placeholder)
        {
            return string.IsNullOrEmpty(entity.Text) ? placeholder ?? string.Empty : entity.Text;
        }

        public static PaintTextVisual CreateText(ICanvasResourceCreator creator, PaintEntity entity, string placeholder = null)
        {
            var text = GetDisplayText(entity, placeholder);
            var fontSize = GetFontSize(entity);

            CanvasTextLayout layout;

            using (var format = new CanvasTextFormat
            {
                FontFamily = "Segoe UI",
                FontWeight = FontWeights.SemiBold,
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

            return new PaintTextVisual(text, entity.TextSize, entity.Alignment, layout, frame, bounds);
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
            var center = new Vector2((float)(bounds.X + bounds.Width / 2), (float)(bounds.Y + bounds.Height / 2));
            var scale = entity.Scale * imageSize.X / Reference;

            return Matrix3x2.CreateTranslation(-center)
                * Matrix3x2.CreateScale(mirrored ? -1 : 1, 1)
                * Matrix3x2.CreateScale(scale)
                * Matrix3x2.CreateRotation(entity.Rotation)
                * Matrix3x2.CreateTranslation(entity.Position * imageSize);
        }

        public static void Draw(CanvasDrawingSession session, PaintEntity entity, PaintTextVisual visual, Vector2 imageSize, bool mirrored, bool glyphs = true)
        {
            GetColors(entity, out Color foreground, out Color? background);

            var previous = session.Transform;
            session.Transform = GetTransform(entity, visual.Bounds, imageSize, mirrored) * previous;

            if (background is Color fill && visual.Frame != null)
            {
                session.FillGeometry(visual.Frame, fill);
            }

            if (glyphs)
            {
                session.DrawTextLayout(visual.Layout, 0, 0, foreground);
            }

            session.Transform = previous;
        }

        /// <summary>
        /// Draws <paramref name="entities"/> over an image of <paramref name="imageSize"/> upright
        /// pixels, through whatever transform <paramref name="session"/> already has.
        /// </summary>
        public static void Draw(CanvasDrawingSession session, IReadOnlyList<PaintEntity> entities, Vector2 imageSize, bool mirrored)
        {
            session.TextAntialiasing = CanvasTextAntialiasing.Grayscale;

            foreach (var entity in entities)
            {
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
