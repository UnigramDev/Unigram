//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Microsoft.Graphics.Canvas.Geometry;
using Telegram.Common;
using Telegram.Controls.Drawers;
using Telegram.Controls.Media;
using Telegram.Controls.Stories.Widgets;
using Telegram.Entities;
using Telegram.Navigation;
using Telegram.Services;
using Telegram.Streams;
using Telegram.Td.Api;
using Telegram.ViewModels.Drawers;
using Telegram.Views.Stories.Popups;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Media.Imaging;

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

        private bool _weatherLoading;
        private string _weatherEmoji;

        // The reaction a new reaction widget starts with.
        private const string DefaultReaction = "❤";

        public PaintTabs()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Offers the story widgets above the stickers.
        /// </summary>
        public bool ShowsWidgets { get; set; }

        public void Attach(PaintToolbar toolbar, MediaEditorSurface surface, IClientService clientService)
        {
            _toolbar = toolbar;
            _surface = surface;
            _clientService = clientService;

            _surface.Entities.LinkEditRequested += Entities_LinkEditRequested;
        }

        public void Deactivate()
        {
            StickerPicker?.Deactivate();
            EmojiPicker?.Deactivate();
            GifPicker?.Deactivate();

            if (_surface != null)
            {
                _surface.Entities.LinkEditRequested -= Entities_LinkEditRequested;
            }
        }

        public void UpdateSelection(PaintEntity selected)
        {
            DrawTab.IsChecked = selected == null;
            StickerTab.IsChecked = selected != null && (PaintEntityRenderer.IsSticker(selected) || PaintEntityRenderer.IsWidget(selected));
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

            // A story takes one weather.
            if (WeatherWidget != null)
            {
                WeatherWidget.Visibility = _surface.Entities.HasAny(PaintEntityType.Weather)
                    ? Visibility.Collapsed
                    : Visibility.Visible;
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

                if (ShowsWidgets)
                {
                    WidgetsPanel.Visibility = Visibility.Visible;

                    LinkWidgetText.Text = Strings.StoryWidgetLink.ToUpper();
                    LinkWidgetLock.Visibility = _clientService.IsPremium
                        ? Visibility.Collapsed
                        : Visibility.Visible;

                    AutomationProperties.SetName(LinkWidget, Strings.StoryWidgetLink);
                    WeatherWidget.Visibility = _surface.Entities.HasAny(PaintEntityType.Weather)
                        ? Visibility.Collapsed
                        : Visibility.Visible;

                    // What the other clients show until the weather is known: 24 degrees Celsius,
                    // or 72 Fahrenheit. It also sizes the skeleton while the weather loads.
                    ShowWeather("\U0001F324", StoryWeatherWidget.IsFahrenheit ? 22.2 : 24);
                    PreloadWeather();

                    ReactionWidgetPreview.Child = new AnimatedImage
                    {
                        AutoPlay = true,
                        LoopCount = 1,
                        FrameSize = new Size(24, 24),
                        DecodeFrameType = DecodePixelType.Logical,
                        Source = new ReactionFileSource(_clientService, new ReactionTypeEmoji(DefaultReaction))
                        {
                            UseCenterAnimation = true,
                            IsAnimated = false
                        }
                    };
                }
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

        // Loaded at once where it needs no asking, and shown loading meanwhile rather than as the
        // placeholder.
        private async void PreloadWeather()
        {
            var service = _clientService.Session.Resolve<ILocationService>();
            if (!_clientService.Options.CanPreloadWeather || !service.HasLocationAccess)
            {
                return;
            }

            SetWeatherLoading(true);

            var weather = await service.GetCurrentWeatherAsync(null);

            SetWeatherLoading(false);

            if (weather != null)
            {
                ShowWeather(weather.Emoji, weather.Temperature);
            }
        }

        private void ShowWeather(string emoji, double temperature)
        {
            if (_weatherEmoji != emoji)
            {
                _weatherEmoji = emoji;
                WeatherWidgetEmoji.Source = new AnimatedEmojiFileSource(_clientService, emoji);
            }

            WeatherWidgetText.Text = StoryWeatherWidget.FormatTemperature(temperature);
        }

        // The text only turns transparent, so the skeleton covers the place it keeps. Hiding the
        // panel instead would hide the skeleton with it.
        private void SetWeatherLoading(bool loading)
        {
            _weatherLoading = loading;

            WeatherWidget.IsEnabled = !loading;
            WeatherWidgetEmoji.Opacity = loading ? 0 : 1;
            WeatherWidgetText.Opacity = loading ? 0 : 1;

            if (loading)
            {
                UpdateWeatherSkeleton();
            }
            else
            {
                VisualUtilities.HideSkeleton(WeatherWidgetContent);
            }
        }

        // The header may not be laid out yet when the weather starts loading.
        private void WeatherWidgetContent_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (_weatherLoading)
            {
                UpdateWeatherSkeleton();
            }
        }

        private void UpdateWeatherSkeleton()
        {
            var size = WeatherWidgetContent.ActualSize;
            if (size.X > 0 && size.Y > 0)
            {
                VisualUtilities.SetSkeleton(WeatherWidgetContent, size, CanvasGeometry.CreateRoundedRectangle(null, 0, 0, size.X, size.Y, 4, 4));
            }
        }

        // The flyout stays open while the location and the weather come in, which can take a few
        // seconds.
        private async void WeatherWidget_Click(object sender, RoutedEventArgs e)
        {
            SetWeatherLoading(true);

            var weather = await _clientService.Session.Resolve<ILocationService>().GetCurrentWeatherAsync(WindowContext.GetNavigationService(this));

            SetWeatherLoading(false);

            if (weather == null)
            {
                return;
            }

            ShowWeather(weather.Emoji, weather.Temperature);
            StickerFlyout.Hide();

            if (!_surface.Entities.HasAny(PaintEntityType.Weather))
            {
                _surface.Entities.AddWeather(weather);
            }
        }

        private async void LinkWidget_Click(object sender, RoutedEventArgs e)
        {
            var navigationService = WindowContext.GetNavigationService(this);

            if (!_clientService.IsPremium)
            {
                ToastPopup.ShowFeaturePromo(navigationService, Strings.StoryLinkPremium, new PremiumFeatureUpgradedStories());
                return;
            }
            else if (!_surface.Entities.CanAddLink())
            {
                return;
            }

            StickerFlyout.Hide();

            var popup = new StoryLinkPopup(_clientService, null);

            var confirm = await navigationService.ShowPopupAsync(popup, requestedTheme: ElementTheme.Dark);
            if (confirm == ContentDialogResult.Primary)
            {
                // Its first fill, on the other clients too.
                var link = new PaintEntity
                {
                    WidgetStyle = PaintWidgetStyle.White
                };

                popup.ApplyTo(link);
                _surface.Entities.AddLink(link);
            }
        }

        private async void Entities_LinkEditRequested(object sender, PaintEntityEventArgs e)
        {
            var popup = new StoryLinkPopup(_clientService, e.Entity);

            var confirm = await WindowContext.GetNavigationService(this).ShowPopupAsync(popup, requestedTheme: ElementTheme.Dark);
            if (confirm == ContentDialogResult.Primary)
            {
                popup.ApplyTo(e.Entity);
                _surface.Entities.RefreshLink(e.Entity);
            }
        }

        private void ReactionWidget_Click(object sender, RoutedEventArgs e)
        {
            StickerFlyout.Hide();
            _surface.Entities.AddReaction(new ReactionTypeEmoji(DefaultReaction));
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
