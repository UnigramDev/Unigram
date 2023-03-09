//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.FileProperties;

namespace Telegram.Entities
{
    public class StorageAudio : StorageMedia
    {
        public StorageAudio(StorageFile file, BasicProperties basic, MusicProperties props)
            : base(file, basic)
        {
            Title = props.Title;
            Performer = props.AlbumArtist;
            Duration = (int)props.Duration.TotalSeconds;
        }

        public string Title { get; private set; }

        public string Performer { get; private set; }

        public int Duration { get; private set; }

        public static new async Task<StorageAudio> CreateAsync(StorageFile file)
        {
            try
            {
                if (!file.IsAvailable)
                {
                    return null;
                }

                var basic = await file.GetBasicPropertiesAsync();
                var audio = await file.Properties.GetMusicPropertiesAsync();

                return new StorageAudio(file, basic, audio);
            }
            catch
            {
                return null;
            }
        }
    }
}
