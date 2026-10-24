//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Microsoft.Graphics.Canvas;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Entities;
using Telegram.Native;
using Telegram.Native.Media;
using Telegram.Services;
using Telegram.Streams;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;

namespace Telegram.Controls
{
    /// <summary>
    /// What a media editor works on: the cropper holding the photo or the video's frames, the
    /// strokes drawn over it, and the objects placed on it. The toolbars around it are the host's.
    /// </summary>
    public sealed partial class MediaEditorSurface : Grid
    {
        private StorageMedia _media;
        private AsyncMediaPlayer _player;

        private bool _shown;

        // Strokes, placed objects and the crop undo as one history, newest last. An entry is null
        // for a stroke, which the canvas keeps the list of; the object its undo takes away; or the
        // crop its undo goes back to.
        private readonly List<object> _undo = new();
        private readonly List<object> _redo = new();

        // The crop the history last saw, or null until StartHistory.
        private ImageCropperState _cropState;
        private int _cropStepTime;

        // A wheel turned notch by notch is one step.
        private const int CropStepMerge = 600;

        // The video was playing when the eyedropper paused it.
        private bool _resumeAfterPick;

        public MediaEditorSurface()
        {
            InitializeComponent();

            // Seen even when the drawing or an object took the press.
            AddHandler(PointerPressedEvent, new PointerEventHandler(OnPointerPressed), true);
        }

        // Nothing the press lands on can hold focus, so it stays wherever it was, which after a
        // popup closes is outside the editor, and its keys never reach the editor. A text being
        // written keeps it.
        private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (!Entities.IsEditing)
            {
                Cropper.Focus(FocusState.Pointer);
            }
        }

        public AsyncMediaPlayer Player => _player;

        public bool IsVideo { get; private set; }

        public bool IsFrame => FrameLayer != null;

        // The blur tool paints a blurred copy of the photo, which only lines up with strokes laid
        // out over the photo itself.
        public bool CanBlur => !IsVideo && !IsFrame;

        /// <summary>
        /// View pixels per pixel of the layers that strokes and objects are laid out in.
        /// </summary>
        public double LayerZoom => IsFrame
            ? Cropper.ViewArea.Width / FrameLayer.Width
            : Cropper.ZoomFactor;

        public Color FrameTopColor => FrameFillTop?.Color ?? default;

        public Color FrameBottomColor => FrameFillBottom?.Color ?? default;

        public StorageMedia Media => _media;

        public void Initialize(IClientService clientService, StorageMedia media)
        {
            _media = media;

            IsVideo = media.File.HasExtension(Constants.VideoTypes);

            Canvas.Strokes = media.EditState.Strokes;
            Entities.ClientService = clientService;
            Entities.Items = media.EditState.Entities;

            Canvas.StrokeAdded += Canvas_StrokeAdded;
            Canvas.StrokesChanged += Canvas_StrokesChanged;
            Entities.EntityCreated += Entities_EntityCreated;
            Entities.EntityDeleted += Entities_EntityDeleted;
            Cropper.Changed += Cropper_Changed;
            Canvas.ColorPicked += Canvas_ColorPicked;
        }

        /// <summary>
        /// Puts the media in a frame of <paramref name="size"/>, filled behind it with a gradient
        /// taken from the media. Strokes and objects are then laid out in the frame, so moving
        /// the media under it leaves them where they are. Call after Initialize, before the
        /// cropper's source is set.
        /// </summary>
        public void InitializeFrame(Size size)
        {
            Cropper.IsFrame = true;

            FindName(nameof(FrameBackdrop));
            FindName(nameof(FrameOverlay));

            FrameFill.Width = FrameLayer.Width = size.Width;
            FrameFill.Height = FrameLayer.Height = size.Height;

            MediaContent.Children.Remove(Layers);
            FrameLayer.Children.Add(Layers);

            Cropper.SourceDecoded += Cropper_SourceDecoded;
        }

        // Starts nothing: the host subscribes to the player before Play.
        public void InitializePlayer(bool muted)
        {
            // The read callback opens the path itself, so a file that does not have one -- a
            // virtual or shared item -- cannot be played. The cropper still shows its frame.
            if (string.IsNullOrEmpty(_media.File.Path))
            {
                return;
            }

            FindName(nameof(Video));

            var options = new AsyncMediaPlayerOptions
            {
                CreateSwapChain = true,
                Mute = muted,
            };

            try
            {
                _player = new AsyncMediaPlayer(options);
            }
            catch (Exception ex)
            {
                // Out of memory, most likely. Everything that touches the player tolerates a
                // null, so the editor degrades to the still frame the cropper already shows.
                Logger.Error(ex);
                return;
            }

            GenerationSources.AddOrReplace("EditMediaPopup", _media.File);
        }

        public void Play()
        {
            if (_player == null)
            {
                return;
            }

            _player.Context.Attach(Video, true);
            _player.Play(new LocalFileSource(_media.File.Path, (long)_media.Size));
        }

        public void Close()
        {
            Canvas.StrokeAdded -= Canvas_StrokeAdded;
            Canvas.StrokesChanged -= Canvas_StrokesChanged;
            Entities.EntityCreated -= Entities_EntityCreated;
            Entities.EntityDeleted -= Entities_EntityDeleted;
            Cropper.Changed -= Cropper_Changed;
            Canvas.ColorPicked -= Canvas_ColorPicked;

            if (_player != null)
            {
                _player.Close();
                _player = null;
            }
        }

        // Escape puts away whatever is in hand, the eyedropper or a text, before it leaves the
        // editor.
        public bool HandleBack()
        {
            if (Canvas.IsPicking)
            {
                Canvas.CancelPick();
                Entities.IsHitTestVisible = true;

                ResumeAfterPick();
            }
            else if (Entities.IsEditing)
            {
                Entities.EndEditing();
            }
            else if (Entities.Selected != null)
            {
                Entities.Deselect();
            }
            else
            {
                return false;
            }

            return true;
        }

        // The blur tool paints a blurred copy of the photo, so the canvas needs the photo too.
        public async void LoadBlurSource()
        {
            if (Canvas.BlurSource != null || !CanBlur)
            {
                return;
            }

            try
            {
                using var stream = await ImageHelper.OpenReadAsync(_media.File);

                var decoder = await BitmapDecoder.CreateAsync(stream);
                var transform = ImageHelper.ComputeScalingTransformForSourceImage(decoder);

                var bitmap = await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied, transform, ExifOrientationMode.RespectExifOrientation, ColorManagementMode.DoNotColorManage);

                // Or Win2D would size it by the file's DPI.
                bitmap.DpiX = 96;
                bitmap.DpiY = 96;

                Canvas.BlurSource = bitmap;
                Canvas.Invalidate();
            }
            catch
            {
                // Without it blur strokes are left out of the preview, not of the sent photo.
            }
        }

        /// <summary>
        /// Starts the eyedropper over what is on screen: a video is paused, so the frame it picks
        /// from stays the one shown, and plays on once a colour is picked or the pick is put away.
        /// </summary>
        public async void BeginPick()
        {
            if (_player?.IsPlaying == true)
            {
                _player.Pause(true);
                _resumeAfterPick = true;
            }

            SoftwareBitmap underlay = null;

            try
            {
                underlay = await RenderUnderlayAsync();
            }
            catch
            {
                // Picked from the strokes alone.
            }

            Canvas.BeginPick(underlay);
        }

        // What lies under the canvas, laid out at its size, or null where the blur tool's photo
        // already is that: the frame as posted, or a video's frame where the player is.
        private async Task<SoftwareBitmap> RenderUnderlayAsync()
        {
            var time = TimeSpan.FromSeconds(_player?.Position ?? 0);

            if (IsFrame)
            {
                var generation = new ImageGeneration
                {
                    Rectangle = Cropper.CropRectangle,
                    Rotation = Cropper.Orientation,
                    Flip = Cropper.Flip,
                    Angle = Cropper.Angle,
                    FrameTopColor = FrameTopColor,
                    FrameBottomColor = FrameBottomColor
                };

                return await ImageHelper.RenderFrameAsync(_media.File, generation, new Size(FrameLayer.ActualWidth, FrameLayer.ActualHeight), time);
            }
            else if (!IsVideo)
            {
                return null;
            }

            // Upright, as the canvas lies over the whole media.
            using var stream = await ImageHelper.OpenReadAsync(_media.File, time, true);

            var decoder = await BitmapDecoder.CreateAsync(stream);
            var transform = ImageHelper.ComputeScalingTransformForSourceImage(decoder);

            var bitmap = await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied, transform, ExifOrientationMode.RespectExifOrientation, ColorManagementMode.DoNotColorManage);
            bitmap.DpiX = 96;
            bitmap.DpiY = 96;

            return bitmap;
        }

        private void Canvas_ColorPicked(object sender, PencilColorPickedEventArgs e)
        {
            ResumeAfterPick();
        }

        private void ResumeAfterPick()
        {
            if (_resumeAfterPick)
            {
                _resumeAfterPick = false;
                _player?.Pause(false);
            }
        }

        /// <summary>
        /// Where the frame is drawn, in <paramref name="relativeTo"/>'s coordinates.
        /// </summary>
        public Rect GetFrameBounds(UIElement relativeTo)
        {
            return FrameLayer.TransformToVisual(relativeTo).TransformBounds(new Rect(0, 0, FrameLayer.ActualWidth, FrameLayer.ActualHeight));
        }

        private void Cropper_ViewChanged(object sender, EventArgs e)
        {
            // The first view is the first layout of the media; see the XAML.
            if (!_shown)
            {
                _shown = true;

                Opacity = 1;
                IsHitTestVisible = true;
            }

            var transform = Cropper.ContentTransform;

            if (IsFrame)
            {
                // The frame's pixels to the view: the frame is the crop, wherever the view shows it.
                var area = Cropper.ViewArea;
                var scale = (float)(area.Width / FrameLayer.Width);

                transform = Matrix3x2.CreateScale(scale) * Matrix3x2.CreateTranslation((float)area.X, (float)area.Y);

                var matrix = new Matrix(transform.M11, transform.M12, transform.M21, transform.M22, transform.M31, transform.M32);
                FrameLayerTransform.Matrix = matrix;
                FrameFillTransform.Matrix = matrix;
            }

            Entities.SetView(transform, Cropper.ViewArea, Cropper.ViewportSize);

            // Recognition measures the hand's movement on screen.
            Canvas.LoupeScale = (float)(1 / LayerZoom);
        }

        // Sampled near the top and the bottom of the media, upright, and toned towards the middle
        // so neither end of the gradient is glaring or muddy. From the cropper's own decode, which
        // for a video is its first frame: decoding the media a second time for two pixels cost as
        // much as showing it.
        private void Cropper_SourceDecoded(object sender, SoftwareBitmap software)
        {
            Cropper.SourceDecoded -= Cropper_SourceDecoded;

            try
            {
                using var bitmap = CanvasBitmap.CreateFromSoftwareBitmap(ElementComposition.GetSharedDevice(), software);

                var width = software.PixelWidth;
                var height = software.PixelHeight;

                FrameFillTop.Color = AdaptFrameColor(bitmap.GetPixelColors(width / 2, (int)(height * 0.1), 1, 1)[0]);
                FrameFillBottom.Color = AdaptFrameColor(bitmap.GetPixelColors(width / 2, (int)(height * 0.9), 1, 1)[0]);
            }
            catch
            {
                // The frame stays black.
            }
        }

        private static Color AdaptFrameColor(Color color)
        {
            var hsv = color.ToHSV();

            hsv.V = Math.Clamp(hsv.V - 0.05, 0.15, 0.85);

            if (hsv.S > 0.1 && hsv.S <= 0.95)
            {
                if (hsv.S <= 0.5)
                {
                    hsv.S = Math.Min(hsv.S + 0.2, 1);
                }
                else if (hsv.S > 0.8)
                {
                    hsv.S = Math.Max(hsv.S - 0.4, 0);
                }
            }

            return hsv.ToRGB();
        }

        // Only a press beside a selected shape reaches the canvas: drawing puts the shape away.
        private void Canvas_StrokeStarted(object sender, EventArgs e)
        {
            Entities.Deselect();
        }

        private void Canvas_ShapeRecognized(object sender, PencilShapeRecognizedEventArgs e)
        {
            Entities.AddShape(e.Shape, e.Color, e.Weight);
        }

        private void Canvas_Gesture(object sender, PencilGestureEventArgs e)
        {
            Cropper.ZoomView(e.Center, e.Scale);
            Cropper.PanView(e.X, e.Y);
        }

        #region History

        public event EventHandler HistoryChanged;

        public bool CanUndo => _undo.Count > 0;

        public bool CanRedo => _redo.Count > 0;

        /// <summary>
        /// Starts the history from the edit as it opened, once the media is in. The strokes already
        /// there stay undoable; the objects already there and the crop do not.
        /// </summary>
        public void StartHistory()
        {
            _undo.Clear();
            _redo.Clear();

            Canvas.ClearRedo();

            for (int i = 0; i < Canvas.StrokeCount; i++)
            {
                _undo.Add(null);
            }

            _cropState = Cropper.GetState();
            HistoryChanged?.Invoke(this, EventArgs.Empty);
        }

        public void Undo()
        {
            if (_undo.Count == 0)
            {
                return;
            }

            var entry = _undo[_undo.Count - 1];
            _undo.RemoveAt(_undo.Count - 1);
            _redo.Add(Step(entry, Canvas.Undo, Entities.Remove));

            HistoryChanged?.Invoke(this, EventArgs.Empty);
        }

        public void Redo()
        {
            if (_redo.Count == 0)
            {
                return;
            }

            var entry = _redo[_redo.Count - 1];
            _redo.RemoveAt(_redo.Count - 1);
            _undo.Add(Step(entry, Canvas.Redo, Entities.Restore));

            HistoryChanged?.Invoke(this, EventArgs.Empty);
        }

        // Applies one entry and returns the one that takes it back: the same stroke or object, or
        // the crop it replaced.
        private object Step(object entry, Action stroke, Action<PaintEntity> entity)
        {
            if (entry is ImageCropperState crop)
            {
                var current = Cropper.GetState();

                // Set first, so that the change it raises is not taken for a new step.
                _cropState = crop;
                Cropper.SetState(crop);

                return current;
            }
            else if (entry is PaintEntity paint)
            {
                entity(paint);
            }
            else
            {
                stroke();
            }

            return entry;
        }

        private void Canvas_StrokeAdded(object sender, EventArgs e)
        {
            _undo.Add(null);
            _redo.Clear();

            HistoryChanged?.Invoke(this, EventArgs.Empty);
        }

        private void Canvas_StrokesChanged(object sender, EventArgs e)
        {
            HistoryChanged?.Invoke(this, EventArgs.Empty);
        }

        private void Entities_EntityCreated(object sender, PaintEntityEventArgs e)
        {
            _undo.Add(e.Entity);
            _redo.Clear();

            Canvas.ClearRedo();
            HistoryChanged?.Invoke(this, EventArgs.Empty);
        }

        // A deleted object is gone from the history too.
        private void Entities_EntityDeleted(object sender, PaintEntityEventArgs e)
        {
            _undo.Remove(e.Entity);
            _redo.Remove(e.Entity);

            HistoryChanged?.Invoke(this, EventArgs.Empty);
        }

        // Raised once a gesture ends, and also by a view pan in draw mode, which leaves the crop as
        // it was and so adds nothing.
        private void Cropper_Changed(object sender, EventArgs e)
        {
            if (_cropState == null)
            {
                return;
            }

            var state = Cropper.GetState();
            if (state.Matches(_cropState))
            {
                return;
            }

            var now = Environment.TickCount;

            if (_undo.Count == 0 || _undo[_undo.Count - 1] is not ImageCropperState || now - _cropStepTime > CropStepMerge)
            {
                _undo.Add(_cropState);
            }

            _cropState = state;
            _cropStepTime = now;

            _redo.Clear();
            Canvas.ClearRedo();

            HistoryChanged?.Invoke(this, EventArgs.Empty);
        }

        #endregion
    }
}
