//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Telegram.Common;
using Telegram.Services;
using Telegram.Td.Api;
using Windows.UI.Xaml.Controls;

namespace Telegram.Controls.Cells
{
    public sealed partial class InlineResultMediaCell : UserControl
    {
        private long _thumbnailToken;

        public InlineResultMediaCell()
        {
            InitializeComponent();
        }

        public void UpdateResult(IClientService clientService, InlineQueryResult result)
        {
            if (result is InlineQueryResultPhoto or InlineQueryResultVideo)
            {
                File file = null;
                if (result is InlineQueryResultPhoto photo)
                {
                    file = photo.Photo.GetSmall().Photo;
                }
                else if (result is InlineQueryResultVideo video)
                {
                    file = video.Video.Thumbnail?.File;
                }

                if (file == null)
                {
                    return;
                }

                if (file.Local.IsDownloadingCompleted)
                {
                    Texture.Source = UriEx.ToBitmap(file.Local.Path);
                    UpdateManager.Unsubscribe(this, ref _thumbnailToken, true);
                }
                else
                {
                    Texture.Source = null;
                    UpdateManager.Subscribe(this, clientService, file, ref _thumbnailToken, UpdateThumbnail, true);

                    if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive)
                    {
                        clientService.DownloadFile(file.Id, 16);
                    }
                }
            }
        }

        private void UpdateThumbnail(object target, File update)
        {
            if (update.Local.IsDownloadingCompleted)
            {
                Texture.Source = UriEx.ToBitmap(update.Local.Path);
            }
        }
    }
}
