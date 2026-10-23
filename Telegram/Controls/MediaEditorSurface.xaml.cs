//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Microsoft.Graphics.Canvas;
using System;
using System.Numerics;
using Telegram.Common;
using Telegram.Entities;
using Telegram.Native;
using Telegram.Native.Media;
using Telegram.Services;
using Telegram.Streams;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.UI;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
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

        public MediaEditorSurface()
        {
            InitializeComponent();
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

        public void Initialize(IClientService clientService, StorageMedia media)
        {
            _media = media;

            IsVideo = media.File.HasExtension(Constants.VideoTypes);

            Canvas.Strokes = media.EditState.Strokes;
            Entities.ClientService = clientService;
            Entities.Items = media.EditState.Entities;
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
    }
}
