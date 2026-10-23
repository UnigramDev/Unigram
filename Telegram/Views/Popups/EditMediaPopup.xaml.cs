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
using Telegram.Common;
using Telegram.Controls;
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

        public EditMediaPopup(XamlRoot xamlRoot, IClientService clientService, StorageMedia media, ImageCropperMask mask = ImageCropperMask.Rectangle)
            : base(xamlRoot)
        {
            InitializeComponent();

            _clientService = clientService;

            Surface.Initialize(clientService, media);
            Surface.Cropper.Changed += Cropper_Changed;

            CropToolbar.Attach(Surface.Cropper);

            Surface.Cropper.SetMask(mask);
            Surface.Cropper.SetProportions(mask == ImageCropperMask.Ellipse ? BitmapProportions.Square : media.EditState.Proportions);

            if (mask == ImageCropperMask.Ellipse)
            {
                Proportions.IsChecked = true;
                Proportions.IsEnabled = false;
            }

            _mask = mask;
            _media = media;

            Proportions.IsChecked = media.EditState.Proportions != BitmapProportions.Custom;

            if (mask == ImageCropperMask.Ellipse)
            {
                CropToolbar.IsRotationVisible = false;
            }

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;

            if (Surface.IsVideo)
            {
                FindName(nameof(TrimToolbar));
                TrimToolbar.Visibility = Visibility.Visible;
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

            FindName(nameof(QualityButton));

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
            if (QualityButton == null)
            {
                return;
            }

            var crop = GetCropSize();
            _qualityLevels = StorageVideo.GetQualityLevels(crop);

            // A muted video plays as a GIF, at whatever level was picked before.
            var muted = TrimToolbar.IsMuted;
            var size = StorageVideo.GetQualitySize(crop, QualityLevel);

            QualityButton.Glyph = muted ? "GIF" : Math.Min(size.Width, size.Height) >= 720 ? "HD" : "SD";
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
            var size = StorageVideo.GetQualitySize(GetCropSize(), level);
            QualitySize.Text = string.Format("{0} × {1}", size.Width, size.Height);
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
            PaintTabs?.Deactivate();
            PaintToolbar?.Detach();

            if (TrimToolbar != null)
            {
                TrimToolbar.MuteChanged -= TrimToolbar_MuteChanged;
                TrimToolbar.Close();
            }

            CropToolbar.Detach();

            Surface.Cropper.Changed -= Cropper_Changed;
            Surface.Close();
        }

        private void OnPreviewKeyDown(object sender, KeyRoutedEventArgs args)
        {
            if (TrimToolbar?.Visibility == Visibility.Visible)
            {
                if (args.Key == VirtualKey.Space /*&& args.Modifiers == VirtualKeyModifiers.None*/)
                {
                    Surface.Player?.Toggle();
                    args.Handled = true;
                }
            }
        }

        private void OnProcessKeyboardAccelerators(UIElement sender, ProcessKeyboardAcceleratorEventArgs args)
        {
            // Keys typed into a text object belong to the text.
            if (Surface.Entities.IsEditorFocused)
            {
                return;
            }

            if (args.Key == VirtualKey.Enter && args.Modifiers == VirtualKeyModifiers.None)
            {
                Accept_Click(null, null);
                args.Handled = true;
            }
            else if (TrimToolbar?.Visibility == Visibility.Visible)
            {
                if (TrimToolbar.ProcessKeyboardAccelerator(args.Key, args.Modifiers))
                {
                    args.Handled = true;
                }
            }
            // Draw mode, which the tabs mark: the toolbar itself hides while a sticker is selected.
            else if (PaintTabs?.Visibility == Visibility.Visible)
            {
                if (PaintToolbar.ProcessKeyboardAccelerator(args.Key, args.Modifiers))
                {
                    args.Handled = true;
                }
            }
            else if (BasicToolbar.Visibility == Visibility.Visible)
            {
                if (args.Key == VirtualKey.R && args.Modifiers == VirtualKeyModifiers.Control)
                {
                    Rotate_Click(null, null);
                    args.Handled = true;
                }
                else if (args.Key == VirtualKey.D && args.Modifiers == VirtualKeyModifiers.Control)
                {
                    Draw_Click(null, null);
                    args.Handled = true;
                }
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
            if (Surface.Cropper.IsCropEnabled)
            {
                // Waiting for the files below leaves room for a second press.
                if (_accepting)
                {
                    return;
                }

                _accepting = true;

                var entities = Surface.Entities.Items;

                // The preview drawn as the editor closes needs every sticker's file on disk.
                if (_clientService != null)
                {
                    await PaintEntityRenderer.LoadFilesAsync(_clientService, entities);
                }

                var rect = Surface.Cropper.CropRectangle;

                TimeSpan trimStartTime = default;
                TimeSpan trimStopTime = default;

                if (TrimToolbar != null)
                {
                    trimStartTime = TrimToolbar.TrimStartTime;
                    trimStopTime = TrimToolbar.TrimStopTime;
                }

                _media.EditState = new ImageGeneration
                {
                    //Rectangle = new Rect(rect.X * w, rect.Y * h, rect.Width * w, rect.Height * h),
                    Rectangle = rect,
                    Proportions = Surface.Cropper.Proportions,
                    Strokes = Surface.Canvas.Strokes,
                    Entities = entities,
                    Flip = Surface.Cropper.Flip,
                    Rotation = Surface.Cropper.Orientation,
                    Angle = Surface.Cropper.Angle,
                    TrimStartTime = trimStartTime,
                    TrimStopTime = trimStopTime,
                };

                if (_media is StorageVideo video && TrimToolbar?.CanMute == true)
                {
                    video.IsMuted = TrimToolbar.IsMuted;
                    video.Quality = _quality;
                }

                Hide(ContentDialogResult.Primary);
            }
            else
            {
                PaintToolbar.Commit();
                ShowCropTools();
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            if (Surface.Cropper.IsCropEnabled)
            {
                Hide(ContentDialogResult.Secondary);
            }
            else
            {
                PaintToolbar.Revert();
                ShowCropTools();

                Cancel();
            }
        }

        private void ShowCropTools()
        {
            Surface.Cropper.IsCropEnabled = true;

            CropToolbar.Visibility = Visibility.Visible;
            BasicToolbar.Visibility = Visibility.Visible;
            TrimToolbar?.Visibility = Visibility.Visible;
            PaintTabs.Visibility = Visibility.Collapsed;
        }

        private void Proportions_Click(object sender, RoutedEventArgs e)
        {
            if (Surface.Cropper.Proportions != BitmapProportions.Custom)
            {
                Surface.Cropper.SetProportions(BitmapProportions.Custom);
                Proportions.IsChecked = false;
            }
            else
            {
                var flyout = new MenuFlyout();
                var items = Surface.Cropper.GetProportions();

                var handler = new RoutedEventHandler((s, args) =>
                {
                    if (s is MenuFlyoutItem option)
                    {
                        Surface.Cropper.SetProportions((BitmapProportions)option.Tag);
                        Proportions.IsChecked = true;
                    }
                });

                foreach (var item in items)
                {
                    var option = new MenuFlyoutItem();
                    option.Click += handler;
                    option.Text = ProportionsToLabelConverter.Convert(item);
                    option.Tag = item;
                    option.MinWidth = 140;
                    option.HorizontalContentAlignment = HorizontalAlignment.Center;

                    flyout.Items.Add(option);
                }

                if (flyout.Items.Count > 0)
                {
                    flyout.ShowAt((GlyphToggleButton)sender);
                }
            }
        }

        private void Rotate_Click(object sender, RoutedEventArgs e)
        {
            Surface.Cropper.RotateCounterclockwise();
        }

        private void Flip_Click(object sender, RoutedEventArgs e)
        {
            Surface.Cropper.Mirror();
        }

        private void Cropper_Changed(object sender, EventArgs e)
        {
            Rotate.IsChecked = Surface.Cropper.Orientation != ImageRotation.None;
            Flip.IsChecked = Surface.Cropper.Flip != ImageFlip.None;
            Proportions.IsChecked = Surface.Cropper.Proportions != BitmapProportions.Custom;

            UpdateQuality();
        }

        private void Draw_Click(object sender, RoutedEventArgs e)
        {
            Surface.Cropper.IsCropEnabled = false;

            CropToolbar.Visibility = Visibility.Collapsed;
            BasicToolbar.Visibility = Visibility.Collapsed;
            TrimToolbar?.Visibility = Visibility.Collapsed;

            if (PaintToolbar == null)
            {
                FindName(nameof(PaintToolbar));
                FindName(nameof(PaintTabs));

                PaintToolbar.Attach(Surface, PaintTabs, _clientService);
            }

            PaintTabs.Visibility = Visibility.Visible;
            PaintToolbar.Open();
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
