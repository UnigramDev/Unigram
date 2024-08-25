//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Geometry;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Telegram.Controls;
using Telegram.Converters;
using Telegram.Entities;
using Telegram.Native;
using Telegram.Navigation;
using Telegram.Services;
using Telegram.Services.Keyboard;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Media.Core;
using Windows.Storage;
using Windows.UI;

namespace Telegram.Views.Popups
{
    public sealed partial class EditMediaPopup : OverlayWindow
    {
        public StorageMedia ResultMedia { get; private set; }

        private readonly ImageCropperMask _mask;

        private readonly StorageFile _file;
        private readonly StorageMedia _media;

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

            _mask = mask;

            _file = media.File;
            _media = media;

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;

            if (mask == ImageCropperMask.Ellipse && string.Equals(media.File.FileType, ".mp4", StringComparison.OrdinalIgnoreCase))
            {
                FindName(nameof(TrimToolbar));
                TrimToolbar.Visibility = Visibility.Visible;
                BasicToolbar.Visibility = Visibility.Collapsed;

                InitializeVideo(media.File);
            }
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            var context = WindowContext.ForXamlRoot(this);
            if (context != null)
            {
                context.InputListener.KeyDown += OnAcceleratorKeyActivated;
            }

            if (_mask == ImageCropperMask.Ellipse)
            {
                await Cropper.SetSourceAsync(_media.File, proportions: BitmapProportions.Square);
            }
            else
            {
                await Cropper.SetSourceAsync(_media.File, _media.EditState.Rotation, _media.EditState.Flip, _media.EditState.Proportions, _media.EditState.Rectangle);
            }
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            var context = WindowContext.ForXamlRoot(this);
            if (context != null)
            {
                context.InputListener.KeyDown -= OnAcceleratorKeyActivated;
            }

            Media.Source = null;
        }

        private void OnAcceleratorKeyActivated(Window sender, InputKeyDownEventArgs args)
        {
            if (args.VirtualKey == Windows.System.VirtualKey.Enter && args.OnlyKey)
            {
                Accept_Click(null, null);
                args.Handled = true;
            }
            else if (DrawToolbar?.Visibility == Visibility.Visible)
            {
                if (args.VirtualKey == Windows.System.VirtualKey.Z && args.OnlyControl && Canvas.CanUndo)
                {
                    Canvas.Undo();
                    args.Handled = true;
                }
                else if (args.VirtualKey == Windows.System.VirtualKey.Y && args.OnlyControl && Canvas.CanRedo)
                {
                    Canvas.Redo();
                    args.Handled = true;
                }
                else if (args.VirtualKey == Windows.System.VirtualKey.D && args.OnlyControl)
                {
                    Brush_Click(null, null);
                    args.Handled = true;
                }
                else if (args.VirtualKey == Windows.System.VirtualKey.E && args.OnlyControl)
                {
                    Erase_Click(null, null);
                    args.Handled = true;
                }
            }
            else if (BasicToolbar.Visibility == Visibility.Visible)
            {
                if (args.VirtualKey == Windows.System.VirtualKey.R && args.OnlyControl)
                {
                    Rotate_Click(null, null);
                    args.Handled = true;
                }
                else if (args.VirtualKey == Windows.System.VirtualKey.D && args.OnlyControl)
                {
                    Draw_Click(null, null);
                    args.Handled = true;
                }
            }
        }

        protected override void OnBackRequestedOverride(object sender, BackRequestedRoutedEventArgs e)
        {
            Cancel_Click(null, null);
            e.Handled = true;
        }

        public bool IsCropEnabled
        {
            get => Cropper.IsCropEnabled;
            set => Cropper.IsCropEnabled = value;
        }

        public Rect CropRectangle
        {
            get { return Cropper.CropRectangle; }
        }

        private async void InitializeVideo(StorageFile file)
        {
            Media.Source = MediaSource.CreateFromStorageFile(file);
            Media.MediaPlayer.AutoPlay = true;
            Media.MediaPlayer.IsMuted = true;
            Media.MediaPlayer.IsLoopingEnabled = true;
            Media.MediaPlayer.PlaybackSession.PositionChanged += MediaPlayer_PositionChanged;

            using var stream = await file.OpenReadAsync();
            using var animation = await Task.Run(() => VideoAnimation.LoadFromFile(new VideoAnimationStreamSource(stream), false, false, false));

            double ratioX = (double)40 / animation.PixelWidth;
            double ratioY = (double)40 / animation.PixelHeight;
            double ratio = Math.Max(ratioY, ratioY);

            var width = (int)(animation.PixelWidth * ratio);
            var height = (int)(animation.PixelHeight * ratio);

            var count = Math.Ceiling(296d / width);

            width = (int)(width * XamlRoot.RasterizationScale);
            height = (int)(height * XamlRoot.RasterizationScale);

            TrimRange.OriginalDuration = TimeSpan.FromMilliseconds(animation.Duration);
            TrimThumbnails.Children.Clear();

            try
            {
                for (int i = 0; i < count; i++)
                {
                    var bitmap = new WriteableBitmap(width, height);
                    var buffer = new PixelBuffer(bitmap);

                    await Task.Run(() =>
                    {
                        animation.SeekToMilliseconds((long)(animation.Duration / count * i), false);
                        animation.RenderSync(buffer, width, height, false, out _);
                    });

                    var image = new Image();
                    image.Height = 40;
                    image.Stretch = Microsoft.UI.Xaml.Media.Stretch.UniformToFill;
                    image.Source = bitmap;

                    TrimThumbnails.Children.Add(image);
                }
            }
            catch { }
        }

        private void Accept_Click(object sender, RoutedEventArgs e)
        {
            if (Cropper.IsCropEnabled)
            {
                var rect = Cropper.CropRectangle;

                TimeSpan trimStartTime = default;
                TimeSpan trimStopTime = default;

                if (TrimRange != null)
                {
                    trimStartTime = TimeSpan.FromMilliseconds(TrimRange.Minimum * TrimRange.OriginalDuration.TotalMilliseconds);
                    trimStopTime = TimeSpan.FromMilliseconds(TrimRange.Maximum * TrimRange.OriginalDuration.TotalMilliseconds);
                }

                _media.EditState = new BitmapEditState
                {
                    //Rectangle = new Rect(rect.X * w, rect.Y * h, rect.Width * w, rect.Height * h),
                    Rectangle = rect,
                    Proportions = Cropper.Proportions,
                    Strokes = Canvas.Strokes,
                    Flip = _flip,
                    Rotation = _rotation,
                    TrimStartTime = trimStartTime,
                    TrimStopTime = trimStopTime,
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

                Cancel();
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
            {
                FindName(nameof(DrawToolbar));
            }

            if (DrawSlider == null)
            {
                FindName(nameof(DrawSlider));
            }

            DrawToolbar.Visibility = Visibility.Visible;
            DrawSlider.Visibility = Visibility.Visible;
            DrawSlider.SetDefault(SettingsService.Current.Pencil);

            Canvas.Mode = PencilCanvasMode.Stroke;
            Canvas.Stroke = DrawSlider.Stroke;
            Canvas.StrokeThickness = DrawSlider.StrokeThickness;

            Brush.IsChecked = true;
            Erase.IsChecked = false;

            InvalidateToolbar();
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
