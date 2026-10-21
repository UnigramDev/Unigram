//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
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

        public StoryService(IClientService clientService, ISettingsService settings, IEventAggregator aggregator)
            : base(clientService, settings, aggregator)
        {
            aggregator.Subscribe<UpdateStory>(this, Handle)
                .Subscribe<UpdateStoryDeleted>(Handle)
                .Subscribe<UpdateStoryPostSucceeded>(Handle);
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

        private void Handle(UpdateStory update)
        {
            StoryViewModel viewModel;

            lock (_stories)
            {
                if (!TryGetValue(new StoryKey(update.Story.PosterChatId, update.Story.Id), out viewModel))
                {
                    return;
                }
            }

            viewModel.Update(update.Story);
        }

        private void Handle(UpdateStoryDeleted update)
        {
            lock (_stories)
            {
                _stories.Remove(new StoryKey(update.StoryPosterChatId, update.StoryId));
            }
        }

        private void Handle(UpdateStoryPostSucceeded update)
        {
            StoryViewModel viewModel;

            lock (_stories)
            {
                var oldKey = new StoryKey(update.Story.PosterChatId, update.OldStoryId);
                if (!TryGetValue(oldKey, out viewModel))
                {
                    return;
                }

                _stories.Remove(oldKey);

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
