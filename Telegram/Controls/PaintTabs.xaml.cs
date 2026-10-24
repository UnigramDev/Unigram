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
using Telegram.Views.Popups;
using Telegram.Views.Stories.Popups;
using System.Numerics;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media.Imaging;
using System;

namespace Telegram.Controls
{
    /// <summary>
    /// A media editor's tabs: crop, drawing, text, stickers and shapes. Crop is a mode of its
    /// own, which the host opens and closes on <see cref="CroppingChanged"/>. Of the others, a
    /// tab other than Draw is lit while an object of its kind is selected; their tools are the
    /// <see cref="PaintToolbar"/>'s.
    /// </summary>
    public sealed partial class PaintTabs : Grid
    {
        private const float IndicatorWidth = 16;

        private static readonly TimeSpan IndicatorDuration = TimeSpan.FromMilliseconds(400);

        private PaintToolbar _toolbar;
        private MediaEditorSurface _surface;
        private IClientService _clientService;

        // Where the indicator is, or -1 until it is first placed.
        private int _indicatorIndex = -1;

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

        /// <summary>
        /// The open tab, by its place: 0 is Crop.
        /// </summary>
        public int SelectedIndex { get; private set; }

        public bool IsCropping { get; private set; }

        /// <summary>
        /// Raised as the crop tab opens or another tab is chosen over it, before that tab acts.
        /// </summary>
        public event EventHandler CroppingChanged;

        public void Attach(MediaEditorSurface surface, IClientService clientService, bool cropping)
        {
            _surface = surface;
            _clientService = clientService;

            _surface.Entities.EditRequested += Entities_EditRequested;

            IsCropping = cropping;
            UpdateSelection(null);
        }

        // The toolbar is made the first time a tab other than Crop opens.
        internal void SetToolbar(PaintToolbar toolbar)
        {
            _toolbar = toolbar;
        }

        public void Deactivate()
        {
            StickerPicker?.Deactivate();
            EmojiPicker?.Deactivate();
            GifPicker?.Deactivate();

            if (_surface != null)
            {
                _surface.Entities.EditRequested -= Entities_EditRequested;
            }
        }

        public void UpdateSelection(PaintEntity selected)
        {
            var sticker = selected != null && (PaintEntityRenderer.IsSticker(selected) || PaintEntityRenderer.IsWidget(selected));
            var text = selected?.Type == PaintEntityType.Text;
            var shape = selected?.Type == PaintEntityType.Shape;

            CropTab.IsChecked = IsCropping;
            DrawTab.IsChecked = !IsCropping && selected == null;
            TextTab.IsChecked = !IsCropping && text;
            StickerTab.IsChecked = !IsCropping && sticker;
            ShapesTab.IsChecked = !IsCropping && shape;

            SelectedIndex = IsCropping ? 0 : text ? 2 : sticker ? 3 : shape ? 4 : 1;
            MoveIndicator(true);
        }

        /// <summary>
        /// The crop tab, as its button opens it.
        /// </summary>
        public void OpenCropping()
        {
            CropTab_Click(null, null);
        }

        /// <summary>
        /// The draw tab, as its button opens it.
        /// </summary>
        public void OpenDrawing()
        {
            DrawTab_Click(null, null);
        }

        private void CropTab_Click(object sender, RoutedEventArgs e)
        {
            if (IsCropping)
            {
                UpdateSelection(null);
                return;
            }

            IsCropping = true;
            CroppingChanged?.Invoke(this, EventArgs.Empty);

            UpdateSelection(null);
        }

        // Any other tab closes the crop first, which makes the toolbar if this is its first time.
        private void LeaveCropping()
        {
            if (IsCropping)
            {
                IsCropping = false;
                CroppingChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        private void DrawTab_Click(object sender, RoutedEventArgs e)
        {
            LeaveCropping();
            _toolbar.SelectDrawing();
        }

        private void TextTab_Click(object sender, RoutedEventArgs e)
        {
            LeaveCropping();

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
            LeaveCropping();

            ShowStickerPicker();
            _toolbar.UpdateTools();
        }

        private UIElement GetTab(int index) => index switch
        {
            0 => CropTab,
            2 => TextTab,
            3 => StickerTab,
            4 => ShapesTab,
            _ => DrawTab
        };

        // The tabs are laid out after the first selection is known.
        private void TabsPanel_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            MoveIndicator(false);
        }

        // Slides under the open tab, stretching on the way: longest a little before halfway, back
        // to its size as it lands.
        private void MoveIndicator(bool animate)
        {
            var index = SelectedIndex;
            var tab = GetTab(index);
            var size = tab.ActualSize;

            if (size.X == 0)
            {
                return;
            }

            var x = tab.ActualOffset.X + (size.X - IndicatorWidth) / 2;

            ElementCompositionPreview.SetIsTranslationEnabled(Indicator, true);
            var visual = ElementComposition.GetElementVisual(Indicator);

            if (!animate || _indicatorIndex < 0)
            {
                visual.Properties.InsertVector3("Translation", new Vector3(x, 0, 0));
            }
            else if (index != _indicatorIndex)
            {
                visual.Properties.TryGetVector3("Translation", out Vector3 from);

                var compositor = visual.Compositor;
                var easing = compositor.CreateCubicBezierEasingFunction(new Vector2(0.1f, 0.9f), new Vector2(0.2f, 1));

                var translation = compositor.CreateVector3KeyFrameAnimation();
                translation.InsertKeyFrame(1, new Vector3(x, 0, 0), easing);
                translation.Duration = IndicatorDuration;

                var stretch = compositor.CreateScalarKeyFrameAnimation();
                stretch.InsertKeyFrame(0.4f, 1 + Math.Min(Math.Abs(x - from.X) / IndicatorWidth, 4) / 2);
                stretch.InsertKeyFrame(1, 1, easing);
                stretch.Duration = IndicatorDuration;

                visual.CenterPoint = new Vector3(IndicatorWidth / 2, 1.5f, 0);
                visual.StartAnimation("Translation", translation);
                visual.StartAnimation("Scale.X", stretch);
            }

            _indicatorIndex = index;
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


                    LocationWidgetText.Text = Strings.StoryWidgetLocation.ToUpper();

                    PhotoWidgetText.Text = Strings.StoryWidgetPhoto.ToUpper();
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

            // The emoji is an animation, so the temperature alone names it.
            AutomationProperties.SetName(WeatherWidget, WeatherWidgetText.Text);
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

        private async void LocationWidget_Click(object sender, RoutedEventArgs e)
        {
            StickerFlyout.Hide();

            var navigationService = WindowContext.GetNavigationService(this);
            var popup = new SendLocationPopup(_clientService.Session, await GetMediaLocationAsync(_surface.Media?.File));

            var confirm = await navigationService.ShowPopupAsync(popup, requestedTheme: ElementTheme.Dark);
            if (confirm == ContentDialogResult.Primary)
            {
                // Its first fill, on the other clients too.
                var location = new PaintEntity
                {
                    WidgetStyle = PaintWidgetStyle.Black
                };

                popup.ApplyTo(location);
                _surface.Entities.AddLocation(location);
            }
        }

        private async void PhotoWidget_Click(object sender, RoutedEventArgs e)
        {
            StickerFlyout.Hide();

            try
            {
                var picker = new FileOpenPicker();
                picker.ViewMode = PickerViewMode.Thumbnail;
                picker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
                picker.FileTypeFilter.AddRange(Constants.PhotoTypes);

                var file = await picker.PickSingleFileAsync(XamlRoot);
                if (file == null)
                {
                    return;
                }

                var photo = await ImportPhotoAsync(file, _clientService.Session.Id);
                if (photo != null)
                {
                    _surface.Entities.AddPhoto(photo.Path, photo.Width, photo.Height);
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
            }
        }

        private sealed class ImportedPhoto
        {
            public string Path { get; init; }

            public int Width { get; init; }

            public int Height { get; init; }
        }

        // An upright copy, at most as large as a story is drawn: the story may be generated after a
        // restart, when the picked file can no longer be opened. It goes among TDLib's own photos,
        // which TDLib clears out in time and the storage settings can clear at once. PNG, so that
        // a transparent photo stays transparent.
        private static async Task<ImportedPhoto> ImportPhotoAsync(StorageFile file, int sessionId)
        {
            using var input = await file.OpenReadAsync();

            var decoder = await BitmapDecoder.CreateAsync(input);
            var transform = ImageHelper.ComputeScalingTransformForSourceImage(decoder, 1920);

            using var bitmap = await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied, transform, ExifOrientationMode.RespectExifOrientation, ColorManagementMode.DoNotColorManage);

            var session = await ApplicationData.Current.LocalFolder.CreateFolderAsync($"{sessionId}", CreationCollisionOption.OpenIfExists);
            var folder = await session.CreateFolderAsync("photos", CreationCollisionOption.OpenIfExists);
            var output = await folder.CreateFileAsync("photo.png", CreationCollisionOption.GenerateUniqueName);

            using (var stream = await output.OpenAsync(FileAccessMode.ReadWrite))
            {
                var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
                encoder.SetSoftwareBitmap(bitmap);

                await encoder.FlushAsync();
            }

            return new ImportedPhoto
            {
                Path = output.Path,
                Width = bitmap.PixelWidth,
                Height = bitmap.PixelHeight
            };
        }

        private async void Entities_EditRequested(object sender, PaintEntityEventArgs e)
        {
            var navigationService = WindowContext.GetNavigationService(this);
            var entity = e.Entity;

            if (entity.Type == PaintEntityType.Link)
            {
                var popup = new StoryLinkPopup(_clientService, entity);
                if (await navigationService.ShowPopupAsync(popup, requestedTheme: ElementTheme.Dark) == ContentDialogResult.Primary)
                {
                    popup.ApplyTo(entity);
                    _surface.Entities.RefreshWidget(entity);
                }
            }
            else if (entity.Type == PaintEntityType.Location)
            {
                var popup = new SendLocationPopup(_clientService.Session, new Location(entity.LocationLatitude, entity.LocationLongitude, 0));
                if (await navigationService.ShowPopupAsync(popup, requestedTheme: ElementTheme.Dark) == ContentDialogResult.Primary)
                {
                    popup.ApplyTo(entity);
                    _surface.Entities.RefreshWidget(entity);
                }
            }
        }

        // Where the media was taken, as its tags say, or null to start from the device.
        private static async Task<Location> GetMediaLocationAsync(StorageFile file)
        {
            if (file == null)
            {
                return null;
            }

            try
            {
                double? latitude, longitude;

                if (file.HasExtension(Constants.VideoTypes))
                {
                    var properties = await file.Properties.GetVideoPropertiesAsync();
                    latitude = properties.Latitude;
                    longitude = properties.Longitude;
                }
                else
                {
                    var properties = await file.Properties.GetImagePropertiesAsync();
                    latitude = properties.Latitude;
                    longitude = properties.Longitude;
                }

                if (latitude is double lat && longitude is double lng)
                {
                    return new Location(lat, lng, 0);
                }
            }
            catch
            {
                // Read as untagged.
            }

            return null;
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
            LeaveCropping();

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
