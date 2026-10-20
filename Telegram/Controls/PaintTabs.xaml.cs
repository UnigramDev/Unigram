//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Telegram.Controls.Drawers;
using Telegram.Controls.Media;
using Telegram.Entities;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.ViewModels.Drawers;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;

namespace Telegram.Controls
{
    /// <summary>
    /// The tabs of a media editor's draw mode: drawing, stickers, text and shapes. A tab other
    /// than Draw is lit while an object of its kind is selected; the tools for it are the
    /// <see cref="PaintToolbar"/>'s.
    /// </summary>
    public sealed partial class PaintTabs : StackPanel
    {
        private PaintToolbar _toolbar;
        private MediaEditorSurface _surface;
        private IClientService _clientService;

        private bool _updatingShapeFill;

        public PaintTabs()
        {
            InitializeComponent();
        }

        public void Attach(PaintToolbar toolbar, MediaEditorSurface surface, IClientService clientService)
        {
            _toolbar = toolbar;
            _surface = surface;
            _clientService = clientService;
        }

        public void Deactivate()
        {
            StickerPicker?.Deactivate();
            EmojiPicker?.Deactivate();
            GifPicker?.Deactivate();
        }

        public void UpdateSelection(PaintEntity selected)
        {
            DrawTab.IsChecked = selected == null;
            StickerTab.IsChecked = selected != null && PaintEntityRenderer.IsSticker(selected);
            TextTab.IsChecked = selected?.Type == PaintEntityType.Text;
            ShapesTab.IsChecked = selected?.Type == PaintEntityType.Shape;
        }

        private void DrawTab_Click(object sender, RoutedEventArgs e)
        {
            _toolbar.SelectDrawing();
        }

        private void TextTab_Click(object sender, RoutedEventArgs e)
        {
            if (_surface.Entities.Selected is not { Type: PaintEntityType.Text })
            {
                _toolbar.AddText();
            }

            // The tabs only show the selection, which placing the text may not have made.
            _toolbar.UpdateTools();
        }

        // The picker opens on every press, so another sticker can be added with one already
        // selected; the tab itself only lights up once one is.
        private void StickerTab_Click(object sender, RoutedEventArgs e)
        {
            ShowStickerPicker();
            _toolbar.UpdateTools();
        }

        private void ShowStickerPicker()
        {
            if (_clientService != null)
            {
                FlyoutBase.ShowAttachedFlyout(StickerTab);
            }
        }

        // The first tab is chosen only once the flyout is open: before that its content is not in
        // the tree, and FindName cannot load a drawer into it.
        private void StickerFlyout_Opened(object sender, object e)
        {
            if (PickerTabs.SelectedIndex < 0)
            {
                PickerTabs.SelectedIndex = 0;
            }
        }

        // Each drawer is made the first time its tab is opened.
        private void PickerTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var index = PickerTabs.SelectedIndex;
            if (_clientService == null || index < 0)
            {
                return;
            }

            if (index == 0 && StickerPicker == null)
            {
                FindName(nameof(StickerPicker));

                StickerPicker.DataContext = StickerDrawerViewModel.Create(_clientService.Session);
                StickerPicker.Activate(null, EmojiSearchType.Default);
            }
            else if (index == 1 && EmojiPicker == null)
            {
                // The chat photo mode lists custom emoji only.
                FindName(nameof(EmojiPicker));

                EmojiPicker.DataContext = EmojiDrawerViewModel.Create(_clientService.Session, EmojiDrawerMode.ChatPhoto);
                EmojiPicker.Activate(null);
            }
            else if (index == 2 && GifPicker == null)
            {
                FindName(nameof(GifPicker));

                GifPicker.DataContext = AnimationDrawerViewModel.Create(_clientService.Session);
                GifPicker.Activate(null);
            }

            StickerPicker?.Visibility = index == 0 ? Visibility.Visible : Visibility.Collapsed;
            EmojiPicker?.Visibility = index == 1 ? Visibility.Visible : Visibility.Collapsed;
            GifPicker?.Visibility = index == 2 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void StickerPicker_ItemClick(object sender, StickerDrawerItemClickEventArgs e)
        {
            AddSticker(e.Sticker);
        }

        private void EmojiPicker_ItemClick(object sender, EmojiDrawerItemClickEventArgs e)
        {
            if (e.ClickedItem is StickerViewModel sticker)
            {
                AddSticker(sticker);
            }
        }

        private void GifPicker_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is Animation animation)
            {
                AddSticker(PaintEntityType.Animation, animation.AnimationValue, animation.Width, animation.Height, false);
            }
        }

        private void AddSticker(Sticker sticker)
        {
            if (sticker?.StickerValue is File file)
            {
                // A custom emoji that takes the text's colour is shown white.
                var tinted = sticker.FullType is StickerFullTypeCustomEmoji { NeedsRepainting: true };
                AddSticker(PaintEntityType.Sticker, file, sticker.Width, sticker.Height, tinted);
            }
        }

        private void AddSticker(PaintEntityType type, File file, int width, int height, bool tinted)
        {
            StickerFlyout.Hide();

            if (file != null)
            {
                _surface.Entities.AddSticker(type, file, width, height, tinted);
            }
        }

        // Like the sticker tab, it opens its list on every press; it lights up while a shape is
        // selected.
        private void ShapesTab_Click(object sender, RoutedEventArgs e)
        {
            FlyoutBase.ShowAttachedFlyout(ShapesTab);
            _toolbar.UpdateTools();
        }

        private void ShapesFlyout_Opening(object sender, object e)
        {
            var filled = _surface.Entities.Selected is { Type: PaintEntityType.Shape } shape
                ? shape.ShapeFilled
                : AppSettings.Pencil.ShapeFilled;

            _updatingShapeFill = true;
            ShapeFilled.IsChecked = filled;
            _updatingShapeFill = false;

            UpdateShapeIcons(filled);
        }

        private void ShapeItem_Click(object sender, RoutedEventArgs e)
        {
            var kind = sender == RectangleItem ? PaintShapeKind.Rectangle
                : sender == StarItem ? PaintShapeKind.Star
                : sender == BubbleItem ? PaintShapeKind.Bubble
                : sender == ArrowItem ? PaintShapeKind.Arrow
                : PaintShapeKind.Circle;

            var settings = AppSettings.Pencil;
            _surface.Entities.AddShape(kind, settings.ShapeFilled, settings.GetColor(PencilTool.Shape), (float)settings.GetWeight(PencilTool.Shape));
        }

        // The choice is kept for new shapes, and given to the selected one too.
        private void ShapeFilled_Changed(object sender, RoutedEventArgs e)
        {
            if (_updatingShapeFill)
            {
                return;
            }

            var filled = ShapeFilled.IsChecked == true;

            AppSettings.Pencil.ShapeFilled = filled;
            _surface.Entities.SetSelectedShapeFilled(filled);

            UpdateShapeIcons(filled);
        }

        private void UpdateShapeIcons(bool filled)
        {
            CircleIcon.Glyph = filled ? Icons.CircleFilled : Icons.Circle;
            RectangleIcon.Glyph = filled ? Icons.SquareFilled : Icons.Square;
            StarIcon.Glyph = filled ? Icons.StarFilled : Icons.Star;
            BubbleIcon.Glyph = filled ? Icons.ChatEmptyFilled : Icons.ChatEmpty;
        }
    }
}
