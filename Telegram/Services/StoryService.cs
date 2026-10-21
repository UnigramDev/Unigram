//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Telegram.ViewModels.Stories;

namespace Telegram.Services
{
    public interface IStoryService
    {
        /// <summary>
        /// The story's one view model, brought up to date with <paramref name="story"/> if it
        /// already exists.
        /// </summary>
        StoryViewModel GetOrCreate(Story story);

        StoryViewModel GetOrCreate(long chatId, StoryInfo storyInfo);

        /// <summary>
        /// Keeps the request that posted <paramref name="story"/>, so that it can be sent again if
        /// the post fails.
        /// </summary>
        void TrackPost(PostStory request, Story story);

        /// <summary>
        /// The chat's stories whose post failed, oldest first.
        /// </summary>
        StoryViewModel[] GetFailedStories(long chatId);

        /// <summary>
        /// Posts a failed story again. It is replaced by the new post's own view model.
        /// </summary>
        Task<Object> RetryAsync(StoryViewModel story);

        /// <summary>
        /// Cancels a story being posted, or forgets one whose post failed.
        /// </summary>
        Task<Object> DiscardAsync(StoryViewModel story);
    }

    // Every list showing a story (the strip, a profile grid, a viewer) gets the same instance from
    // here, so an update reaches all of them and a posted story keeps its view model when TDLib
    // swaps its temporary id for the real one.
    public partial class StoryService : ServiceBase, IStoryService, IHandle
    {
        private readonly record struct StoryKey(long ChatId, int StoryId);

        // Weak, so the lists holding a story decide how long it lives.
        private readonly Dictionary<StoryKey, WeakReference<StoryViewModel>> _stories = new();
        private int _pruneAt = 64;

        // Posts not yet confirmed, held until they are: in flight, or failed and waiting for a retry.
        // Both are guarded by the _stories lock.
        private readonly List<StoryViewModel> _outgoing = new();
        private readonly Dictionary<StoryKey, PostStory> _requests = new();

        public StoryService(IClientService clientService, ISettingsService settings, IEventAggregator aggregator)
            : base(clientService, settings, aggregator)
        {
            aggregator.Subscribe<UpdateStory>(this, Handle)
                .Subscribe<UpdateStoryDeleted>(Handle)
                .Subscribe<UpdateStoryPostSucceeded>(Handle)
                .Subscribe<UpdateStoryPostFailed>(Handle)
                .Subscribe<UpdateFileGenerationProgress>(Handle);
        }

        public StoryViewModel GetOrCreate(Story story)
        {
            StoryViewModel viewModel;

            lock (_stories)
            {
                if (!TryGetValue(new StoryKey(story.PosterChatId, story.Id), out viewModel))
                {
                    viewModel = new StoryViewModel(ClientService, story);
                    Add(new StoryKey(story.PosterChatId, story.Id), viewModel);

                    return viewModel;
                }
            }

            viewModel.Update(story);
            return viewModel;
        }

        public StoryViewModel GetOrCreate(long chatId, StoryInfo storyInfo)
        {
            lock (_stories)
            {
                if (!TryGetValue(new StoryKey(chatId, storyInfo.StoryId), out StoryViewModel viewModel))
                {
                    viewModel = new StoryViewModel(ClientService, chatId, storyInfo);
                    Add(new StoryKey(chatId, storyInfo.StoryId), viewModel);
                }

                return viewModel;
            }
        }

        private bool TryGetValue(StoryKey key, out StoryViewModel viewModel)
        {
            if (_stories.TryGetValue(key, out var reference) && reference.TryGetTarget(out viewModel))
            {
                return true;
            }

            viewModel = null;
            return false;
        }

        private void Add(StoryKey key, StoryViewModel viewModel)
        {
            _stories[key] = new WeakReference<StoryViewModel>(viewModel);

            if (_stories.Count >= _pruneAt)
            {
                Prune();
            }
        }

        private void Prune()
        {
            List<StoryKey> collected = null;

            foreach (var item in _stories)
            {
                if (!item.Value.TryGetTarget(out _))
                {
                    collected ??= new List<StoryKey>();
                    collected.Add(item.Key);
                }
            }

            if (collected != null)
            {
                foreach (var key in collected)
                {
                    _stories.Remove(key);
                }
            }

            _pruneAt = Math.Max(64, _stories.Count * 2);
        }

        public void TrackPost(PostStory request, Story story)
        {
            lock (_stories)
            {
                _requests[new StoryKey(story.PosterChatId, story.Id)] = request;
            }
        }

        public StoryViewModel[] GetFailedStories(long chatId)
        {
            lock (_stories)
            {
                var count = 0;

                foreach (var story in _outgoing)
                {
                    if (story.IsPostFailed && story.PosterChatId == chatId)
                    {
                        count++;
                    }
                }

                if (count == 0)
                {
                    return Array.Empty<StoryViewModel>();
                }

                var failed = new StoryViewModel[count];
                var index = 0;

                foreach (var story in _outgoing)
                {
                    if (story.IsPostFailed && story.PosterChatId == chatId)
                    {
                        failed[index++] = story;
                    }
                }

                return failed;
            }
        }

        public async Task<Object> RetryAsync(StoryViewModel story)
        {
            var key = new StoryKey(story.PosterChatId, story.Id);
            PostStory request;

            lock (_stories)
            {
                if (!story.IsPostFailed || !_requests.Remove(key, out request))
                {
                    return null;
                }

                Forget(key, story);
            }

            Aggregator.Publish(new UpdateFailedStories(story.PosterChatId));

            var response = await ClientService.SendAsync(request);
            if (response is Story posted)
            {
                TrackPost(request, posted);
            }
            else if (response is Error error)
            {
                lock (_stories)
                {
                    _requests[key] = request;
                    _outgoing.Add(story);
                    Add(key, story);
                }

                story.SetPostError(error);
                Aggregator.Publish(new UpdateFailedStories(story.PosterChatId));
            }

            return response;
        }

        public async Task<Object> DiscardAsync(StoryViewModel story)
        {
            if (story.IsPostFailed)
            {
                lock (_stories)
                {
                    Forget(new StoryKey(story.PosterChatId, story.Id), story);
                }

                Aggregator.Publish(new UpdateFailedStories(story.PosterChatId));
                return new Ok();
            }

            // TDLib then reports the story deleted, which forgets it.
            return await ClientService.SendAsync(new DeleteStory(story.PosterChatId, story.Id));
        }

        private void Handle(UpdateFileGenerationProgress update)
        {
            StoryViewModel viewModel = null;

            lock (_stories)
            {
                foreach (var request in _requests)
                {
                    if (GetConversion(request.Value.Content) == update.Conversion)
                    {
                        TryGetValue(request.Key, out viewModel);
                        break;
                    }
                }
            }

            viewModel?.SetPostConversionProgress(update.Percent / 100f);
        }

        private static string GetConversion(InputStoryContent content)
        {
            return content switch
            {
                InputStoryContentVideo { Video: InputFileGenerated video } => video.Conversion,
                InputStoryContentPhoto { Photo: InputFileGenerated photo } => photo.Conversion,
                _ => null
            };
        }

        private void Forget(StoryKey key, StoryViewModel story)
        {
            _stories.Remove(key);
            _requests.Remove(key);
            _outgoing.Remove(story);
        }

        private void Handle(UpdateStory update)
        {
            var key = new StoryKey(update.Story.PosterChatId, update.Story.Id);
            StoryViewModel viewModel;

            lock (_stories)
            {
                if (!TryGetValue(key, out viewModel))
                {
                    // TDLib announces a post before the list carrying it, so its view model exists
                    // with its state by the time a list asks for it, and is held while in flight.
                    if (update.Story.IsBeingPosted)
                    {
                        viewModel = new StoryViewModel(ClientService, update.Story);
                        Add(key, viewModel);
                        _outgoing.Add(viewModel);
                    }

                    return;
                }

                if (update.Story.IsBeingPosted && !_outgoing.Contains(viewModel))
                {
                    _outgoing.Add(viewModel);
                }
            }

            viewModel.Update(update.Story);
        }

        private void Handle(UpdateStoryDeleted update)
        {
            var key = new StoryKey(update.StoryPosterChatId, update.StoryId);

            lock (_stories)
            {
                if (TryGetValue(key, out StoryViewModel viewModel))
                {
                    Forget(key, viewModel);
                }
                else
                {
                    _stories.Remove(key);
                    _requests.Remove(key);
                }
            }
        }

        private void Handle(UpdateStoryPostFailed update)
        {
            var key = new StoryKey(update.Story.PosterChatId, update.Story.Id);
            StoryViewModel viewModel;

            lock (_stories)
            {
                if (!TryGetValue(key, out viewModel))
                {
                    viewModel = new StoryViewModel(ClientService, update.Story);
                    Add(key, viewModel);
                }

                if (!_outgoing.Contains(viewModel))
                {
                    _outgoing.Add(viewModel);
                }
            }

            viewModel.Update(update.Story);
            viewModel.SetPostError(update.Error);

            Aggregator.Publish(new UpdateFailedStories(update.Story.PosterChatId));
        }

        private void Handle(UpdateStoryPostSucceeded update)
        {
            StoryViewModel viewModel;

            lock (_stories)
            {
                var oldKey = new StoryKey(update.Story.PosterChatId, update.OldStoryId);
                _requests.Remove(oldKey);

                if (!TryGetValue(oldKey, out viewModel))
                {
                    return;
                }

                _stories.Remove(oldKey);
                _outgoing.Remove(viewModel);

                // A list can have asked for the new id first, as TDLib sends the active stories
                // that carry it before this update; that instance is the one the list holds.
                var newKey = new StoryKey(update.Story.PosterChatId, update.Story.Id);
                if (!TryGetValue(newKey, out _))
                {
                    _stories[newKey] = new WeakReference<StoryViewModel>(viewModel);
                }
            }

            viewModel.Update(update.Story);
        }
    }
}
