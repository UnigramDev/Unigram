//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using System.Numerics;
using Telegram.Common;
using Telegram.Controls.Media;
using Telegram.Entities;
using Telegram.Services;
using Telegram.Services.Settings;
using Windows.UI;
using Windows.UI.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Media;

namespace Telegram.Controls
{
    /// <summary>
    /// The options row of a media editor's draw tabs: the brushes or the selected object's
    /// options, its colour and its size. The tabs are a <see cref="PaintTabs"/>, and the undo
    /// history is the surface's.
    /// </summary>
    public sealed partial class PaintToolbar : Grid
    {
        // What the row shows: each change of it passes the old row out as the new one comes in.
        private enum ToolsKind
        {
            Brushes,
            Text,
            None
        }

        private MediaEditorSurface _surface;
        private PaintTabs _tabs;

        // Not remembered between edits: a session that ended on the eraser would open unable to draw.
        private PencilTool _tool;

        private bool _weightDragging;

        private ToolsKind _kind;
        private int _tabIndex = -1;

        // While the crop tab is open: the row is away, and comes back on Resume.
        private bool _suspended = true;

        // The row is about to change to show _kind.
        private bool _swapping;

        private readonly ThemeShadowEx _shadow;

        public PaintToolbar()
        {
            InitializeComponent();

            _shadow = new ThemeShadowEx(Card, 16);
        }

        private PencilCanvas Canvas => _surface.Canvas;

        private PaintEntityLayer Entities => _surface.Entities;

        public void Attach(MediaEditorSurface surface, PaintTabs tabs)
        {
            _surface = surface;
            _tabs = tabs;

            _tabs.SetToolbar(this);

            Canvas.ColorPicked += Canvas_ColorPicked;
            Entities.SelectionChanged += Entities_SelectionChanged;

            if (!surface.CanBlur)
            {
                BlurTool.Visibility = Visibility.Collapsed;
            }
        }

        public void Detach()
        {
            if (_surface == null)
            {
                return;
            }

            Canvas.ColorPicked -= Canvas_ColorPicked;
            Entities.SelectionChanged -= Entities_SelectionChanged;

            _surface = null;
        }

        /// <summary>
        /// Brings the row back, sliding in from <paramref name="direction"/>, and lets drawing and
        /// placing start again.
        /// </summary>
        public void Resume(int direction)
        {
            _suspended = false;

            Canvas.IsEnabled = true;
            Entities.IsHitTestVisible = true;

            Canvas.EraserThickness = PencilTools.GetThickness(PencilTool.Eraser, AppSettings.Pencil.GetWeight(PencilTool.Eraser));
            SelectTool(_tool);

            if (_kind != ToolsKind.None)
            {
                MediaEditorTransitions.SlideIn(ToolsPanel, direction);
            }

            _surface.LoadBlurSource();
        }

        /// <summary>
        /// Slides the row away towards <paramref name="direction"/> and leaves what was drawn and
        /// placed alone, for another tab to take over until <see cref="Resume"/>.
        /// </summary>
        public void Suspend(int direction)
        {
            _suspended = true;
            _swapping = false;

            Entities.Deselect();

            Canvas.IsEnabled = false;
            Entities.IsHitTestVisible = false;

            MediaEditorTransitions.SlideOut(ToolsPanel, direction);
        }

        // Keeps what was drawn and placed so far.
        public void Save()
        {
            Canvas.SaveState();
            Entities.SaveState();
        }

        public bool ProcessKeyboardAccelerator(VirtualKey key, VirtualKeyModifiers modifiers)
        {
            if (key == VirtualKey.D && modifiers == VirtualKeyModifiers.Control)
            {
                if (Entities.Selected != null)
                {
                    Entities.DuplicateSelected();
                }
                else
                {
                    SelectTool(PencilTool.Pen);
                }
            }
            else if ((key is VirtualKey.Delete or VirtualKey.Back) && modifiers == VirtualKeyModifiers.None && Entities.Selected != null)
            {
                Entities.DeleteSelected();
            }
            else if (key == VirtualKey.E && modifiers == VirtualKeyModifiers.Control)
            {
                SelectTool(PencilTool.Eraser);
            }
            else
            {
                return false;
            }

            return true;
        }

        // The Draw tab: back to the brush last used.
        public void SelectDrawing()
        {
            SelectTool(_tool);
        }

        public void AddText()
        {
            var settings = AppSettings.Pencil;
            Entities.AddText(settings.GetColor(PencilTool.Text), (float)settings.GetWeight(PencilTool.Text), settings.TextFrame, settings.TextAlignment, settings.TextFont);
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
        private PencilTool Slot => Entities.Selected?.Type switch
        {
            PaintEntityType.Text => PencilTool.Text,
            PaintEntityType.Shape => PencilTool.Shape,
            _ => _tool
        };

        private PaintEntity SelectedText => Entities.Selected is { Type: PaintEntityType.Text } entity ? entity : null;

        // What the colour button edits, besides the brush.
        private PaintEntity SelectedColored => Entities.Selected is { Type: PaintEntityType.Text or PaintEntityType.Shape } entity ? entity : null;

        private static ToolsKind GetKind(PaintEntity selected)
        {
            if (selected == null || selected.Type == PaintEntityType.Shape)
            {
                return ToolsKind.Brushes;
            }
            else if (selected.Type == PaintEntityType.Text)
            {
                return ToolsKind.Text;
            }

            // A sticker or a widget has no tools: no colour or size, the rest is in its menu, and
            // another one comes from the tab.
            return ToolsKind.None;
        }

        public void UpdateTools()
        {
            var selected = Entities.Selected;
            _tabs.UpdateSelection(selected);

            var kind = GetKind(selected);
            var index = _tabs.SelectedIndex;
            var direction = _tabIndex < 0 ? 0 : Math.Sign(index - _tabIndex);

            _tabIndex = index;

            if (_suspended)
            {
                _kind = kind;
                return;
            }

            // Anything that changes before the swap takes its picture of the old row is applied
            // with it.
            if (kind != _kind && !_swapping)
            {
                _kind = kind;
                _swapping = true;

                MediaEditorTransitions.Swap(ToolsPanel, this, direction, ToolsPanel_Swap);
                return;
            }

            _kind = kind;

            if (!_swapping)
            {
                ApplyTools();
            }
        }

        private void ToolsPanel_Swap()
        {
            _swapping = false;

            if (!_suspended)
            {
                ApplyTools();
            }
        }

        private void ApplyTools()
        {
            var slot = Slot;

            // The highlight stays on the brush while the text tab hides it, so it is in place on
            // the way back. From where the button is, as a hidden brush takes no column.
            var button = GetToolButton(_tool);
            ToolSelection.Translation = new Vector3(button.ActualOffset.X - PenTool.ActualOffset.X, 0, 0);

            PenTool.IsChecked = slot == PencilTool.Pen;
            ArrowTool.IsChecked = slot == PencilTool.Arrow;
            MarkerTool.IsChecked = slot == PencilTool.Marker;
            NeonTool.IsChecked = slot == PencilTool.Neon;
            BlurTool.IsChecked = slot == PencilTool.Blur;
            EraserTool.IsChecked = slot == PencilTool.Eraser;
            // A tab other than Draw is open exactly while an object of its kind is selected:
            // opening one places an object if none is, and putting the object away goes back to
            // drawing.
            var selected = Entities.Selected;
            var text = selected?.Type == PaintEntityType.Text;
            var shape = selected?.Type == PaintEntityType.Shape;
            var sticker = _kind == ToolsKind.None;

            // Hidden rather than collapsed, so the bar keeps its width and the highlight its place.
            // A shape keeps the brushes, one press away from drawing again.
            var brushes = selected == null || shape;

            ShowBrush(ToolSelection, selected == null);
            ShowBrush(PenTool, brushes);
            ShowBrush(ArrowTool, brushes);
            ShowBrush(MarkerTool, brushes);
            ShowBrush(NeonTool, brushes);
            ShowBrush(BlurTool, brushes);
            ShowBrush(EraserTool, brushes);

            TextOptions.Visibility = text
                ? Visibility.Visible
                : Visibility.Collapsed;

            ToolsPanel.Visibility = sticker
                ? Visibility.Collapsed
                : Visibility.Visible;

            if (sticker)
            {
                return;
            }

            if (shape)
            {
                WeightSlider.SetRange(PencilTools.GetMinimumWeight(PencilTool.Shape), PencilTools.GetMaximumWeight(PencilTool.Shape), selected.ShapeWeight);

                ColorButton.IsEnabled = true;
                ColorSwatchPanel.Opacity = 1;
                ColorSwatch.Fill = new SolidColorBrush(selected.Color);
                return;
            }

            if (selected is PaintEntity entity)
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

        private UIElement GetToolButton(PencilTool tool) => tool switch
        {
            PencilTool.Arrow => ArrowTool,
            PencilTool.Marker => MarkerTool,
            PencilTool.Neon => NeonTool,
            PencilTool.Blur => BlurTool,
            PencilTool.Eraser => EraserTool,
            _ => PenTool
        };

        private void SetColor(Color color)
        {
            if (SelectedColored != null)
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
            if (SelectedText is not PaintEntity entity)
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
            if (SelectedText is not PaintEntity entity)
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
            if (SelectedText is not PaintEntity entity)
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

            flyout.ShowAt(FontButton, FlyoutPlacementMode.Bottom);
        }

        private void FontItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not MenuFlyoutItemBase { Tag: PaintTextFont font } || SelectedText is not PaintEntity entity)
            {
                return;
            }

            entity.Font = font;

            AppSettings.Pencil.TextFont = font;

            Entities.InvalidateSelected();
            UpdateTextOptions(entity);
        }

        private void AddText_Click(object sender, RoutedEventArgs e)
        {
            AddText();
        }

        private void Entities_SelectionChanged(object sender, EventArgs e)
        {
            UpdateTools();
        }

        private void ColorFlyout_Opening(object sender, object e)
        {
            var current = SelectedColored?.Color ?? AppSettings.Pencil.GetColor(Slot);
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

            CustomColorPicker.Color = SelectedColored?.Color ?? Canvas.Stroke;

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

            Canvas.LoupeScale = (float)(1 / _surface.LayerZoom);
            _surface.BeginPick();
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
            if (SelectedText != null)
            {
                Entities.SetSelectedTextSize((float)WeightSlider.Value);
            }
            else if (Entities.Selected?.Type == PaintEntityType.Shape)
            {
                Entities.SetSelectedShapeWeight((float)WeightSlider.Value);
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

        // As wide on screen as the stroke will be: the canvas is laid out in its layer's pixels and
        // scaled into the view.
        private void UpdateWeightPreview()
        {
            var diameter = Canvas.StrokeThickness * Canvas.ActualWidth * _surface.LayerZoom;

            WeightPreview.Width = diameter;
            WeightPreview.Height = diameter;
        }

    }
}
