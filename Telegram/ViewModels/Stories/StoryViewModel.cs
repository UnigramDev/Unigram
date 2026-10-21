//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Services;
using Telegram.Td.Api;

namespace Telegram.ViewModels.Stories
{
    // Get one from IStoryService rather than constructing it: that is what makes it the only view
    // model of its story. Bot previews are the exception, as their ids are only positions.
    // Deliberately not bindable: one instance is shared by windows on different threads, and it is
    // updated on the TDLib thread.
    public partial class StoryViewModel
    {
        public IClientService ClientService { get; }

        // One load per story, whichever window asks first.
        private readonly object _loadLock = new();
        private Task _load;

        public StoryViewModel(IClientService clientService, long chatId, StoryInfo storyInfo)
        {
            ClientService = clientService;

            PosterChatId = chatId;
            Chat = ClientService.GetChat(chatId);

            Date = storyInfo.Date;
            Id = storyInfo.StoryId;
        }

        public StoryViewModel(IClientService clientService, Story story, bool botPreview = false)
        {
            ClientService = clientService;

            PosterChatId = story.PosterChatId;
            Chat = ClientService.GetChat(story.PosterChatId);

            IsBotPreview = botPreview;

            Update(story);
        }

        public int Date { get; private set; }

        public long PosterChatId { get; }

        public Chat Chat { get; }

        public int Id { get; private set; }

        public bool IsBotPreview { get; }

        public Task LoadAsync()
        {
            lock (_loadLock)
            {
                return _load ??= LoadCoreAsync();
            }
        }

        private async Task LoadCoreAsync()
        {
            var response = await ClientService.SendAsync(new GetStory(PosterChatId, Id, false));
            if (response is Story story)
            {
                Update(story);
                DownloadFiles();

                if (story.Content is StoryContentLive live && !ClientService.TryGetGroupCall(live.GroupCallId, out _))
                {
                    await ClientService.SendAsync(new GetGroupCall(live.GroupCallId));
                }
            }
        }

        public void Update(Story story)
        {
            Id = story.Id;
            Date = story.Date;
            Caption = story.Caption;
            Content = story.Content;
            PrivacySettings = story.PrivacySettings;
            InteractionInfo = story.InteractionInfo;
            CanGetInteractions = story.CanGetInteractions;
            CanBeReplied = story.CanBeReplied;
            CanBeForwarded = story.CanBeForwarded;
            CanToggleIsPostedToChatPage = story.CanToggleIsPostedToChatPage;
            CanBeEdited = story.CanBeEdited;
            CanBeDeleted = story.CanBeDeleted;
            CanBeAddedToAlbum = story.CanBeAddedToAlbum;
            AlbumIds = story.AlbumIds.ToList();
            IsVisibleOnlyForSelf = story.IsVisibleOnlyForSelf;
            IsPostedToChatPage = story.IsPostedToChatPage;
            HasExpiredViewers = story.HasExpiredViewers;
            Areas = story.Areas;
            ChosenReactionType = story.ChosenReactionType;
        }

        public FormattedText Caption { get; private set; }

        public StoryContent Content { get; private set; }

        /// <summary>
        /// Privacy rules affecting story visibility; may be null if the story isn't owned.
        /// </summary>
        public StoryPrivacySettings PrivacySettings { get; private set; }

        /// <summary>
        /// Information about interactions with the story; may be null if the story isn't
        /// owned or there were no interactions.
        /// </summary>
        public StoryInteractionInfo InteractionInfo { get; private set; }

        /// <summary>
        /// True, users viewed the story can be received through getStoryViewers.
        /// </summary>
        public bool CanGetInteractions { get; private set; }

        /// <summary>
        /// True, if the story can be replied in the chat with the story sender.
        /// </summary>
        public bool CanBeReplied { get; private set; }

        /// <summary>
        /// True, if the story can be forwarded as a message. Otherwise, screenshots and
        /// saving of the story content must be also forbidden.
        /// </summary>
        public bool CanBeForwarded { get; private set; }

        /// <summary>
        /// True, if the story's IsPostedToChatPage value can be changed.
        /// </summary>
        public bool CanToggleIsPostedToChatPage { get; private set; }

        /// <summary>
        /// True, if the story can be edited.
        /// </summary>
        public bool CanBeEdited { get; private set; }

        /// <summary>
        /// True, if the story can be deleted.
        /// </summary>
        public bool CanBeDeleted { get; private set; }

        public bool CanBeAddedToAlbum { get; private set; }

        public List<int> AlbumIds { get; private set; }

        /// <summary>
        /// True, if the story is visible only for the current user.
        /// </summary>
        public bool IsVisibleOnlyForSelf { get; private set; }

        /// <summary>
        /// True, if the story is saved in the sender's profile and will be available there
        /// after expiration.
        /// </summary>
        public bool IsPostedToChatPage { get; private set; }

        /// <summary>
        /// True, if users viewed the story can't be received, because the story has expired
        /// more than getOption("story_viewers_expiration_delay") seconds ago.
        /// </summary>
        public bool HasExpiredViewers { get; private set; }

        /// <summary>
        /// Clickable areas to be shown on the story content.
        /// </summary>
        public Vector<StoryArea> Areas { get; private set; }

        /// <summary>
        /// Type of the chosen reaction; may be null if none.
        /// </summary>
        public ReactionType ChosenReactionType { get; private set; }

        public void Load()
        {
            _ = LoadAsync();
        }

        public void Prepare()
        {
            lock (_loadLock)
            {
                if (_load == null)
                {
                    // The load downloads the files once it has the story.
                    _load = LoadCoreAsync();
                    return;
                }
            }

            DownloadFiles();
        }

        private void DownloadFiles()
        {
            if (Content is StoryContentPhoto photo)
            {
                var file = photo.Photo.GetBig();
                if (file != null && file.Photo.Local.CanBeDownloaded && !file.Photo.Local.IsDownloadingCompleted)
                {
                    ClientService.DownloadFile(file.Photo.Id, 32);
                }

                var thumbnail = photo.Photo.GetSmall();
                if (thumbnail != null && thumbnail.Photo.Local.CanBeDownloaded && !thumbnail.Photo.Local.IsDownloadingCompleted)
                {
                    ClientService.DownloadFile(thumbnail.Photo.Id, 30);
                }
            }
            else if (Content is StoryContentVideo videoContent)
            {
                var video = SelectVideoFile(videoContent);

                var file = video.Video;
                if (file != null && file.Local.CanBeDownloaded && !file.Local.IsDownloadingCompleted)
                {
                    ClientService.DownloadFile(file.Id, 32, 0, video.PreloadPrefixSize);
                }

                var thumbnail = video.Thumbnail;
                if (thumbnail != null && thumbnail.File.Local.CanBeDownloaded && !thumbnail.File.Local.IsDownloadingCompleted)
                {
                    ClientService.DownloadFile(thumbnail.File.Id, 30);
                }
            }
        }

        private StoryVideo SelectVideoFile(StoryContentVideo video)
        {
            //if (video.AlternativeVideo == null || (AppSettings.Playback.HighQuality && ClientService.IsPremium))
            {
                return video.Video;
            }

            return video.AlternativeVideo;
        }

        public File GetFile()
        {
            return Content switch
            {
                StoryContentPhoto photo => photo.Photo.GetBig()?.Photo,
                StoryContentVideo video => video.Video.Video,
                _ => null
            };
        }
    }
}
