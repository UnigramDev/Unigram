//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Telegram.Controls;
using Telegram.Native.Controls;
using Telegram.Navigation;
using Telegram.Streams;
using Telegram.Td.Api;
using Windows.Foundation;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media.Imaging;

namespace Telegram.Views.Popups
{
    public sealed partial class ZoomableMediaPopup : GridEx
    {
        public ViewModelBase ViewModel => DataContext as ViewModelBase;

        private ApplicationView _applicationView;

        private object _lastItem;

        public ZoomableMediaPopup()
        {
            InitializeComponent();
        }

        // TODO: WinUI - These handlers are no longer needed and can be removed
        protected override void OnLoaded()
        {
            _applicationView = ApplicationView.GetForCurrentView();
            _applicationView.VisibleBoundsChanged += OnVisibleBoundsChanged;

            OnVisibleBoundsChanged(_applicationView, null);
        }

        // TODO: WinUI - These handlers are no longer needed and can be removed
        protected override void OnUnloaded()
        {
            _lastItem = null;

            if (_applicationView != null)
            {
                _applicationView.VisibleBoundsChanged -= OnVisibleBoundsChanged;
            }
        }

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

        public void SetSticker(Sticker sticker)
        {
            _lastItem = sticker;

            Title.Text = sticker.Emoji;
            Aspect.MaxWidth = 224;
            Aspect.MaxHeight = 224;
            Aspect.Constraint = sticker;

            Thumbnail.Opacity = 0;
            Texture.Source = null;
            Container.Child = new AnimatedImage
            {
                AutoPlay = true,
                FrameSize = new Size(224, 224),
                DecodeFrameType = DecodePixelType.Logical,
                IsCachingEnabled = true,
                Source = new DelayedFileSource(ViewModel.ClientService, sticker.StickerValue)
            };
        }

        public void SetAnimation(Animation animation)
        {
            _lastItem = animation;

            Title.Text = string.Empty;
            Aspect.MaxWidth = 420;
            Aspect.MaxHeight = 420;
            Aspect.Constraint = animation;

            Thumbnail.Opacity = 0;
            Texture.Source = null;
            Container.Child = new AnimatedImage
            {
                AutoPlay = true,
                FrameSize = new Size(420, 420),
                DecodeFrameType = DecodePixelType.Logical,
                IsCachingEnabled = false,
                Source = new DelayedFileSource(ViewModel.ClientService, animation.AnimationValue)
            };
        }

        public void SetPhoto(Photo photo)
        {
            _lastItem = photo;

            Title.Text = string.Empty;
            Aspect.MaxWidth = 420;
            Aspect.MaxHeight = 420;
            Aspect.Constraint = photo;

            Thumbnail.Opacity = 0;

            var big = photo.GetBig();
            var small = photo.GetSmall();

            var content = new ImageView();

            if (big.Photo.Id != small.Photo.Id)
            {
                Texture.SetSource(ViewModel.ClientService, small.Photo, photo.Minithumbnail);
                content.SetSource(ViewModel.ClientService, big.Photo);
            }
            else
            {
                Texture.Source = null;
                content.SetSource(ViewModel.ClientService, big.Photo, photo.Minithumbnail);
            }

            Container.Child = content;
        }
    }
}
