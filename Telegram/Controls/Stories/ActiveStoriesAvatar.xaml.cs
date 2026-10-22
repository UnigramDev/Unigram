//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Telegram.Common;
using Telegram.Td.Api;
using Telegram.ViewModels.Stories;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Telegram.Controls.Stories
{
    /// <summary>
    /// A chat's photo in the stories strip, ringed by its stories, or by the progress or failure
    /// of your own posts.
    /// </summary>
    public sealed partial class ActiveStoriesAvatar : Grid
    {
        private ActiveStoriesViewModel _viewModel;

        private StoryViewModel _posting;
        private long _postingToken;

        public ActiveStoriesAvatar()
        {
            InitializeComponent();
        }

        public bool IsPosting => _posting != null;

        public bool IsPostFailed { get; private set; }

        public void Update(ActiveStoriesViewModel activeStories)
        {
            _viewModel = activeStories;

            var chat = activeStories.Chat;
            if (activeStories.ClientService.TryGetUser(chat, out User user))
            {
                Photo.Source = ProfilePictureSource.User(activeStories.ClientService, user);
            }
            else
            {
                Photo.Source = ProfilePictureSource.Chat(activeStories.ClientService, chat);
            }

            UpdatePosting();
        }

        public void UpdateSegments(ChatActiveStories activeStories)
        {
            Segments.UpdateActiveStories(activeStories, 48, true);
            SegmentsSmall?.UpdateActiveStories(activeStories, 48, false);

            LiveBadge.Visibility = Segments.HasLiveBadge
                ? Visibility.Visible
                : Visibility.Collapsed;

            // A cell's binding can run before Update hands over a recycled cell's new item.
            if (_viewModel != null && _viewModel.ChatId == activeStories?.ChatId)
            {
                UpdatePosting();
            }
        }

        public ActiveStoriesSegments LoadSegmentsSmall()
        {
            if (SegmentsSmall == null)
            {
                FindName(nameof(SegmentsSmall));

                if (_viewModel != null)
                {
                    SegmentsSmall.UpdateActiveStories(_viewModel.Item, 48, false);
                    UpdatePosting();
                }
            }

            return SegmentsSmall;
        }

        // Your own posts, while any is in flight or failed, take over the ring.
        private void UpdatePosting()
        {
            var posting = default(StoryViewModel);
            var failed = false;

            if (_viewModel.IsMyStory)
            {
                foreach (var story in _viewModel.Items)
                {
                    if (story.IsPostFailed)
                    {
                        failed = true;
                    }
                    else if (story.IsBeingPosted)
                    {
                        posting ??= story;
                    }
                }
            }

            IsPostFailed = failed;

            if (failed)
            {
                Segments.ShowFailed(48);
                SegmentsSmall?.ShowFailed(48, 3);

                posting = null;
            }
            else if (posting != null)
            {
                UpdatePostingProgress(posting);
            }

            // The ring takes the place of the segments while it shows.
            var segments = posting != null
                ? Visibility.Collapsed
                : Visibility.Visible;

            Segments.Visibility = segments;
            SegmentsSmall?.Visibility = segments;

            if (posting == null && UploadProgress != null)
            {
                UploadProgress.Visibility = Visibility.Collapsed;
            }

            // Your own cell with nothing in it is there to add a story from.
            var empty = _viewModel.IsMyStory && _viewModel.Items.Count == 0;
            if (empty && AddBadge == null)
            {
                FindName(nameof(AddBadge));
            }

            if (AddBadge != null)
            {
                AddBadge.Visibility = empty
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }

            // TDLib posts a chat's stories one at a time, so the first in flight is the one moving.
            if (_posting != posting)
            {
                _posting = posting;

                var file = posting?.GetFile();
                if (file != null)
                {
                    UpdateManager.Subscribe(this, posting.ClientService, file, ref _postingToken, UpdatePostingFile);

                    // The upload comes from the file, the conversion before it from GenerationService.
                    _viewModel.Aggregator.Subscribe<UpdateFileGenerationProgress>(this, Handle);
                }
                else
                {
                    UpdateManager.Unsubscribe(this, ref _postingToken);
                    _viewModel.Aggregator.Unsubscribe<UpdateFileGenerationProgress>(this);
                }
            }
        }

        private void Handle(UpdateFileGenerationProgress update)
        {
            this.BeginOnUIThread(UpdatePostingProgress);
        }

        private void UpdatePostingFile(File file)
        {
            UpdatePostingProgress();
        }

        private void UpdatePostingProgress()
        {
            if (_posting != null)
            {
                UpdatePostingProgress(_posting);
            }
        }

        private void UpdatePostingProgress(StoryViewModel posting)
        {
            if (UploadProgress == null)
            {
                FindName(nameof(UploadProgress));
            }

            UploadProgress.Visibility = Visibility.Visible;
            UploadProgress.Value = posting.GetPostProgress();
        }
    }
}
