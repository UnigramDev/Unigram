//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System.Linq;
using System.Threading.Tasks;
using Telegram.Collections;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.ViewModels.Gallery;
using Windows.UI.Xaml.Controls;

namespace Telegram.ViewModels.Chats
{
    public partial class ChatPhotosViewModel : GalleryViewModelBase
    {
        private const int LoadLimit = 20;

        private readonly Chat _chat;

        // Id of the big size of the photo the gallery opened on. It is handed to us rather than
        // found in the history, so the message that set it has to be recognised and skipped.
        private readonly long _photoId;

        // searchChatMessages cursor, and the only direction this gallery pages in: Items[0] is
        // the chat's current photo, so there is never anything before it.
        private long _nextFromMessageId;

        // Messages the server counts among the results that the gallery does not show, plus the
        // current photo when the history holds no message for it. TotalCount comes back fresh
        // with every page, so the running correction has to be kept beside it rather than
        // folded into TotalItems.
        private int _totalCountDelta;

        private bool _initialized;

        public ChatPhotosViewModel(IClientService clientService, IStorageService storageService, IEventAggregator aggregator, Chat chat, ChatPhoto photo)
            : base(clientService, storageService, aggregator)
        {
            _chat = chat;
            _photoId = photo.GetBig().Photo.Id;

            Items = new RangeObservableCollection<GalleryMedia> { new GalleryChatPhoto(clientService, chat, photo, 0) };
            SelectedItem = Items[0];
            FirstItem = Items[0];

            Initialize();
        }

        private async void Initialize()
        {
            IsLoading = true;

            try
            {
                await LoadAsync();
            }
            finally
            {
                IsLoading = false;
            }
        }

        protected override Task<IncrementalLoadResult> LoadNextAsync()
        {
            return LoadAsync();
        }

        private async Task<IncrementalLoadResult> LoadAsync()
        {
            // A zero cursor asks from the newest match, so the first page and every one after it
            // are the same request.
            var response = await ClientService.SendAsync(new SearchChatMessages(_chat.Id, null, string.Empty, null, _nextFromMessageId, 0, LoadLimit, new SearchMessagesFilterChatPhoto()));
            if (response is not FoundChatMessages messages)
            {
                return new IncrementalLoadResult(0, false);
            }

            var count = 0;
            var found = false;

            foreach (var message in messages.Messages.OrderByDescending(x => x.Id))
            {
                if (message.Content is not MessageChatChangePhoto chatChangePhoto)
                {
                    _totalCountDelta--;
                    continue;
                }

                if (chatChangePhoto.Photo.Sizes.Any(x => x.Photo.Id == _photoId))
                {
                    found = true;
                    continue;
                }

                Items.Add(new GalleryChatPhoto(ClientService, _chat, chatChangePhoto.Photo, message.Id));
                count++;
            }

            _nextFromMessageId = messages.NextFromMessageId;

            // The current photo is the newest match, so the first page settles whether the
            // history has a message for it. Without one it is an item the server never counted.
            if (!_initialized)
            {
                _initialized = true;

                if (!found)
                {
                    _totalCountDelta++;
                }
            }

            TotalItems = messages.TotalCount + _totalCountDelta;

            OnSelectedItemChanged(_selectedItem);

            return new IncrementalLoadResult((uint)count, _nextFromMessageId != 0);
        }

        public override bool CanDelete
        {
            get
            {
                var chat = _chat;
                if (chat != null && ClientService.TryGetSupergroup(chat, out Supergroup supergroup))
                {
                    if (supergroup.Status is ChatMemberStatusCreator || supergroup.Status is ChatMemberStatusAdministrator administrator && administrator.Rights.CanChangeInfo)
                    {
                        return true;
                    }

                    return supergroup.Status is ChatMemberStatusMember && chat.Permissions.CanChangeInfo;
                }
                else if (chat != null && ClientService.TryGetBasicGroup(chat, out BasicGroup basicGroup))
                {
                    if (basicGroup.Status is ChatMemberStatusCreator || basicGroup.Status is ChatMemberStatusAdministrator administrator && administrator.Rights.CanChangeInfo)
                    {
                        return true;
                    }

                    return basicGroup.Status is ChatMemberStatusMember && chat.Permissions.CanChangeInfo;
                }

                return false;
            }
        }

        public override async void Delete()
        {
            var confirm = await ShowPopupAsync(Strings.AreYouSureDeletePhoto, Strings.AppName, Strings.OK, Strings.Cancel);
            if (confirm == ContentDialogResult.Primary && _selectedItem is GalleryChatPhoto chatPhoto)
            {
                Function function;
                if (chatPhoto.MessageId == 0)
                {
                    function = new SetChatPhoto(_chat.Id, null);
                }
                else
                {
                    function = new DeleteMessages(_chat.Id, new[] { chatPhoto.MessageId }, true);
                }

                var response = await ClientService.SendAsync(function);
                if (response is Ok)
                {
                    var index = Items.IndexOf(chatPhoto);
                    if (index < Items.Count - 1 && chatPhoto.MessageId != 0)
                    {
                        SelectedItem = Items[index > 0 ? index - 1 : index + 1];
                        Items.Remove(chatPhoto);
                        TotalItems--;
                    }
                    else
                    {
                        NavigationService.GoBack();
                    }
                }
            }
        }

        public override RangeObservableCollection<GalleryMedia> Group => Items;

        public void SetAsMain()
        {
            var item = _selectedItem as GalleryChatPhoto;
            if (item == null)
            {
                return;
            }

            ClientService.Send(new SetChatPhoto(_chat.Id, new InputChatPhotoPrevious(item.Id)));
            ShowToast(_chat.Type is ChatTypeSupergroup supergroup && supergroup.IsChannel
                ? item.IsVideo ? Strings.MainChannelProfileVideoSetHint : Strings.MainChannelProfilePhotoSetHint
                : item.IsVideo ? Strings.MainGroupProfileVideoSetHint : Strings.MainGroupProfilePhotoSetHint);
        }
    }
}
