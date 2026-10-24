//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Geometry;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text.Json;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Controls.Media;
using Telegram.Converters;
using Telegram.Entities;
using Telegram.Navigation;
using Telegram.Services;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Input;

namespace Telegram.Views.Popups
{
    public sealed partial class EditMediaPopup : OverlayWindow
    {
        public StorageMedia ResultMedia { get; private set; }

        private readonly ImageCropperMask _mask;
        private readonly IClientService _clientService;

        private readonly StorageMedia _media;

        // The level picked, and how many the crop offers now: a crop made smaller than the pick
        // allows takes its highest, and the pick comes back if the crop grows again.
        private int _quality;
        private int _qualityLevels;

        private bool _accepting;
        private bool _confirming;

        // What GetStateKey said once the media was in.
        private string _initialState;

        public EditMediaPopup(XamlRoot xamlRoot, IClientService clientService, StorageMedia media, ImageCropperMask mask = ImageCropperMask.Rectangle)
            : base(xamlRoot)
        {
            InitializeComponent();

            _clientService = clientService;

            Surface.Initialize(clientService, media);
            Surface.Cropper.Changed += Cropper_Changed;
            Surface.HistoryChanged += Surface_HistoryChanged;

            CropToolbar.Attach(Surface.Cropper);

            // Media for a chat opens on the crop.
            PaintTabs.Attach(Surface, clientService, true);

            Surface.Cropper.SetMask(mask);
            Surface.Cropper.SetProportions(mask == ImageCropperMask.Ellipse ? BitmapProportions.Square : media.EditState.Proportions);

            _mask = mask;
            _media = media;

            // A round crop is square and upright.
            CropToolbar.CanChangeProportions = mask != ImageCropperMask.Ellipse;
            CropToolbar.IsRotationVisible = mask != ImageCropperMask.Ellipse;

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;

            if (Surface.IsVideo)
            {
                FindName(nameof(TrimToolbar));
                Surface.Cropper.MinimumAreaSize = 64;

                InitializeVideo(media, mask);
                InitializeVideoTools(media as StorageVideo, mask);
            }
        }

        private void InitializeVideo(StorageMedia media, ImageCropperMask mask)
        {
            var profile = mask == ImageCropperMask.Ellipse;

            Surface.InitializePlayer(profile);

            TrimToolbar.MuteChanged += TrimToolbar_MuteChanged;
            TrimToolbar.Initialize(XamlRoot, Surface.Player, media, profile ? TimeSpan.FromSeconds(9.6) : null, !profile);

            Surface.Play();
        }

        private void InitializeVideoTools(StorageVideo video, ImageCropperMask mask)
        {
            // A profile video is sent silent, at a size of its own.
            if (video == null || mask == ImageCropperMask.Ellipse)
            {
                TrimToolbar.CanMute = false;
                return;
            }

            QualityButton.Visibility = Visibility.Visible;

            var state = video.EditState;
            _qualityLevels = StorageVideo.GetQualityLevels(StorageVideo.GetCropSize(video.Width, video.Height, state.Rectangle, state.Rotation));
            _quality = video.Quality ?? GetDefaultQuality(video);

            TrimToolbar.IsMuted = video.IsMuted;
            UpdateQuality();
        }

        // The level last picked for a video with as many, else the video's own size, which a
        // video over 1000 MB always starts at.
        private int GetDefaultQuality(StorageVideo video)
        {
            var remembered = AppSettings.Pencil.GetVideoQuality(_qualityLevels);
            if (remembered >= 0 && remembered < _qualityLevels && video.Size <= 1000L * 1024 * 1024)
            {
                return remembered;
            }

            return _qualityLevels - 1;
        }

        private void TrimToolbar_MuteChanged(object sender, EventArgs e)
        {
            UpdateQuality();

            if (TrimToolbar.IsMuted)
            {
                ToastPopup.Show(XamlRoot, Strings.EditorMuteHint);
            }
        }

        private int QualityLevel => Math.Min(_quality, _qualityLevels - 1);

        private Size GetCropSize()
        {
            var video = _media as StorageVideo;
            return StorageVideo.GetCropSize(video.Width, video.Height, Surface.Cropper.CropRectangle, Surface.Cropper.Orientation);
        }

        private void UpdateQuality()
        {
            if (QualityButton?.Visibility != Visibility.Visible)
            {
                return;
            }

            var crop = GetCropSize();
            _qualityLevels = StorageVideo.GetQualityLevels(crop);

            // A muted video plays as a GIF, at whatever level was picked before.
            var muted = TrimToolbar.IsMuted;
            var size = StorageVideo.GetQualitySize(crop, QualityLevel);

            QualityButton.Glyph = muted ? Icons.Gif : Math.Min(size.Width, size.Height) >= 720 ? Icons.Hd : Icons.HdOff;
            QualityButton.IsEnabled = !muted;
        }

        private void QualityButton_Click(object sender, RoutedEventArgs e)
        {
            UpdateQuality();

            if (_qualityLevels <= 1)
            {
                ToastPopup.Show(XamlRoot, Strings.VideoQualityIsTooLow);
                return;
            }

            var level = QualityLevel;

            QualitySlider.Maximum = _qualityLevels - 1;
            QualitySlider.Value = level;
            UpdateQualitySize(level);

            FlyoutBase.ShowAttachedFlyout(QualityButton);
        }

        private void QualitySlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            var level = (int)Math.Round(e.NewValue);
            if (level == QualityLevel || level < 0 || level >= _qualityLevels)
            {
                return;
            }

            _quality = level;
            AppSettings.Pencil.SetVideoQuality(_qualityLevels, level);

            UpdateQualitySize(level);
            UpdateQuality();
        }

        private void UpdateQualitySize(int level)
        {
            var crop = GetCropSize();
            var size = StorageVideo.GetQualitySize(crop, level);

            QualityName.Text = StorageVideo.QualityNames[level];
            QualitySize.Text = string.Format("~{0} • {1}x{2}", FileSizeConverter.Convert(EstimateSize(crop, size, level)), size.Width, size.Height);
        }

        // The AAC bitrate the transcoder encodes sound at, above the lowest levels.
        private const int TranscodedAudioBitrate = 128_000;

        // What the video is expected to weigh as sent: the file itself when nothing makes it
        // transcode, else the transcoder's bitrate for the output over the trimmed length.
        private long EstimateSize(Size crop, Size output, int level)
        {
            var video = _media as StorageVideo;
            var seconds = video.TotalMilliseconds / 1000;

            var start = TrimToolbar.TrimStartTime;
            var stop = TrimToolbar.TrimStopTime != default
                ? TrimToolbar.TrimStopTime
                : TimeSpan.FromMilliseconds(video.TotalMilliseconds);

            var cropper = Surface.Cropper;
            var rectangle = cropper.CropRectangle;

            // As StorageVideo.GetGeneration decides it, from what the editor holds now.
            var transcode = StorageVideo.QualitySides[level] < Math.Max(crop.Width, crop.Height)
                || TrimToolbar.TrimStartTime != default
                || TrimToolbar.TrimStopTime != default
                || Surface.Canvas.StrokeCount > 0
                || !Surface.Entities.IsEmpty
                || rectangle.X != 0 || rectangle.Y != 0 || rectangle.Right != 1 || rectangle.Bottom != 1
                || cropper.Angle != 0 || cropper.Orientation != ImageRotation.None || cropper.Flip != ImageFlip.None;

            if (!transcode || seconds <= 0)
            {
                return (long)video.Size;
            }

            // The source's bitrate as the transcoder probes it, from the file's size.
            var original = (int)Math.Min(int.MaxValue, video.Size * 8 / seconds);

            // Lighter only when picked down to it, as the transcode decides.
            var audio = level < GenerationService.LowLevels && StorageVideo.QualitySides[level] < Math.Max(crop.Width, crop.Height)
                ? GenerationService.LowLevelAudioBitrate
                : TranscodedAudioBitrate;

            var bitrate = GenerationService.MakeVideoBitrate(video.Height, video.Width, original, (int)output.Height, (int)output.Width)
                + (TrimToolbar.IsMuted || !video.HasAudio ? 0 : audio);

            return (long)(bitrate * (stop - start).TotalSeconds / 8);
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (_mask == ImageCropperMask.Ellipse)
            {
                await Surface.Cropper.SetSourceAsync(_media.File, proportions: BitmapProportions.Square);
            }
            else
            {
                await Surface.Cropper.SetSourceAsync(_media.File, _media.EditState.Rotation, _media.EditState.Flip, _media.EditState.Proportions, _media.EditState.Rectangle, _media.EditState.Angle);
            }

            Surface.StartHistory();
            _initialState = GetStateKey();

            if (_media.EditState.Strokes is { } strokes)
            {
                foreach (var stroke in strokes)
                {
                    if (stroke.Tool == PencilTool.Blur)
                    {
                        Surface.LoadBlurSource();
                        break;
                    }
                }
            }

            ConnectedAnimationServiceEx.TryStart("EditMediaPopup", Surface.Cropper);
        }

        // The toolbars let go of the player before the surface closes it.
        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            PaintTabs.Deactivate();
            PaintToolbar?.Detach();

            if (TrimToolbar != null)
            {
                TrimToolbar.MuteChanged -= TrimToolbar_MuteChanged;
                TrimToolbar.Close();
            }

            CropToolbar.Detach();

            Surface.Cropper.Changed -= Cropper_Changed;
            Surface.HistoryChanged -= Surface_HistoryChanged;
            Surface.Close();
        }

        private void OnPreviewKeyDown(object sender, KeyRoutedEventArgs args)
        {
            if (TrimToolbar != null && args.Key == VirtualKey.Space && !Surface.Entities.IsEditorFocused)
            {
                Surface.Player?.Toggle();
                args.Handled = true;
            }
        }

        private void OnProcessKeyboardAccelerators(UIElement sender, ProcessKeyboardAcceleratorEventArgs args)
        {
            // Keys typed into a text object belong to the text.
            if (Surface.Entities.IsEditorFocused)
            {
                return;
            }

            var handled = true;

            if (args.Key == VirtualKey.Enter && args.Modifiers == VirtualKeyModifiers.None)
            {
                Accept_Click(null, null);
            }
            else if (args.Key == VirtualKey.Z && args.Modifiers == VirtualKeyModifiers.Control)
            {
                Surface.Undo();
            }
            else if ((args.Key == VirtualKey.Y && args.Modifiers == VirtualKeyModifiers.Control)
                || (args.Key == VirtualKey.Z && args.Modifiers == (VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift)))
            {
                Surface.Redo();
            }
            else if (args.Key == VirtualKey.R && args.Modifiers == VirtualKeyModifiers.Control && PaintTabs.IsCropping)
            {
                Surface.Cropper.RotateCounterclockwise();
            }
            else if (args.Key == VirtualKey.D && args.Modifiers == VirtualKeyModifiers.Control && PaintTabs.IsCropping)
            {
                PaintTabs.OpenDrawing();
            }
            else
            {
                // The drawing's keys while a drawing tab is open, then the video's.
                handled = (!PaintTabs.IsCropping && PaintToolbar?.ProcessKeyboardAccelerator(args.Key, args.Modifiers) == true)
                    || TrimToolbar?.ProcessKeyboardAccelerator(args.Key, args.Modifiers) == true;
            }

            if (handled)
            {
                args.Handled = true;
            }
        }

        protected override void OnBackRequestedOverride(object sender, BackRequestedRoutedEventArgs e)
        {
            if (Surface.HandleBack())
            {
                // Spent on what was in hand: the next back is free to leave.
                Cancel();
            }
            else
            {
                Cancel_Click(null, null);
            }

            e.Handled = true;
        }

        public bool IsCropEnabled
        {
            get => Surface.Cropper.IsCropEnabled;
            set => Surface.Cropper.IsCropEnabled = value;
        }

        public Rect CropRectangle
        {
            get { return Surface.Cropper.CropRectangle; }
        }

        private async void Accept_Click(object sender, RoutedEventArgs e)
        {
            // Waiting for the files below leaves room for a second press.
            if (_accepting)
            {
                return;
            }

            _accepting = true;

            PaintToolbar?.Save();

            var entities = Surface.Entities.Items;

            // The preview drawn as the editor closes needs every sticker's file on disk.
            if (_clientService != null)
            {
                await PaintEntityRenderer.LoadFilesAsync(_clientService, entities);
            }

            _media.EditState = CreateEditState(entities);

            if (_media is StorageVideo video && TrimToolbar?.CanMute == true)
            {
                video.IsMuted = TrimToolbar.IsMuted;
                video.Quality = _quality;
            }

            Hide(ContentDialogResult.Primary);
        }

        // Closing asks first only when it would drop something: the edit, as Done would write it,
        // differs from what it was once the media was in.
        private async void Cancel_Click(object sender, RoutedEventArgs e)
        {
            if (_confirming)
            {
                return;
            }

            if (_initialState != null && GetStateKey() != _initialState)
            {
                _confirming = true;

                var confirm = await MessagePopup.ShowNestedAsync(XamlRoot, Strings.PhotoEditorDiscardAlert, Strings.DiscardChanges, Strings.Discard, Strings.Cancel, destructive: true, requestedTheme: ElementTheme.Dark);

                _confirming = false;

                if (confirm != ContentDialogResult.Primary)
                {
                    return;
                }
            }

            Hide(ContentDialogResult.Secondary);
        }

        // The edit as Done writes it, from what the layers last saved.
        private ImageGeneration CreateEditState(IReadOnlyList<PaintEntity> entities)
        {
            TimeSpan trimStartTime = default;
            TimeSpan trimStopTime = default;

            if (TrimToolbar != null)
            {
                trimStartTime = TrimToolbar.TrimStartTime;
                trimStopTime = TrimToolbar.TrimStopTime;
            }

            return new ImageGeneration
            {
                Rectangle = Surface.Cropper.CropRectangle,
                Proportions = Surface.Cropper.Proportions,
                Strokes = Surface.Canvas.Strokes,
                Entities = entities,
                Flip = Surface.Cropper.Flip,
                Rotation = Surface.Cropper.Orientation,
                Angle = Surface.Cropper.Angle,
                TrimStartTime = trimStartTime,
                TrimStopTime = trimStopTime,
            };
        }

        // Everything Done would keep, the video's settings outside the edit included. Moving or
        // restyling an object is not in the undo history, so the history cannot tell this.
        private string GetStateKey()
        {
            PaintToolbar?.Save();

            var state = JsonSerializer.Serialize(CreateEditState(Surface.Entities.Items), GenerationJsonContext.Default.ImageGeneration);
            return TrimToolbar != null
                ? $"{state}|{TrimToolbar.IsMuted}|{_quality}"
                : state;
        }

        private void Undo_Click(object sender, RoutedEventArgs e)
        {
            Surface.Undo();
        }

        private void Redo_Click(object sender, RoutedEventArgs e)
        {
            Surface.Redo();
        }

        private void Surface_HistoryChanged(object sender, EventArgs e)
        {
            UndoButton.IsEnabled = Surface.CanUndo;
            RedoButton.IsEnabled = Surface.CanRedo;
        }

        // The crop is the first tab, so its row comes from the left and leaves that way.
        private void PaintTabs_CroppingChanged(object sender, EventArgs e)
        {
            if (PaintTabs.IsCropping)
            {
                PaintToolbar?.Suspend(-1);

                Surface.Cropper.IsCropEnabled = true;
                MediaEditorTransitions.SlideIn(CropToolbar, -1);
            }
            else
            {
                Surface.Cropper.IsCropEnabled = false;
                MediaEditorTransitions.SlideOut(CropToolbar, 1);

                if (PaintToolbar == null)
                {
                    FindName(nameof(PaintToolbar));
                    PaintToolbar.Attach(Surface, PaintTabs);
                }

                PaintToolbar.Resume(1);
            }
        }

        private void Cropper_Changed(object sender, EventArgs e)
        {
            UpdateQuality();
        }
    }

    public sealed partial class SmoothPathBuilder
    {
        private List<Vector2> _controlPoints;
        private readonly List<Vector2> _path;

        private Vector2 _beginPoint;

        public SmoothPathBuilder(Vector2 beginPoint)
        {
            _beginPoint = beginPoint;

            _controlPoints = new List<Vector2>();
            _path = new List<Vector2>();
        }

        public Color? Stroke { get; set; }
        public float StrokeThickness { get; set; }

        public void MoveTo(Vector2 point)
        {
            if (_controlPoints.Count < 4)
            {
                _controlPoints.Add(point);
                return;
            }

            var endPoint = new Vector2(
                (_controlPoints[2].X + point.X) / 2,
                (_controlPoints[2].Y + point.Y) / 2);

            _path.Add(_controlPoints[1]);
            _path.Add(_controlPoints[2]);
            _path.Add(endPoint);

            _controlPoints = new List<Vector2> { endPoint, point };
        }

        public void EndFigure(Vector2 point)
        {
            if (_controlPoints.Count > 1)
            {
                for (int i = 0; i < _controlPoints.Count; i++)
                {
                    MoveTo(point);
                }
            }
        }

        public CanvasGeometry ToGeometry(ICanvasResourceCreator resourceCreator, Vector2 canvasSize)
        {
            //var multiplier = NSMakePoint(imageSize.width / touch.canvasSize.width, imageSize.height / touch.canvasSize.height)
            var multiplier = canvasSize; //_imageSize / canvasSize;

            using var builder = new CanvasPathBuilder(resourceCreator);
            builder.BeginFigure(_beginPoint * multiplier);

            for (int i = 0; i < _path.Count; i += 3)
            {
                builder.AddCubicBezier(
                    _path[i] * multiplier,
                    _path[i + 1] * multiplier,
                    _path[i + 2] * multiplier);
            }

            builder.EndFigure(CanvasFigureLoop.Open);

            return CanvasGeometry.CreatePath(builder);
        }
    }
}
