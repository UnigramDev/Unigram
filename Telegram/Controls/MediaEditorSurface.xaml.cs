//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using Telegram.Common;
using Telegram.Entities;
using Telegram.Native.Media;
using Telegram.Services;
using Telegram.Streams;
using Windows.Graphics.Imaging;
using Windows.UI.Xaml.Controls;

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

        public MediaEditorSurface()
        {
            InitializeComponent();
        }

        public AsyncMediaPlayer Player => _player;

        public bool IsVideo { get; private set; }

        public void Initialize(IClientService clientService, StorageMedia media)
        {
            _media = media;

            IsVideo = media.File.HasExtension(Constants.VideoTypes);

            Canvas.Strokes = media.EditState.Strokes;
            Entities.ClientService = clientService;
            Entities.Items = media.EditState.Entities;
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
                Backend = AppSettings.Diagnostics.UseFFmpegPlayer
                    ? AsyncMediaPlayerBackend.FFmpeg
                    : AsyncMediaPlayerBackend.LibVlc,
                CreateSwapChain = true,
                Mute = muted,
                LogLevel = AppSettings.VerbosityLevel >= 4
                    ? AsyncMediaPlayerLogLevel.Debug
                    : AppSettings.VerbosityLevel >= 3
                    ? AsyncMediaPlayerLogLevel.Notice
                    : AppSettings.VerbosityLevel >= 2
                    ? AsyncMediaPlayerLogLevel.Warning
                    : AsyncMediaPlayerLogLevel.Error,
            };

            try
            {
                _player = new AsyncMediaPlayer(options);
            }
            catch (Exception ex)
            {
                // libvlc could not start: no plugin bank, or out of memory. Everything that
                // touches the player tolerates a null, so the editor degrades to the still
                // frame the cropper already shows.
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
            if (Canvas.BlurSource != null || IsVideo)
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
            Entities.SetView(Cropper.ContentTransform, Cropper.ViewArea, Cropper.ViewportSize);

            // Recognition measures the hand's movement on screen.
            Canvas.LoupeScale = (float)(1 / Cropper.ZoomFactor);
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
