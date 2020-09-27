using System;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Controls;
using Windows.Foundation;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Unigram.Views.Popups
{
    public sealed partial class ZoomableMediaPopup : Grid
    {
        private readonly ApplicationView _applicationView;

        private object _lastItem;

        public ZoomableMediaPopup()
        {
            InitializeComponent();

            _applicationView = ApplicationView.GetForCurrentView();
            _applicationView.VisibleBoundsChanged += OnVisibleBoundsChanged;
            
            OnVisibleBoundsChanged(_applicationView, null);
        }

        public Action<int> DownloadFile { get; set; }

        private void OnVisibleBoundsChanged(ApplicationView sender, object args)
        {
            if (sender == null)
            {
                return;
            }

            if (/*BackgroundElement != null &&*/ Window.Current?.Bounds is Rect bounds && sender.VisibleBounds != bounds)
            {
                Margin = new Thickness(sender.VisibleBounds.X - bounds.Left, sender.VisibleBounds.Y - bounds.Top, bounds.Width - (sender.VisibleBounds.Right - bounds.Left), bounds.Height - (sender.VisibleBounds.Bottom - bounds.Top));
            }
            else
            {
                Margin = new Thickness();
            }
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            Padding = new Thickness(0, 0, 0, InputPane.GetForCurrentView().OccludedRect.Height);

            InputPane.GetForCurrentView().Showing += InputPane_Showing;
            InputPane.GetForCurrentView().Hiding += InputPane_Hiding;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            InputPane.GetForCurrentView().Showing -= InputPane_Showing;
            InputPane.GetForCurrentView().Hiding -= InputPane_Hiding;
        }

        private void InputPane_Showing(InputPane sender, InputPaneVisibilityEventArgs args)
        {
            Padding = new Thickness(0, 0, 0, args.OccludedRect.Height);
        }

        private void InputPane_Hiding(InputPane sender, InputPaneVisibilityEventArgs args)
        {
            Padding = new Thickness();
        }

        public void SetSticker(Sticker sticker)
        {
            _lastItem = sticker;

            Title.Text = sticker.Emoji;
            Aspect.MaxWidth = 200;
            Aspect.MaxHeight = 200;
            Aspect.Constraint = sticker;

            if (sticker.Thumbnail != null)
            {
                UpdateThumbnail(sticker.Thumbnail.File, true);
            }

            UpdateFile(sticker, sticker.StickerValue, true);
        }

        public void SetAnimation(Animation animation)
        {
            _lastItem = animation;

            Title.Text = string.Empty;
            Aspect.MaxWidth = 320;
            Aspect.MaxHeight = 420;
            Aspect.Constraint = animation;

            if (animation.Thumbnail != null && animation.Thumbnail.Format is ThumbnailFormatJpeg)
            {
                UpdateThumbnail(animation.Thumbnail.File, true);
            }

            UpdateFile(animation, animation.AnimationValue, true);
        }

        public void UpdateFile(File file)
        {
            if (_lastItem is Sticker sticker && file.Local.IsDownloadingCompleted)
            {
                if (sticker.StickerValue.Id == file.Id)
                {
                    UpdateFile(sticker, file, false);
                }
                else if (sticker.Thumbnail?.File.Id == file.Id)
                {
                    UpdateThumbnail(file, false);
                }
            }
            else if (_lastItem is Animation animation && file.Local.IsDownloadingCompleted)
            {
                if (animation.AnimationValue.Id == file.Id)
                {
                    UpdateFile(animation, file, false);
                }
                else if (animation.Thumbnail?.File.Id == file.Id && animation.Thumbnail.Format is ThumbnailFormatJpeg)
                {
                    UpdateThumbnail(file, false);
                }
            }
        }

        private void UpdateFile(Sticker sticker, File file, bool download)
        {
            if (file.Local.IsDownloadingCompleted)
            {
                if (sticker.IsAnimated)
                {
                    Thumbnail.Opacity = 0;
                    Texture.Source = null;
                    Container.Child = new LottieView { Source = new Uri("file:///" + file.Local.Path) };
                }
                else
                {
                    Thumbnail.Opacity = 0;
                    Texture.Source = PlaceholderHelper.GetWebPFrame(file.Local.Path);
                    Container.Child = new Border();
                }
            }
            else if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive)
            {
                Thumbnail.Opacity = 1;
                Texture.Source = null;
                Container.Child = new Border();

                if (download)
                {
                    DownloadFile?.Invoke(file.Id);
                }
            }
        }

        private void UpdateFile(Animation animation, File file, bool download)
        {
            if (file.Local.IsDownloadingCompleted)
            {
                Thumbnail.Opacity = 0;
                Texture.Source = null;
                Container.Child = new AnimationView { Source = new Uri("file:///" + file.Local.Path) };
            }
            else if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive)
            {
                Thumbnail.Opacity = 1;
                Texture.Source = null;
                Container.Child = new Border();

                if (download)
                {
                    DownloadFile?.Invoke(file.Id);
                }
            }
        }

        private void UpdateThumbnail(File file, bool download)
        {
            if (file.Local.IsDownloadingCompleted)
            {
                Thumbnail.Source = PlaceholderHelper.GetWebPFrame(file.Local.Path);
            }
            else if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive)
            {
                Thumbnail.Source = null;

                if (download)
                {
                    DownloadFile?.Invoke(file.Id);
                }
            }
        }
    }
}
