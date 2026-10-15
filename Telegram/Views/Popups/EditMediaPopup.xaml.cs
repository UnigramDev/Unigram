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
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Controls.Media;
using Telegram.Converters;
using Telegram.Entities;
using Telegram.Native;
using Telegram.Native.Media;
using Telegram.Navigation;
using Telegram.Services;
using Telegram.Services.Settings;
using Telegram.Streams;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.UI;
using Windows.UI.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;

namespace Telegram.Views.Popups
{
    public sealed partial class EditMediaPopup : OverlayWindow
    {
        public StorageMedia ResultMedia { get; private set; }

        private readonly ImageCropperMask _mask;

        private readonly StorageFile _file;
        private readonly StorageMedia _media;

        private AsyncMediaPlayer _player;

        private TimeSpan _duration;
        private bool _resume;

        // Not remembered between edits: a session that ended on the eraser would open unable to draw.
        private PencilTool _tool;

        private bool _weightDragging;

        // Strokes and placed objects undo as one history, newest last. An entry is the object its
        // undo takes away, or null for a stroke, which the canvas keeps the list of.
        private readonly List<PaintEntity> _undo = new();
        private readonly List<PaintEntity> _redo = new();

        public EditMediaPopup(XamlRoot xamlRoot, StorageMedia media, ImageCropperMask mask = ImageCropperMask.Rectangle)
            : base(xamlRoot)
        {
            InitializeComponent();

            Canvas.Strokes = media.EditState.Strokes;
            Entities.Items = media.EditState.Entities;

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

            Proportions.IsChecked = media.EditState.Proportions != BitmapProportions.Custom;

            if (mask == ImageCropperMask.Ellipse)
            {
                RotationWheel.Visibility = Visibility.Collapsed;
            }

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;

            if (media.File.HasExtension(Constants.VideoTypes))
            {
                FindName(nameof(TrimToolbar));
                TrimToolbar.Visibility = Visibility.Visible;
                BasicToolbar.Visibility = Visibility.Collapsed;

                // MediaTranscoder can only crop a video to a rectangle: no free angle until the
                // video pipeline replaces it.
                CropToolbar.Visibility = Visibility.Collapsed;
                Cropper.MinimumAreaSize = 64;

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
                await Cropper.SetSourceAsync(_media.File, _media.EditState.Rotation, _media.EditState.Flip, _media.EditState.Proportions, _media.EditState.Rectangle, _media.EditState.Angle);
            }

            if (_media.EditState.Strokes is { } strokes)
            {
                foreach (var stroke in strokes)
                {
                    if (stroke.Tool == PencilTool.Blur)
                    {
                        LoadBlurSource();
                        break;
                    }
                }
            }

            ConnectedAnimationServiceEx.TryStart("EditMediaPopup", Cropper);
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            if (_player != null)
            {
                _player.PositionChanged -= Player_PositionChanged;
                _player.EndReached -= Player_EndReached;

                _player.Close();
                _player = null;
            }
        }

        private void OnPreviewKeyDown(object sender, KeyRoutedEventArgs args)
        {
            if (TrimToolbar?.Visibility == Visibility.Visible)
            {
                if (args.Key == VirtualKey.Space /*&& args.Modifiers == VirtualKeyModifiers.None*/)
                {
                    _player?.Toggle();
                    args.Handled = true;
                }
            }
        }

        private void OnProcessKeyboardAccelerators(UIElement sender, ProcessKeyboardAcceleratorEventArgs args)
        {
            // Keys typed into a text object belong to the text.
            if (Entities.IsEditorFocused)
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
                if (args.Key == VirtualKey.M && args.Modifiers == VirtualKeyModifiers.None)
                {
                    if (_player != null)
                    {
                        _player.Mute = !_player.Mute;
                    }

                    args.Handled = true;
                }
            }
            else if (DrawToolbar?.Visibility == Visibility.Visible)
            {
                if (args.Key == VirtualKey.Z && args.Modifiers == VirtualKeyModifiers.Control && _undo.Count > 0)
                {
                    UndoStep();
                    args.Handled = true;
                }
                else if (((args.Key == VirtualKey.Y && args.Modifiers == VirtualKeyModifiers.Control)
                    || (args.Key == VirtualKey.Z && args.Modifiers == (VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift))) && _redo.Count > 0)
                {
                    RedoStep();
                    args.Handled = true;
                }
                else if (args.Key == VirtualKey.D && args.Modifiers == VirtualKeyModifiers.Control)
                {
                    if (Entities.Selected != null)
                    {
                        Entities.DuplicateSelected();
                    }
                    else
                    {
                        SelectTool(PencilTool.Pen);
                    }

                    args.Handled = true;
                }
                else if ((args.Key is VirtualKey.Delete or VirtualKey.Back) && args.Modifiers == VirtualKeyModifiers.None && Entities.Selected != null)
                {
                    Entities.DeleteSelected();
                    args.Handled = true;
                }
                else if (args.Key == VirtualKey.E && args.Modifiers == VirtualKeyModifiers.Control)
                {
                    SelectTool(PencilTool.Eraser);
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
            // Escape puts away whatever is in hand, the eyedropper or a text, before it leaves the
            // editor.
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
                Cancel_Click(null, null);
            }

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

        private void InitializePlayer(StorageMedia media, ImageCropperMask mask)
        {
            // The read callback opens the path itself, so a file that does not have one -- a
            // virtual or shared item -- cannot be played. The cropper still shows its frame.
            if (string.IsNullOrEmpty(media.File.Path))
            {
                return;
            }

            FindName(nameof(Video));

            var options = new AsyncMediaPlayerOptions
            {
                CreateSwapChain = true,
                Mute = mask == ImageCropperMask.Ellipse,
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

            GenerationSources.AddOrReplace("EditMediaPopup", media.File);

            _player.PositionChanged += Player_PositionChanged;
            _player.EndReached += Player_EndReached;

            _player.Context.Attach(Video, true);
            _player.Play(new LocalFileSource(media.File.Path, (long)media.Size));
        }

        private async void InitializeVideo(StorageMedia media, ImageCropperMask mask)
        {
            try
            {
                InitializePlayer(media, mask);

                using var stream = await media.File.OpenReadAsync();
                using var animation = await Task.Run(() => VideoAnimation.LoadFromFile(new VideoAnimationStreamSource(stream), false, false, false));

                var pixelWidth = animation.PixelWidth;
                var pixelHeight = animation.PixelHeight;

                if (animation.Rotation is 90 or 270)
                {
                    pixelWidth = animation.PixelHeight;
                    pixelHeight = animation.PixelWidth;
                }

                // A single row of frames 40 tall, each as wide as its aspect makes it, so the
                // height is what the scale is pinned to. Fitting inside 40 square instead would
                // leave a portrait video too short to fill the strip.
                double ratio = 40d / pixelHeight;

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
                    var buffer = BufferSurface.Create((uint)(rasterWidth * rasterHeight * 4));

                    var rendered = await Task.Run(() =>
                    {
                        animation.SeekToMilliseconds((long)(animation.Duration / count * i), false);
                        return animation.RenderSync(buffer, rasterWidth, rasterHeight, true, out _);
                    });

                    var bitmap = new WriteableBitmap(rasterWidth, rasterHeight);

                    // Otherwise the buffer holds uninitialised memory.
                    if (rendered)
                    {
                        BufferSurface.Copy(buffer, bitmap.PixelBuffer);
                    }

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
                    Entities = Entities.Items,
                    Flip = Cropper.Flip,
                    Rotation = Cropper.Orientation,
                    Angle = Cropper.Angle,
                    TrimStartTime = trimStartTime,
                    TrimStopTime = trimStopTime,
                };

                Hide(ContentDialogResult.Primary);
            }
            else
            {
                Canvas.SaveState();
                Entities.SaveState();

                Cropper.IsCropEnabled = true;
                Canvas.IsEnabled = false;
                Entities.IsHitTestVisible = false;

                CropToolbar.Visibility = Visibility.Visible;
                BasicToolbar.Visibility = Visibility.Visible;
                PaintTabs.Visibility = Visibility.Collapsed;
                DrawToolbar.Visibility = Visibility.Collapsed;
                WeightSlider.Visibility = Visibility.Collapsed;
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
                Entities.RestoreState();

                Cropper.IsCropEnabled = true;
                Canvas.IsEnabled = false;
                Entities.IsHitTestVisible = false;

                CropToolbar.Visibility = Visibility.Visible;
                BasicToolbar.Visibility = Visibility.Visible;
                PaintTabs.Visibility = Visibility.Collapsed;
                DrawToolbar.Visibility = Visibility.Collapsed;
                WeightSlider.Visibility = Visibility.Collapsed;

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

        private void Rotate_Click(object sender, RoutedEventArgs e)
        {
            Cropper.RotateCounterclockwise();
        }

        private void Flip_Click(object sender, RoutedEventArgs e)
        {
            Cropper.Mirror();
        }

        private void ResetCrop_Click(object sender, RoutedEventArgs e)
        {
            Cropper.Reset();
        }

        private void Cropper_ViewChanged(object sender, EventArgs e)
        {
            Entities.SetView(Cropper.ContentTransform, Cropper.ViewArea, Cropper.ViewportSize);
        }

        private void Cropper_Changed(object sender, EventArgs e)
        {
            Rotate.IsChecked = Cropper.Orientation != ImageRotation.None;
            Flip.IsChecked = Cropper.Flip != ImageFlip.None;
            Proportions.IsChecked = Cropper.Proportions != BitmapProportions.Custom;

            RotationWheel.Angle = Cropper.Angle;
            ResetCrop.Visibility = Cropper.IsDefault
                ? Visibility.Collapsed
                : Visibility.Visible;
        }

        private void RotationWheel_RotationStarted(object sender, EventArgs e)
        {
            Cropper.BeginRotation();
        }

        private void RotationWheel_RotationChanged(object sender, EventArgs e)
        {
            Cropper.SetAngle(RotationWheel.Angle);
        }

        private void RotationWheel_RotationCompleted(object sender, EventArgs e)
        {
            Cropper.EndRotation();
        }

        private void Draw_Click(object sender, RoutedEventArgs e)
        {
            Cropper.IsCropEnabled = false;
            Canvas.IsEnabled = true;
            Entities.IsHitTestVisible = true;

            CropToolbar.Visibility = Visibility.Collapsed;
            BasicToolbar.Visibility = Visibility.Collapsed;

            if (DrawToolbar == null)
            {
                FindName(nameof(DrawToolbar));
                FindName(nameof(PaintTabs));
            }

            if (WeightSlider == null)
            {
                FindName(nameof(WeightSlider));
                FindName(nameof(WeightPreview));
            }

            PaintTabs.Visibility = Visibility.Visible;
            DrawToolbar.Visibility = Visibility.Visible;
            WeightSlider.Visibility = Visibility.Visible;

            Canvas.EraserThickness = PencilTools.GetThickness(PencilTool.Eraser, AppSettings.Pencil.GetWeight(PencilTool.Eraser));
            SelectTool(_tool);
            LoadBlurSource();

            ResetHistory();
        }

        private void Tool_Click(object sender, RoutedEventArgs e)
        {
            var tool = sender == ArrowTool ? PencilTool.Arrow
                : sender == MarkerTool ? PencilTool.Marker
                : sender == NeonTool ? PencilTool.Neon
                : sender == BlurTool ? PencilTool.Blur
                : sender == EraserTool ? PencilTool.Eraser
                : PencilTool.Pen;

            SelectTool(tool);
        }

        private void SelectTool(PencilTool tool)
        {
            _tool = tool;
            Canvas.Tool = tool;

            // A brush puts a selected object away; the toolbar then follows the selection.
            if (Entities.Selected != null)
            {
                Entities.Deselect();
            }
            else
            {
                UpdateTools();
            }
        }

        // What the colour and size controls edit: the selected text, or else the brush.
        private PencilTool Slot => Entities.Selected != null ? PencilTool.Text : _tool;

        private void UpdateTools()
        {
            var slot = Slot;

            // The tools sit in equal columns from the first one on. The highlight stays on the brush
            // while the text tab hides it, so it is in place on the way back.
            ToolSelection.Translation = new Vector3((PenTool.ActualSize.X - 4) * (int)_tool, 0, 0);

            PenTool.IsChecked = slot == PencilTool.Pen;
            ArrowTool.IsChecked = slot == PencilTool.Arrow;
            MarkerTool.IsChecked = slot == PencilTool.Marker;
            NeonTool.IsChecked = slot == PencilTool.Neon;
            BlurTool.IsChecked = slot == PencilTool.Blur;
            EraserTool.IsChecked = slot == PencilTool.Eraser;
            // The text tab is open exactly while a text is selected: opening it places one if
            // none is, and putting the text away goes back to drawing.
            var text = slot == PencilTool.Text;

            DrawTab.IsChecked = !text;
            TextTab.IsChecked = text;

            // Hidden rather than collapsed, so the bar keeps its width and the highlight its place.
            ShowBrush(ToolSelection, !text);
            ShowBrush(PenTool, !text);
            ShowBrush(ArrowTool, !text);
            ShowBrush(MarkerTool, !text);
            ShowBrush(NeonTool, !text);
            ShowBrush(BlurTool, !text);
            ShowBrush(EraserTool, !text);

            TextOptions.Visibility = text
                ? Visibility.Visible
                : Visibility.Collapsed;

            if (Entities.Selected is PaintEntity entity)
            {
                WeightSlider.SetRange(PencilTools.GetMinimumWeight(PencilTool.Text), PencilTools.GetMaximumWeight(PencilTool.Text), entity.TextSize);

                ColorButton.IsEnabled = true;
                ColorSwatchPanel.Opacity = 1;
                ColorSwatch.Fill = new SolidColorBrush(entity.Color);

                UpdateTextOptions(entity);
                return;
            }

            var weight = AppSettings.Pencil.GetWeight(slot);
            WeightSlider.SetRange(PencilTools.GetMinimumWeight(slot), PencilTools.GetMaximumWeight(slot), weight);
            Canvas.StrokeThickness = PencilTools.GetThickness(slot, WeightSlider.Value);

            ColorButton.IsEnabled = PencilTools.HasColor(slot);
            ColorSwatchPanel.Opacity = ColorButton.IsEnabled ? 1 : 0.4;
            SetColor(PencilTools.HasColor(slot) ? AppSettings.Pencil.GetColor(slot) : Colors.Transparent);
        }

        private void SetColor(Color color)
        {
            if (Entities.Selected != null)
            {
                Entities.SetSelectedColor(color);
            }
            else
            {
                Canvas.Stroke = color;
            }

            ColorSwatch.Fill = new SolidColorBrush(color);
        }

        private static void ShowBrush(UIElement brush, bool show)
        {
            brush.Opacity = show ? 1 : 0;
            brush.IsHitTestVisible = show;
        }

        private void UpdateTextOptions(PaintEntity entity)
        {
            AlignButton.Glyph = entity.Alignment switch
            {
                PaintTextAlignment.Center => Icons.TextAlignCenter,
                PaintTextAlignment.Right => Icons.TextAlignRight,
                _ => Icons.TextAlignLeft
            };

            FrameFilled.Visibility = entity.Frame == PaintTextFrame.Filled ? Visibility.Visible : Visibility.Collapsed;
            FrameTranslucent.Visibility = entity.Frame == PaintTextFrame.Translucent ? Visibility.Visible : Visibility.Collapsed;
            FrameSolid.Visibility = entity.Frame == PaintTextFrame.Solid ? Visibility.Visible : Visibility.Collapsed;
            FrameNone.Visibility = entity.Frame == PaintTextFrame.None ? Visibility.Visible : Visibility.Collapsed;

            SetFont(FontButton, entity.Font);
            FontButton.Glyph = GetFontName(entity.Font);
        }

        // Each typeface is named in itself, in the button and in the list.
        private static void SetFont(Control control, PaintTextFont font)
        {
            PaintEntityRenderer.GetFont(font, out string family, out FontWeight weight, out FontStyle style, out FontStretch stretch);

            control.FontFamily = new FontFamily(family);
            control.FontWeight = weight;
            control.FontStyle = style;
            control.FontStretch = stretch;
        }

        // The two named after the font itself take its Windows name.
        private static string GetFontName(PaintTextFont font) => font switch
        {
            PaintTextFont.Italic => Strings.PhotoEditorTypefaceItalic,
            PaintTextFont.Serif => Strings.PhotoEditorTypefaceSerif,
            PaintTextFont.Condensed => Strings.PhotoEditorTypefaceCondensed,
            PaintTextFont.Mono => Strings.PhotoEditorTypefaceMono,
            PaintTextFont.Display => "Georgia",
            _ => "Segoe UI"
        };

        // Each change also becomes the look of the next new text.
        private void AlignButton_Click(object sender, RoutedEventArgs e)
        {
            if (Entities.Selected is not PaintEntity entity)
            {
                return;
            }

            entity.Alignment = entity.Alignment switch
            {
                PaintTextAlignment.Left => PaintTextAlignment.Center,
                PaintTextAlignment.Center => PaintTextAlignment.Right,
                _ => PaintTextAlignment.Left
            };

            AppSettings.Pencil.TextAlignment = entity.Alignment;

            Entities.InvalidateSelected();
            UpdateTextOptions(entity);
        }

        private void FrameButton_Click(object sender, RoutedEventArgs e)
        {
            if (Entities.Selected is not PaintEntity entity)
            {
                return;
            }

            entity.Frame = (PaintTextFrame)(((int)entity.Frame + 1) % 4);

            AppSettings.Pencil.TextFrame = entity.Frame;

            Entities.InvalidateSelected();
            UpdateTextOptions(entity);
        }

        private void FontButton_Click(object sender, RoutedEventArgs e)
        {
            if (Entities.Selected is not PaintEntity entity)
            {
                return;
            }

            var flyout = new MenuFlyout();

            for (int i = 0; i <= (int)PaintTextFont.Display; i++)
            {
                var font = (PaintTextFont)i;
                var item = new ToggleMenuFlyoutItem
                {
                    Text = GetFontName(font),
                    Tag = font,
                    IsChecked = font == entity.Font,
                    FontSize = 15
                };

                SetFont(item, font);
                item.Click += FontItem_Click;

                flyout.Items.Add(item);
            }

            flyout.ShowAt(FontButton, FlyoutPlacementMode.Top);
        }

        private void FontItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not MenuFlyoutItemBase { Tag: PaintTextFont font } || Entities.Selected is not PaintEntity entity)
            {
                return;
            }

            entity.Font = font;

            AppSettings.Pencil.TextFont = font;

            Entities.InvalidateSelected();
            UpdateTextOptions(entity);
        }

        private void DrawTab_Click(object sender, RoutedEventArgs e)
        {
            SelectTool(_tool);
        }

        private void TextTab_Click(object sender, RoutedEventArgs e)
        {
            if (Entities.Selected == null)
            {
                AddText();
            }

            // The tabs only show the selection, which placing the text may not have made.
            UpdateTools();
        }

        private void AddText_Click(object sender, RoutedEventArgs e)
        {
            AddText();
        }

        private void AddText()
        {
            var settings = AppSettings.Pencil;
            Entities.AddText(settings.GetColor(PencilTool.Text), (float)settings.GetWeight(PencilTool.Text), settings.TextFrame, settings.TextAlignment, settings.TextFont);
        }

        private void Entities_SelectionChanged(object sender, EventArgs e)
        {
            if (DrawToolbar != null)
            {
                UpdateTools();
            }
        }

        private void ColorFlyout_Opening(object sender, object e)
        {
            var current = Entities.Selected?.Color ?? AppSettings.Pencil.GetColor(Slot);
            var colors = new List<SolidColorBrush>();

            foreach (var color in PencilSettings.PresetColors)
            {
                colors.Add(new SolidColorBrush(color));
            }

            foreach (var color in AppSettings.Pencil.GetRecentColors())
            {
                colors.Add(new SolidColorBrush(color));
            }

            ColorPalette.ItemsSource = colors;
            ColorPalette.SelectedIndex = colors.FindIndex(x => x.Color == current);

            PalettePanel.Visibility = Visibility.Visible;
            CustomColorPicker?.Visibility = Visibility.Collapsed;
        }

        // The picker's colour is shown as it changes and kept once the flyout closes, however it
        // closes.
        private void ColorFlyout_Closed(object sender, object e)
        {
            if (CustomColorPicker?.Visibility == Visibility.Visible)
            {
                AppSettings.Pencil.SetColor(Slot, CustomColorPicker.Color);
            }
        }

        private void MoreColors_Click(object sender, RoutedEventArgs e)
        {
            if (CustomColorPicker == null)
            {
                FindName(nameof(CustomColorPicker));
            }

            CustomColorPicker.Color = Entities.Selected?.Color ?? Canvas.Stroke;

            PalettePanel.Visibility = Visibility.Collapsed;
            CustomColorPicker.Visibility = Visibility.Visible;
        }

        private void CustomColorPicker_ColorChanged(object sender, EventArgs e)
        {
            SetColor(CustomColorPicker.Color);
        }

        private void CustomColorPicker_Completed(object sender, EventArgs e)
        {
            ColorFlyout.Hide();
        }

        private void CustomColorPicker_EyedropperRequested(object sender, EventArgs e)
        {
            ColorFlyout.Hide();

            // The objects would take the press meant for the eyedropper while one is selected.
            Entities.IsHitTestVisible = false;

            Canvas.LoupeScale = (float)(1 / Cropper.ZoomFactor);
            Canvas.BeginPick();
        }

        private void Canvas_ColorPicked(object sender, PencilColorPickedEventArgs e)
        {
            Entities.IsHitTestVisible = true;

            AppSettings.Pencil.SetColor(Slot, e.Color);
            SetColor(e.Color);
        }

        private void ColorPalette_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is SolidColorBrush brush)
            {
                AppSettings.Pencil.SetColor(Slot, brush.Color);
                SetColor(brush.Color);
            }

            ColorFlyout.Hide();
        }

        private void WeightSlider_ValueChanged(object sender, EventArgs e)
        {
            if (Entities.Selected != null)
            {
                Entities.SetSelectedTextSize((float)WeightSlider.Value);
            }
            else
            {
                Canvas.StrokeThickness = PencilTools.GetThickness(Canvas.Tool, WeightSlider.Value);

                if (Canvas.Tool == PencilTool.Eraser)
                {
                    Canvas.EraserThickness = Canvas.StrokeThickness;
                }

                if (_weightDragging)
                {
                    UpdateWeightPreview();
                }
            }

            if (!_weightDragging)
            {
                AppSettings.Pencil.SetWeight(Slot, WeightSlider.Value);
            }
        }

        // A text shows its own size as it changes, so only a brush gets the preview dot.
        private void WeightSlider_DragStarted(object sender, EventArgs e)
        {
            _weightDragging = true;

            if (Entities.Selected == null)
            {
                UpdateWeightPreview();
                WeightPreview.Visibility = Visibility.Visible;
            }
        }

        private void WeightSlider_DragCompleted(object sender, EventArgs e)
        {
            _weightDragging = false;

            WeightPreview.Visibility = Visibility.Collapsed;
            AppSettings.Pencil.SetWeight(Slot, WeightSlider.Value);
        }

        // As wide on screen as the stroke will be: the canvas is laid out in source pixels and
        // scaled by the cropper.
        private void UpdateWeightPreview()
        {
            var diameter = Canvas.StrokeThickness * Canvas.ActualWidth * Cropper.ZoomFactor;

            WeightPreview.Width = diameter;
            WeightPreview.Height = diameter;
        }

        // The blur tool paints a blurred copy of the photo, so the canvas needs the photo too.
        private async void LoadBlurSource()
        {
            if (Canvas.BlurSource != null || _file.HasExtension(Constants.VideoTypes))
            {
                return;
            }

            try
            {
                using var stream = await ImageHelper.OpenReadAsync(_file);

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

        private void Undo_Click(object sender, RoutedEventArgs e)
        {
            UndoStep();
        }

        private void Redo_Click(object sender, RoutedEventArgs e)
        {
            RedoStep();
        }

        private void UndoStep()
        {
            if (_undo.Count == 0)
            {
                return;
            }

            var entity = _undo[_undo.Count - 1];

            _undo.RemoveAt(_undo.Count - 1);
            _redo.Add(entity);

            if (entity == null)
            {
                Canvas.Undo();
            }
            else
            {
                Entities.Remove(entity);
            }

            InvalidateToolbar();
        }

        private void RedoStep()
        {
            if (_redo.Count == 0)
            {
                return;
            }

            var entity = _redo[_redo.Count - 1];

            _redo.RemoveAt(_redo.Count - 1);
            _undo.Add(entity);

            if (entity == null)
            {
                Canvas.Redo();
            }
            else
            {
                Entities.Restore(entity);
            }

            InvalidateToolbar();
        }

        // Each visit to draw mode starts from the strokes already there, which stay undoable; the
        // objects already there do not.
        private void ResetHistory()
        {
            _undo.Clear();
            _redo.Clear();

            Canvas.ClearRedo();

            for (int i = 0; i < Canvas.StrokeCount; i++)
            {
                _undo.Add(null);
            }

            InvalidateToolbar();
        }

        private void Canvas_StrokeAdded(object sender, EventArgs e)
        {
            _undo.Add(null);
            _redo.Clear();
        }

        private void Entities_EntityCreated(object sender, PaintEntityEventArgs e)
        {
            _undo.Add(e.Entity);
            _redo.Clear();

            Canvas.ClearRedo();
            InvalidateToolbar();
        }

        private void Entities_EntityDeleted(object sender, PaintEntityEventArgs e)
        {
            _undo.Remove(e.Entity);
            _redo.Remove(e.Entity);

            InvalidateToolbar();
        }

        private void Canvas_Gesture(object sender, PencilGestureEventArgs e)
        {
            Cropper.ZoomView(e.Center, e.Scale);
            Cropper.PanView(e.X, e.Y);
        }

        private void Canvas_StrokesChanged(object sender, EventArgs e)
        {
            InvalidateToolbar();
        }

        private void InvalidateToolbar()
        {
            Undo?.IsEnabled = _undo.Count > 0;

            Redo?.IsEnabled = _redo.Count > 0;
        }

        // AsyncMediaPlayer raises its events on the thread that created it, so this and
        // Player_EndReached are already on the UI thread.
        private void Player_PositionChanged(AsyncMediaPlayer sender, AsyncMediaPlayerPositionChangedEventArgs args)
        {
            // The duration is only known once the probe in InitializeVideo has run, and until
            // then every position is past the end of a zero length range.
            if (TrimRange.IsChanging || _duration == TimeSpan.Zero)
            {
                return;
            }

            if (args.Position >= TrimRange.Maximum * _duration.TotalSeconds)
            {
                sender.Position = _duration.TotalSeconds * TrimRange.Minimum;
                return;
            }

            TrimRange.Value = args.Position / _duration.TotalSeconds;
        }

        private void Player_EndReached(AsyncMediaPlayer sender, object args)
        {
            sender.Play();

            if (TrimRange.Minimum != 0)
            {
                sender.Position = _duration.TotalSeconds * TrimRange.Minimum;
            }
        }

        private void TrimRange_MinimumChanged(object sender, double e)
        {
            if (_player == null)
            {
                return;
            }

            if (_player.IsPlaying)
            {
                _resume = true;
                _player.Pause();
            }

            _player.Position = _duration.TotalSeconds * e;
        }

        private void TrimRange_MaximumChanged(object sender, double e)
        {
            if (_player == null)
            {
                return;
            }

            _player.Position = _duration.TotalSeconds * e;

            if (_resume)
            {
                _resume = false;
                _player.Play();
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
