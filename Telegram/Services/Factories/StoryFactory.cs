//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Telegram.Controls;
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

            // A photo with an animated sticker on it goes out as a video, as long as the stickers
            // take to come round together.
            var stillDuration = await GenerationService.GetStillDurationAsync(state.Entities, true);
            if (stillDuration > TimeSpan.Zero)
            {
                var still = new VideoGeneration
                {
                    Transcode = true,
                    Mute = true,
                    OutputSize = VideoSize,
                    VideoBitrate = GenerationService.StillBitrate,
                    StillDuration = stillDuration,
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

                var serialized = JsonSerializer.Serialize(still, GenerationJsonContext.Default.VideoGeneration);
                var file = await GenerationService.PrepareAsync(media.File, ConversionType.Transcode, serialized);

                return new InputStoryContentVideo(file, MessageFactory.GetAddedStickers(state.Entities), stillDuration.TotalSeconds, 0, true);
            }

            var photo = JsonSerializer.Serialize(state, GenerationJsonContext.Default.ImageGeneration);
            var input = await GenerationService.PrepareAsync(media.File, ConversionType.Compress, photo);

            return new InputStoryContentPhoto(input, MessageFactory.GetAddedStickers(state.Entities));
        }

        /// <summary>
        /// The story's clickable areas, from the objects in the edit in
        /// <see cref="StorageMedia.EditState"/>, or null when there are none.
        /// </summary>
        public static InputStoryAreas CreateAreas(StorageMedia media)
        {
            var state = media.EditState;
            if (state?.Entities == null || !state.HasFrame)
            {
                return null;
            }

            List<InputStoryArea> areas = null;

            foreach (var entity in state.Entities)
            {
                if (entity.Type == PaintEntityType.Reaction)
                {
                    var type = new InputStoryAreaTypeSuggestedReaction(entity.GetReactionType(), entity.ReactionDark, entity.Mirror);

                    areas ??= new List<InputStoryArea>();
                    areas.Add(new InputStoryArea(GetPosition(entity, PaintEntityRenderer.GetReactionBounds(), state.FrameSize), type));
                }
            }

            return areas != null
                ? new InputStoryAreas(new Vector<InputStoryArea>(areas))
                : null;
        }

        // An object laid out in the frame, as shares of the frame: its centre, the size of what it
        // covers, and its clockwise turn in degrees.
        private static AreaPosition GetPosition(PaintEntity entity, Rect bounds, Size frame)
        {
            // The renderer's units to shares of the frame's width.
            var scale = entity.Scale / PaintEntityRenderer.Reference;

            var degrees = entity.Rotation * 180 / Math.PI % 360;

            return new AreaPosition(
                entity.Position.X * 100,
                entity.Position.Y * 100,
                bounds.Width * scale * 100,
                bounds.Height * scale * frame.Width / frame.Height * 100,
                degrees < 0 ? degrees + 360 : degrees,
                0);
        }
    }
}
