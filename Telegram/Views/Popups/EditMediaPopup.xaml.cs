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
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Converters;
using Telegram.Entities;
using Telegram.Native;
using Telegram.Navigation;
using Telegram.Services;
using Windows.Foundation;
using Windows.Media.Core;
using Windows.Storage;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Media.Imaging;

namespace Telegram.Views.Popups
{
    public sealed partial class EditMediaPopup : OverlayWindow
    {
        public StorageMedia ResultMedia { get; private set; }

        private readonly ImageCropperMask _mask;

        private readonly StorageFile _file;
        private readonly StorageMedia _media;

        private ImageRotation _rotation;
        private ImageFlip _flip;

        private TimeSpan _duration;
        private bool _resume;

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
            _rotation = media.EditState.Rotation;
            _flip = media.EditState.Flip;

            Rotate.IsChecked = _rotation != ImageRotation.None;
            Flip.IsChecked = _flip != ImageFlip.None;
            Proportions.IsChecked = media.EditState.Proportions != BitmapProportions.Custom;

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;

            if (Constants.VideoTypes.Contains(media.File.FileType.ToLower()))
            {
                FindName(nameof(TrimToolbar));
                TrimToolbar.Visibility = Visibility.Visible;
                BasicToolbar.Visibility = Visibility.Collapsed;

                InitializeVideo(media, mask);
            }
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (_mask == ImageCropperMask.Ellipse)
            {
                await Cropper.SetSourceAsync(_media.File, proportions: BitmapProportions.Square);
            }
            else
            {
                await Cropper.SetSourceAsync(_media.File, _media.EditState.Rotation, _media.EditState.Flip, _media.EditState.Proportions, _media.EditState.Rectangle);
            }

            var animation = ConnectedAnimationService.GetForCurrentView().GetAnimation("EditMediaPopup");
            animation?.TryStart(Cropper);
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            Media.Source = null;
        }

        private void OnPreviewKeyDown(object sender, KeyRoutedEventArgs args)
        {
            if (TrimToolbar?.Visibility == Visibility.Visible)
            {
                if (args.Key == VirtualKey.Space /*&& args.Modifiers == VirtualKeyModifiers.None*/)
                {
                    if (Media.MediaPlayer.PlaybackSession.PlaybackState == Windows.Media.Playback.MediaPlaybackState.Playing)
                    {
                        Media.MediaPlayer.Pause();
                    }
                    else
                    {
                        Media.MediaPlayer.Play();
                    }

                    args.Handled = true;
                }
            }
        }

        private void OnProcessKeyboardAccelerators(UIElement sender, ProcessKeyboardAcceleratorEventArgs args)
        {
            if (args.Key == VirtualKey.Enter && args.Modifiers == VirtualKeyModifiers.None)
            {
                Accept_Click(null, null);
                args.Handled = true;
            }
            else if (TrimToolbar?.Visibility == Visibility.Visible)
            {
                if (args.Key == VirtualKey.M && args.Modifiers == VirtualKeyModifiers.None)
                {
                    Media.MediaPlayer.IsMuted = !Media.MediaPlayer.IsMuted;
                    args.Handled = true;
                }
            }
            else if (DrawToolbar?.Visibility == Visibility.Visible)
            {
                if (args.Key == VirtualKey.Z && args.Modifiers == VirtualKeyModifiers.Control && Canvas.CanUndo)
                {
                    Canvas.Undo();
                    args.Handled = true;
                }
                else if (args.Key == VirtualKey.Y && args.Modifiers == VirtualKeyModifiers.Control && Canvas.CanRedo)
                {
                    Canvas.Redo();
                    args.Handled = true;
                }
                else if (args.Key == VirtualKey.D && args.Modifiers == VirtualKeyModifiers.Control)
                {
                    Brush_Click(null, null);
                    args.Handled = true;
                }
                else if (args.Key == VirtualKey.E && args.Modifiers == VirtualKeyModifiers.Control)
                {
                    Erase_Click(null, null);
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

        private async void InitializeVideo(StorageMedia media, ImageCropperMask mask)
        {
            try
            {
                Media.Source = MediaSource.CreateFromStorageFile(media.File);
                Media.MediaPlayer.AutoPlay = true;
                Media.MediaPlayer.IsMuted = mask == ImageCropperMask.Ellipse;
                Media.MediaPlayer.IsLoopingEnabled = true;
                Media.MediaPlayer.PlaybackSession.PositionChanged += MediaPlayer_PositionChanged;

                using var stream = await media.File.OpenReadAsync();
                using var animation = await Task.Run(() => VideoAnimation.LoadFromFile(new VideoAnimationStreamSource(stream), false, false, false));

                var pixelWidth = animation.PixelWidth;
                var pixelHeight = animation.PixelHeight;

                if (animation.Rotation is 90 or 270)
                {
                    pixelWidth = animation.PixelHeight;
                    pixelHeight = animation.PixelWidth;
                }

                double ratioX = (double)40 / pixelWidth;
                double ratioY = (double)40 / pixelHeight;
                double ratio = Math.Max(ratioY, ratioY);

                var width = (int)(pixelWidth * ratio);
                var height = (int)(pixelHeight * ratio);

                var count = Math.Ceiling(296d / width);

                int rasterWidth;
                int rasterHeight;
                if (animation.Rotation is 90 or 270)
                {
                    rasterWidth = (int)(height * XamlRoot.RasterizationScale);
                    rasterHeight = (int)(width * XamlRoot.RasterizationScale);
                }
                {
                    rasterWidth = (int)(width * XamlRoot.RasterizationScale);
                    rasterHeight = (int)(height * XamlRoot.RasterizationScale);
                }

                var duration = TimeSpan.FromMilliseconds(animation.Duration);
                var maxLength = mask == ImageCropperMask.Ellipse
                    ? TimeSpan.FromSeconds(10)
                    : duration;

                TrimRange.SetOriginalDuration(_duration = duration, maxLength);
                TrimThumbnails.Children.Clear();

                if (mask != ImageCropperMask.Ellipse && media.EditState?.TrimStartTime != TimeSpan.Zero && media.EditState?.TrimStopTime != TimeSpan.Zero)
                {
                    TrimRange.Minimum = media.EditState.TrimStartTime.TotalSeconds / TrimRange.OriginalDuration.TotalSeconds;
                    TrimRange.Maximum = media.EditState.TrimStopTime.TotalSeconds / TrimRange.OriginalDuration.TotalSeconds;
                }

                for (int i = 0; i < count; i++)
                {
                    var bitmap = new WriteableBitmap(rasterWidth, rasterHeight);
                    var buffer = new PixelBuffer(bitmap);

                    await Task.Run(() =>
                    {
                        animation.SeekToMilliseconds((long)(animation.Duration / count * i), false);
                        animation.RenderSync(buffer, rasterWidth, rasterHeight, true, out _);
                    });

                    var image = new Border();
                    image.Height = height;
                    image.Width = width;
                    image.Background = new ImageBrush
                    {
                        ImageSource = bitmap,
                        AlignmentX = AlignmentX.Center,
                        AlignmentY = AlignmentY.Center,
                        Stretch = Stretch.Fill,
                        RelativeTransform = new CompositeTransform
                        {
                            CenterX = 0.5,
                            CenterY = 0.5,
                            Rotation = animation.Rotation
                        }
                    };

                    Grid.SetColumn(image, i);

                    TrimThumbnails.Children.Add(image);
                    TrimThumbnails.ColumnDefinitions.Add(1, i == count - 1 ? GridUnitType.Star : GridUnitType.Auto);
                }
            }
            catch
            {
                // People like to delete files while they're sending them
            }
        }

        private void Accept_Click(object sender, RoutedEventArgs e)
        {
            if (Cropper.IsCropEnabled)
            {
                var rect = Cropper.CropRectangle;

                TimeSpan trimStartTime = default;
                TimeSpan trimStopTime = default;

                if (TrimRange != null && (TrimRange.Minimum != 0 || TrimRange.Maximum != 1))
                {
                    trimStartTime = TimeSpan.FromMilliseconds(TrimRange.Minimum * TrimRange.OriginalDuration.TotalMilliseconds);
                    trimStopTime = TimeSpan.FromMilliseconds(TrimRange.Maximum * TrimRange.OriginalDuration.TotalMilliseconds);
                }

                _media.EditState = new ImageGeneration
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
            var rotation = ImageRotation.None;

            var proportions = RotateProportions(Cropper.Proportions);
            var rectangle = ImageHelper.RotateArea(Cropper.CropRectangle, 1, 1, 1);

            switch (_rotation)
            {
                case ImageRotation.None:
                    rotation = ImageRotation.Clockwise90Degrees;
                    break;
                case ImageRotation.Clockwise90Degrees:
                    rotation = ImageRotation.Clockwise180Degrees;
                    break;
                case ImageRotation.Clockwise180Degrees:
                    rotation = ImageRotation.Clockwise270Degrees;
                    break;
            }

            _rotation = rotation;
            await Cropper.SetSourceAsync(_file, rotation, _flip, proportions, rectangle);

            Rotate.IsChecked = _rotation != ImageRotation.None;
            Canvas.Invalidate();
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
            var rectangle = ImageHelper.FlipArea(Cropper.CropRectangle, 1, 1, ImageFlip.Horizontal);

            switch (rotation)
            {
                case ImageRotation.Clockwise90Degrees:
                case ImageRotation.Clockwise270Degrees:
                    switch (flip)
                    {
                        case ImageFlip.None:
                            flip = ImageFlip.Vertical;
                            break;
                        case ImageFlip.Vertical:
                            flip = ImageFlip.None;
                            break;
                        case ImageFlip.Horizontal:
                            flip = ImageFlip.None;
                            rotation = rotation == ImageRotation.Clockwise90Degrees
                                ? ImageRotation.Clockwise270Degrees
                                : ImageRotation.Clockwise90Degrees;
                            break;
                    }
                    break;
                case ImageRotation.None:
                case ImageRotation.Clockwise180Degrees:
                    switch (flip)
                    {
                        case ImageFlip.None:
                            flip = ImageFlip.Horizontal;
                            break;
                        case ImageFlip.Horizontal:
                            flip = ImageFlip.None;
                            break;
                        case ImageFlip.Vertical:
                            flip = ImageFlip.None;
                            rotation = rotation == ImageRotation.None
                                ? ImageRotation.Clockwise180Degrees
                                : ImageRotation.None;
                            break;
                    }
                    break;
            }

            _flip = flip;
            _rotation = rotation;
            await Cropper.SetSourceAsync(_file, _rotation, flip, proportions, rectangle);

            //Transform.ScaleX = _flip == ImageFlip.Horizontal ? -1 : 1;
            //Transform.ScaleY = _flip == ImageFlip.Vertical ? -1 : 1;

            Flip.IsChecked = _flip != ImageFlip.None;
            Canvas.Invalidate();
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
            Undo?.IsEnabled = Canvas.CanUndo;

            Redo?.IsEnabled = Canvas.CanRedo;
        }

        private async void MediaPlayer_PositionChanged(Windows.Media.Playback.MediaPlaybackSession sender, object args)
        {
            await TrimRange.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                if (TrimRange.IsChanging)
                {
                    return;
                }

                if (sender.Position.TotalMilliseconds >= TrimRange.Maximum * _duration.TotalMilliseconds)
                {
                    sender.Position = TimeSpan.FromMilliseconds(_duration.TotalMilliseconds * TrimRange.Minimum);
                    return;
                }

                TrimRange.Value = sender.Position.TotalMilliseconds / _duration.TotalMilliseconds;
            });
        }

        private void TrimRange_MinimumChanged(object sender, double e)
        {
            if (Media.MediaPlayer.PlaybackSession.PlaybackState == Windows.Media.Playback.MediaPlaybackState.Playing)
            {
                _resume = true;
                Media.MediaPlayer.Pause();
            }

            Media.MediaPlayer.PlaybackSession.Position =
                TimeSpan.FromMilliseconds(_duration.TotalMilliseconds * e);
        }

        private void TrimRange_MaximumChanged(object sender, double e)
        {
            Media.MediaPlayer.PlaybackSession.Position =
                TimeSpan.FromMilliseconds(_duration.TotalMilliseconds * e);

            if (_resume)
            {
                _resume = false;
                Media.MediaPlayer.Play();
            }
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
