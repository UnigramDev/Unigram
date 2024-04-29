//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Telegram.Common;
using Telegram.Controls.Media;
using Telegram.Td.Api;
using Telegram.ViewModels.Stories;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;

namespace Telegram.Controls.Cells
{
    public sealed partial class StoryCell : UserControl
    {
        private StoryViewModel _viewModel;
        public StoryViewModel ViewModel => _viewModel;

        public StoryCell()
        {
            InitializeComponent();
            LayoutRoot.Constraint = new Size(256, 320);
        }

        public void Update(StoryViewModel story, bool pinned = false)
        {
            _viewModel = story;

            var glyph = pinned
                ? Icons.PinFilled16
                : story.PrivacySettings switch
                {
                    StoryPrivacySettingsCloseFriends => Icons.StarFilled16,
                    StoryPrivacySettingsSelectedUsers => Icons.PeopleFilled16,
                    StoryPrivacySettingsContacts => Icons.PersonCircleFilled16,
                    _ => null
                };

            if (glyph != null)
            {
                Glyph.Text = glyph;
                DropShadowEx.Attach(Glyph, target: Shadow);
            }
            else
            {
                Glyph.Text = string.Empty;
            }


            if (story.Content is StoryContentPhoto photo)
            {
                Overlay.Visibility = Visibility.Collapsed;

                var file = photo.Photo.GetBig();
                if (file != null)
                {
                    UpdateFile(story, file.Photo, true);
                }

                var thumbnail = photo.Photo.GetSmall();
                if (thumbnail != null /*&& (file == null || !file.Photo.Local.IsDownloadingCompleted)*/)
                {
                    UpdateThumbnail(story, thumbnail.Photo, photo.Photo.Minithumbnail, true);
                }
            }
            else if (story.Content is StoryContentVideo video)
            {
                Overlay.Visibility = Visibility.Visible;
                Subtitle.Text = video.Video.GetDuration();

                UpdateManager.Unsubscribe(this, ref _fileToken, true);
                Texture.ImageSource = null;

                var thumbnail = video.Video.Thumbnail;
                if (thumbnail != null /*&& (file == null || !file.Photo.Local.IsDownloadingCompleted)*/)
                {
                    UpdateThumbnail(story, thumbnail.File, video.Video.Minithumbnail, true);
                }
            }
        }

        private long _fileToken;
        private long _thumbnailToken;

        private void UpdateThumbnail(object target, File file)
        {
            UpdateThumbnail(_viewModel, file, null, false);
        }

        private void UpdateFile(object target, File file)
        {
            UpdateFile(_viewModel, file, false);
        }

        private void UpdateThumbnail(StoryViewModel story, File file, Minithumbnail minithumbnail, bool download)
        {
            BitmapImage source = null;
            ImageBrush brush;

            if (LayoutRoot.Background is ImageBrush existing)
            {
                brush = existing;
            }
            else
            {
                brush = new ImageBrush
                {
                    Stretch = Stretch.UniformToFill,
                    AlignmentX = AlignmentX.Center,
                    AlignmentY = AlignmentY.Center
                };

                LayoutRoot.Background = brush;
            }

            if (file != null)
            {
                if (file.Local.IsDownloadingCompleted)
                {
                    source = new BitmapImage();
                    PlaceholderHelper.GetBlurred(source, file.Local.Path, 3);
                }
                else
                {
                    if (download)
                    {
                        if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive)
                        {
                            story.ClientService.DownloadFile(file.Id, 1);
                        }

                        UpdateManager.Subscribe(this, story.ClientService, file, ref _thumbnailToken, UpdateThumbnail, true);
                    }

                    if (minithumbnail != null)
                    {
                        source = new BitmapImage();
                        PlaceholderHelper.GetBlurred(source, minithumbnail.Data, 3);
                    }
                }
            }
            else if (minithumbnail != null)
            {
                source = new BitmapImage();
                PlaceholderHelper.GetBlurred(source, minithumbnail.Data, 3);
            }

            brush.ImageSource = source;
        }

        private void UpdateFile(StoryViewModel item, File file, bool download)
        {
            if (file.Local.IsDownloadingCompleted)
            {
                UpdateManager.Unsubscribe(this, ref _fileToken, true);
                Texture.ImageSource = UriEx.ToBitmap(file.Local.Path, 0, 0);
            }
            else if (download)
            {
                Texture.ImageSource = null;

                if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive)
                {
                    item.ClientService.DownloadFile(file.Id, 1);
                }

                UpdateManager.Subscribe(this, _viewModel.ClientService, file, ref _fileToken, UpdateFile, true);
            }
        }
    }
}
