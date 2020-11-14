using System;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.FileProperties;

namespace Unigram.Entities
{
    public class StorageAudio : StorageMedia
    {
        public StorageAudio(StorageFile file, MusicProperties props)
            : base(file, null)
        {
            Title = props.Title;
            Performer = props.AlbumArtist;
            Duration = (int)props.Duration.TotalSeconds;
        }

        public string Title { get; private set; }

        public string Performer { get; private set; }

        public int Duration { get; private set; }

        public new static async Task<StorageAudio> CreateAsync(StorageFile file)
        {
            try
            {
                if (!file.IsAvailable)
                {
                    return null;
                }

                var basic = await file.GetBasicPropertiesAsync();
                var audio = await file.Properties.GetMusicPropertiesAsync();

                return new StorageAudio(file, audio);
            }
            catch
            {
                return null;
            }
        }
    }
}
