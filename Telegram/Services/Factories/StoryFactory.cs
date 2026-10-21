//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Text.Json;
using System.Threading.Tasks;
using Telegram.Entities;
using Telegram.Td.Api;
using Windows.Foundation;

namespace Telegram.Services.Factories
{
    public static class StoryFactory
    {
        // The sizes TDLib requires a story's photo and video to be.
        public static readonly Size PhotoSize = new(1080, 1920);
        public static readonly Size VideoSize = new(720, 1280);

        /// <summary>
        /// The story's file, generated from the edit in <see cref="StorageMedia.EditState"/>, which
        /// must hold a frame.
        /// </summary>
        public static async Task<InputStoryContent> CreateContentAsync(StorageMedia media)
        {
            var state = media.EditState;

            if (media is StorageVideo video)
            {
                var generation = new VideoGeneration
                {
                    // Always: whatever the source, a story is H.264 at the one size TDLib takes.
                    Transcode = true,
                    Mute = video.IsMuted,
                    OutputSize = VideoSize,
                    Rectangle = state.Rectangle,
                    Angle = state.Angle,
                    Rotation = state.Rotation,
                    Flip = state.Flip,
                    Strokes = state.Strokes,
                    Entities = state.Entities,
                    FrameSize = state.FrameSize,
                    FrameTopColor = state.FrameTopColor,
                    FrameBottomColor = state.FrameBottomColor
                };

                var start = state.TrimStartTime;
                var stop = state.TrimStopTime > start
                    ? state.TrimStopTime
                    : TimeSpan.FromMilliseconds(video.TotalMilliseconds);

                if (state.TrimStartTime != default)
                {
                    generation.TrimStartTime = state.TrimStartTime;
                }

                if (state.TrimStopTime != default)
                {
                    generation.TrimStopTime = state.TrimStopTime;
                }

                var serialized = JsonSerializer.Serialize(generation, GenerationJsonContext.Default.VideoGeneration);
                var file = await GenerationService.PrepareAsync(video.File, ConversionType.Transcode, serialized);

                // The cover is the first frame kept, until one can be picked.
                return new InputStoryContentVideo(file, MessageFactory.GetAddedStickers(state.Entities), (stop - start).TotalSeconds, 0, video.IsMuted);
            }

            var photo = JsonSerializer.Serialize(state, GenerationJsonContext.Default.ImageGeneration);
            var input = await GenerationService.PrepareAsync(media.File, ConversionType.Compress, photo);

            return new InputStoryContentPhoto(input, MessageFactory.GetAddedStickers(state.Entities));
        }
    }
}
