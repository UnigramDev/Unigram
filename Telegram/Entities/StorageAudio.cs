//
// Copyright Fela Ameghino 2015-2025
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Threading.Tasks;
using Telegram.Native;
using Windows.Storage;

namespace Telegram.Entities
{
    public partial class StorageAudio : StorageMedia
    {
        private StorageAudio(StorageFile file, ulong fileSize, double totalMilliseconds, string title, string performer)
            : base(file, fileSize)
        {
            Title = title;
            Performer = performer;
            TotalSeconds = (int)Math.Floor(totalMilliseconds / 1000);
        }

        public string Title { get; }

        public string Performer { get; }

        public int TotalSeconds { get; }

        public string Duration
        {
            get
            {
                var duration = TimeSpan.FromSeconds(TotalSeconds);
                if (duration.TotalHours >= 1)
                {
                    return duration.ToString("h\\:mm\\:ss");
                }
                else
                {
                    return duration.ToString("mm\\:ss");
                }
            }
        }

        public static async Task<StorageAudio> CreateAsync(StorageFile file, ulong fileSize)
        {
            try
            {
                using var stream = await file.OpenReadAsync();
                using var animation = await Task.Run(() => VideoAnimation.LoadFromFile(new VideoAnimationStreamSource(stream), true, false, true));

                if (animation != null && animation.Duration > 0 && animation.HasAudio && !animation.HasVideo)
                {
                    return new StorageAudio(file, fileSize, animation.Duration, animation.Title, animation.Artist);
                }

                return null;
            }
            catch
            {
                return null;
            }
        }
    }
}
