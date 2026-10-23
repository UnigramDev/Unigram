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
using Telegram.Common;
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

                // TDLib takes the cover from the start of the trimmed video.
                var coverTimestamp = Math.Clamp((state.CoverTime - start).TotalSeconds, 0, (stop - start).TotalSeconds);

                return new InputStoryContentVideo(file, MessageFactory.GetAddedStickers(state.Entities), (stop - start).TotalSeconds, coverTimestamp, video.IsMuted || !video.HasAudio);
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
                    areas.Add(new InputStoryArea(GetPosition(entity, PaintEntityRenderer.GetReactionBounds(), state.FrameSize, 0), type));
                }
                else if (entity.Type == PaintEntityType.Weather)
                {
                    var bounds = PaintEntityRenderer.GetWidgetBounds(entity);
                    var background = PaintEntityRenderer.GetWidgetBackground(entity.WidgetStyle);

                    // In whole degrees, as it was shown.
                    var type = new InputStoryAreaTypeWeather(Math.Floor(entity.WeatherTemperature + 0.5), entity.WeatherEmoji, background.ToValue(true));

                    areas ??= new List<InputStoryArea>();
                    areas.Add(new InputStoryArea(GetPosition(entity, bounds, state.FrameSize, bounds.Height * 0.2), type));
                }
                else if (entity.Type == PaintEntityType.Link && !string.IsNullOrEmpty(entity.LinkUrl))
                {
                    var bounds = PaintEntityRenderer.GetWidgetBounds(entity);
                    var radius = PaintLinkVisual.GetRadius(entity, entity.WidgetSize) * PaintLinkVisual.Unit;

                    // An address typed without its scheme is still a web one.
                    var url = entity.LinkUrl.Contains("://")
                        ? entity.LinkUrl
                        : "https://" + entity.LinkUrl;

                    areas ??= new List<InputStoryArea>();
                    areas.Add(new InputStoryArea(GetPosition(entity, bounds, state.FrameSize, radius), new InputStoryAreaTypeLink(url)));
                }
                else if (entity.Type == PaintEntityType.Location)
                {
                    var bounds = PaintEntityRenderer.GetWidgetBounds(entity);

                    // Named by the venue bot's query and result; by its point should those be missing.
                    InputStoryAreaType type = entity.LocationQueryId != 0 && !string.IsNullOrEmpty(entity.LocationResultId)
                        ? new InputStoryAreaTypeFoundVenue(entity.LocationQueryId, entity.LocationResultId)
                        : new InputStoryAreaTypeLocation(new Location(entity.LocationLatitude, entity.LocationLongitude, 0), null);

                    areas ??= new List<InputStoryArea>();
                    areas.Add(new InputStoryArea(GetPosition(entity, bounds, state.FrameSize, bounds.Height * 0.2), type));
                }
            }

            return areas != null
                ? new InputStoryAreas(new Vector<InputStoryArea>(areas))
                : null;
        }

        // An object laid out in the frame, as shares of the frame: its centre, the size of what it
        // covers, its clockwise turn in degrees, and its corner radius against the frame's width.
        // The radius is in the renderer's units, as the bounds are.
        private static AreaPosition GetPosition(PaintEntity entity, Rect bounds, Size frame, double radius)
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
                radius * scale * 100);
        }
    }
}
