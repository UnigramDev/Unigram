using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Geometry;
using System;
using System.Collections.Generic;
using System.Numerics;
using Unigram.Controls;
using Unigram.Converters;
using Unigram.Entities;
using Unigram.Services;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Media.Core;
using Windows.Media.Editing;
using Windows.Storage;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;

namespace Unigram.Views.Popups
{
    public sealed partial class EditMediaPopup : OverlayPage
    {
        public StorageMedia ResultMedia { get; private set; }

        private StorageFile _file;
        private StorageMedia _media;

        private BitmapRotation _rotation = BitmapRotation.None;
        private BitmapFlip _flip = BitmapFlip.None;

        public EditMediaPopup(StorageMedia media, ImageCropperMask mask = ImageCropperMask.Rectangle)
        {
            InitializeComponent();

            Canvas.Strokes = media.EditState.Strokes;

            Cropper.SetMask(mask);
            Cropper.SetProportions(mask == ImageCropperMask.Ellipse ? BitmapProportions.Square : media.EditState.Proportions);

            if (mask == ImageCropperMask.Ellipse)
            {
                Proportions.IsChecked = true;
                Proportions.IsEnabled = false;
            }

            _file = media.File;
            _media = media;

            Loaded += async (s, args) =>
            {
                await Cropper.SetSourceAsync(media.File, media.EditState.Rotation, media.EditState.Flip, mask == ImageCropperMask.Ellipse ? BitmapProportions.Square : media.EditState.Proportions, mask == ImageCropperMask.Ellipse ? new Rect?() : media.EditState.Rectangle);
            };
            Unloaded += (s, args) =>
            {
                Media.Source = null;
            };

            if (mask == ImageCropperMask.Ellipse && string.Equals(media.File.FileType, ".mp4", StringComparison.OrdinalIgnoreCase))
            {
                FindName(nameof(TrimToolbar));
                TrimToolbar.Visibility = Visibility.Visible;
                BasicToolbar.Visibility = Visibility.Collapsed;

                InitializeVideo(media.File);
            }
        }

        public bool IsCropEnabled
        {
            get { return this.Cropper.IsCropEnabled; }
            set { this.Cropper.IsCropEnabled = value; }
        }

        public Rect CropRectangle
        {
            get { return this.Cropper.CropRectangle; }
        }

        private async void InitializeVideo(StorageFile file)
        {
            Media.Source = MediaSource.CreateFromStorageFile(file);
            Media.MediaPlayer.AutoPlay = true;
            Media.MediaPlayer.IsMuted = true;
            Media.MediaPlayer.IsLoopingEnabled = true;
            Media.MediaPlayer.PlaybackSession.PositionChanged += MediaPlayer_PositionChanged;

            var clip = await MediaClip.CreateFromFileAsync(file);
            var composition = new MediaComposition();
            composition.Clips.Add(clip);

            var props = clip.GetVideoEncodingProperties();

            double ratioX = (double)40 / props.Width;
            double ratioY = (double)40 / props.Height;
            double ratio = Math.Max(ratioY, ratioY);

            var width = (int)(props.Width * ratio);
            var height = (int)(props.Height * ratio);

            var count = Math.Ceiling(296d / width);

            var times = new List<TimeSpan>();

            for (int i = 0; i < count; i++)
            {
                times.Add(clip.OriginalDuration / count * i);
            }

            TrimThumbnails.Children.Clear();

            var thumbnails = await composition.GetThumbnailsAsync(times, width, height, VideoFramePrecision.NearestKeyFrame);

            foreach (var thumb in thumbnails)
            {
                var bitmap = new BitmapImage();
                await bitmap.SetSourceAsync(thumb);

                var image = new Image();
                image.Width = width;
                image.Height = height;
                image.Stretch = Windows.UI.Xaml.Media.Stretch.UniformToFill;
                image.Source = bitmap;

                TrimThumbnails.Children.Add(image);
            }

            TrimRange.SetOriginalDuration(clip.OriginalDuration);
        }

        private void Accept_Click(object sender, RoutedEventArgs e)
        {
            if (Cropper.IsCropEnabled)
            {
                var rect = Cropper.CropRectangle;
                var w = Cropper.PixelWidth;
                var h = Cropper.PixelHeight;

                _media.EditState = new BitmapEditState
                {
                    //Rectangle = new Rect(rect.X * w, rect.Y * h, rect.Width * w, rect.Height * h),
                    Rectangle = rect,
                    Proportions = Cropper.Proportions,
                    Strokes = Canvas.Strokes,
                    Flip = _flip,
                    Rotation = _rotation,
                    TrimStartTime = TimeSpan.FromMilliseconds(TrimRange.Minimum * TrimRange.OriginalDuration.TotalMilliseconds),
                    TrimStopTime = TimeSpan.FromMilliseconds(TrimRange.Maximum * TrimRange.OriginalDuration.TotalMilliseconds),
                };

                Hide(ContentDialogResult.Primary);
            }
            else
            {
                Canvas.SaveState();

                Cropper.IsCropEnabled = true;
                Canvas.IsEnabled = false;

                BasicToolbar.Visibility = Visibility.Visible;
                DrawToolbar.Visibility = Visibility.Collapsed;
                DrawSlider.Visibility = Visibility.Collapsed;

                SettingsService.Current.Pencil = DrawSlider.GetDefault();
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            if (Cropper.IsCropEnabled)
            {
                Hide(ContentDialogResult.Secondary);
            }
            else
            {
                Canvas.RestoreState();

                Cropper.IsCropEnabled = true;
                Canvas.IsEnabled = false;

                BasicToolbar.Visibility = Visibility.Visible;
                DrawToolbar.Visibility = Visibility.Collapsed;
                DrawSlider.Visibility = Visibility.Collapsed;

                SettingsService.Current.Pencil = DrawSlider.GetDefault();
            }
        }

        private void Proportions_Click(object sender, RoutedEventArgs e)
        {
            if (Cropper.Proportions != BitmapProportions.Custom)
            {
                Cropper.SetProportions(BitmapProportions.Custom);
                Proportions.IsChecked = false;
            }
            else
            {
                var flyout = new MenuFlyout();
                var items = Cropper.GetProportions();

                var handler = new RoutedEventHandler((s, args) =>
                {
                    if (s is MenuFlyoutItem option)
                    {
                        Cropper.SetProportions((BitmapProportions)option.Tag);
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

        private async void Rotate_Click(object sender, RoutedEventArgs e)
        {
            var rotation = BitmapRotation.None;

            var proportions = RotateProportions(Cropper.Proportions);
            var rectangle = RotateArea(Cropper.CropRectangle);

            switch (_rotation)
            {
                case BitmapRotation.None:
                    rotation = BitmapRotation.Clockwise90Degrees;
                    break;
                case BitmapRotation.Clockwise90Degrees:
                    rotation = BitmapRotation.Clockwise180Degrees;
                    break;
                case BitmapRotation.Clockwise180Degrees:
                    rotation = BitmapRotation.Clockwise270Degrees;
                    break;
            }

            _rotation = rotation;
            await Cropper.SetSourceAsync(_file, rotation, _flip, proportions, rectangle);

            Rotate.IsChecked = _rotation != BitmapRotation.None;
            Canvas.Invalidate();
        }

        private Rect RotateArea(Rect area)
        {
            var point = new Point(1 - area.Bottom, 1 - (1 - area.X));
            var result = new Rect(point.X, point.Y, area.Height, area.Width);

            return result;
        }

        private BitmapProportions RotateProportions(BitmapProportions proportions)
        {
            switch (proportions)
            {
                case BitmapProportions.Original:
                case BitmapProportions.Square:
                default:
                    return proportions;
                // Portrait
                case BitmapProportions.TwoOverThree:
                    return BitmapProportions.ThreeOverTwo;
                case BitmapProportions.ThreeOverFive:
                    return BitmapProportions.FiveOverThree;
                case BitmapProportions.ThreeOverFour:
                    return BitmapProportions.FourOverThree;
                case BitmapProportions.FourOverFive:
                    return BitmapProportions.FiveOverFour;
                case BitmapProportions.FiveOverSeven:
                    return BitmapProportions.SevenOverFive;
                case BitmapProportions.NineOverSixteen:
                    return BitmapProportions.SixteenOverNine;
                // Landscape
                case BitmapProportions.ThreeOverTwo:
                    return BitmapProportions.TwoOverThree;
                case BitmapProportions.FiveOverThree:
                    return BitmapProportions.ThreeOverFive;
                case BitmapProportions.FourOverThree:
                    return BitmapProportions.ThreeOverFour;
                case BitmapProportions.FiveOverFour:
                    return BitmapProportions.FourOverFive;
                case BitmapProportions.SevenOverFive:
                    return BitmapProportions.FiveOverSeven;
                case BitmapProportions.SixteenOverNine:
                    return BitmapProportions.NineOverSixteen;
            }
        }

        private async void Flip_Click(object sender, RoutedEventArgs e)
        {
            var flip = _flip;
            var rotation = _rotation;

            var proportions = Cropper.Proportions;
            var rectangle = FlipArea(Cropper.CropRectangle);

            switch (rotation)
            {
                case BitmapRotation.Clockwise90Degrees:
                case BitmapRotation.Clockwise270Degrees:
                    switch (flip)
                    {
                        case BitmapFlip.None:
                            flip = BitmapFlip.Vertical;
                            break;
                        case BitmapFlip.Vertical:
                            flip = BitmapFlip.None;
                            break;
                        case BitmapFlip.Horizontal:
                            flip = BitmapFlip.None;
                            rotation = rotation == BitmapRotation.Clockwise90Degrees
                                ? BitmapRotation.Clockwise270Degrees
                                : BitmapRotation.Clockwise90Degrees;
                            break;
                    }
                    break;
                case BitmapRotation.None:
                case BitmapRotation.Clockwise180Degrees:
                    switch (flip)
                    {
                        case BitmapFlip.None:
                            flip = BitmapFlip.Horizontal;
                            break;
                        case BitmapFlip.Horizontal:
                            flip = BitmapFlip.None;
                            break;
                        case BitmapFlip.Vertical:
                            flip = BitmapFlip.None;
                            rotation = rotation == BitmapRotation.None
                                ? BitmapRotation.Clockwise180Degrees
                                : BitmapRotation.None;
                            break;
                    }
                    break;
            }

            _flip = flip;
            _rotation = rotation;
            await Cropper.SetSourceAsync(_file, _rotation, flip, proportions, rectangle);

            //Transform.ScaleX = _flip == BitmapFlip.Horizontal ? -1 : 1;
            //Transform.ScaleY = _flip == BitmapFlip.Vertical ? -1 : 1;

            Flip.IsChecked = _flip != BitmapFlip.None;
            Canvas.Invalidate();
        }

        private Rect FlipArea(Rect area)
        {
            var point = new Point(1 - area.Right, area.Y);
            var result = new Rect(point.X, point.Y, area.Width, area.Height);

            return result;
        }

        private void Draw_Click(object sender, RoutedEventArgs e)
        {
            Cropper.IsCropEnabled = false;
            Canvas.IsEnabled = true;

            BasicToolbar.Visibility = Visibility.Collapsed;

            if (DrawToolbar == null)
                FindName(nameof(DrawToolbar));
            if (DrawSlider == null)
                FindName(nameof(DrawSlider));

            DrawToolbar.Visibility = Visibility.Visible;
            DrawSlider.Visibility = Visibility.Visible;
            DrawSlider.SetDefault(SettingsService.Current.Pencil);

            Canvas.Mode = PencilCanvasMode.Stroke;
            Canvas.Stroke = DrawSlider.Stroke;
            Canvas.StrokeThickness = DrawSlider.StrokeThickness;

            Brush.IsChecked = true;
            Erase.IsChecked = false;
        }

        private void Brush_Click(object sender, RoutedEventArgs e)
        {
            if (Canvas.Mode != PencilCanvasMode.Stroke)
            {
                Canvas.Mode = PencilCanvasMode.Stroke;
                Brush.IsChecked = true;
                Erase.IsChecked = false;
            }
        }

        private void Erase_Click(object sender, RoutedEventArgs e)
        {
            if (Canvas.Mode != PencilCanvasMode.Eraser)
            {
                Canvas.Mode = PencilCanvasMode.Eraser;
                Brush.IsChecked = false;
                Erase.IsChecked = true;
            }
        }

        private void Undo_Click(object sender, RoutedEventArgs e)
        {
            Canvas.Undo();
        }

        private void Redo_Click(object sender, RoutedEventArgs e)
        {
            Canvas.Redo();
        }

        private void DrawSlider_StrokeChanged(object sender, EventArgs e)
        {
            Canvas.Stroke = DrawSlider.Stroke;
            Canvas.StrokeThickness = DrawSlider.StrokeThickness;

            Brush_Click(null, null);
        }

        private void Canvas_StrokesChanged(object sender, EventArgs e)
        {
            InvalidateToolbar();
        }

        private void InvalidateToolbar()
        {
            if (Undo != null)
            {
                Undo.IsEnabled = Canvas.CanUndo;
            }

            if (Redo != null)
            {
                Redo.IsEnabled = Canvas.CanRedo;
            }
        }

        private async void MediaPlayer_PositionChanged(Windows.Media.Playback.MediaPlaybackSession sender, object args)
        {
            await TrimRange.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                if (sender.Position.TotalMilliseconds >= TrimRange.Maximum * sender.NaturalDuration.TotalMilliseconds)
                {
                    sender.Position = TimeSpan.FromMilliseconds(sender.NaturalDuration.TotalMilliseconds * TrimRange.Minimum);
                    return;
                }

                TrimRange.Value = sender.Position.TotalMilliseconds / sender.NaturalDuration.TotalMilliseconds;
            });
        }

        private void TrimRange_MinimumChanged(object sender, double e)
        {
            Media.MediaPlayer.Pause();
            Media.MediaPlayer.PlaybackSession.Position =
                TimeSpan.FromMilliseconds(Media.MediaPlayer.PlaybackSession.NaturalDuration.TotalMilliseconds * e);
        }

        private void TrimRange_MaximumChanged(object sender, double e)
        {
            Media.MediaPlayer.PlaybackSession.Position =
                TimeSpan.FromMilliseconds(Media.MediaPlayer.PlaybackSession.NaturalDuration.TotalMilliseconds * e);
            Media.MediaPlayer.Play();
        }
    }

    public sealed class SmoothPathBuilder
    {
        private List<Vector2> _controlPoints;
        private List<Vector2> _path;

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

            var builder = new CanvasPathBuilder(resourceCreator);
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
