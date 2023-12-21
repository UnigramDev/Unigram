//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System.Linq;
using Telegram.Collections;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.ViewModels.Gallery;
using Windows.UI.Xaml.Controls;

namespace Telegram.ViewModels.Chats
{
    public class ChatPhotosViewModel : GalleryViewModelBase
    {
        private readonly DisposableMutex _loadMoreLock = new DisposableMutex();
        private readonly Chat _chat;

        public ChatPhotosViewModel(IClientService clientService, IStorageService storageService, IEventAggregator aggregator, Chat chat, ChatPhoto photo)
            : base(clientService, storageService, aggregator)
        {
            _chat = chat;
            Items = new MvxObservableCollection<GalleryMedia> { new GalleryChatPhoto(clientService, chat, photo, 0) };
            SelectedItem = Items[0];
            FirstItem = Items[0];

            Initialize(photo.GetBig().Photo.Id, photo.GetSmall().Photo.Id);
        }

        private async void Initialize(long bigId, long smallId)
        {
            using (await _loadMoreLock.WaitAsync())
            {
                var limit = 20;
                var offset = -limit / 2;

                var response = await ClientService.SendAsync(new SearchChatMessages(_chat.Id, string.Empty, null, 0, offset, limit, new SearchMessagesFilterChatPhoto(), 0));
                if (response is FoundChatMessages messages)
                {
                    TotalItems = messages.TotalCount;

                    var missing = true;

                    foreach (var message in messages.Messages.OrderByDescending(x => x.Id))
                    {
                        if (message.Content is MessageChatChangePhoto chatChangePhoto)
                        {
                            if (chatChangePhoto.Photo.Sizes.Any(x => x.Photo.Id == bigId))
                            {
                                missing = false;
                                continue;
                            }

                            Items.Add(new GalleryChatPhoto(ClientService, _chat, chatChangePhoto.Photo, message.Id));
                        }
                        else
                        {
                            TotalItems--;
                        }
                    }

                    if (missing)
                    {
                        TotalItems++;
                    }

                    OnSelectedItemChanged(_selectedItem);
                }
            }
        }

        protected override async void LoadPrevious()
        {
            //using (await _loadMoreLock.WaitAsync())
            //{
            //    var item = Items.FirstOrDefault() as GalleryMessageItem;
            //    if (item == null)
            //    {
            //        return;
            //    }

            //    var fromMessageId = item.Id;

            //    var limit = 20;
            //    var offset = -limit / 2;

            //    var response = await ClientService.SendAsync(new SearchChatMessages(_chatId, string.Empty, 0, fromMessageId, offset, limit, new SearchMessagesFilterChatPhoto()));
            //    if (response is Telegram.Td.Api.Messages messages)
            //    {
            //        TotalItems = messages.TotalCount;

            //        foreach (var message in messages.MessagesValue.Where(x => x.Id < fromMessageId))
            //        {
            //            if (message.Content is MessageChatChangePhoto)
            //            {
            //                Items.Insert(0, new GalleryMessageItem(ClientService, message));
            //            }
            //            else
            //            {
            //                TotalItems--;
            //            }
            //        }

            //        OnSelectedItemChanged(_selectedItem);
            //    }
            //}
        }

        protected override async void LoadNext()
        {
            //using (await _loadMoreLock.WaitAsync())
            //{
            //    var item = Items.LastOrDefault() as GalleryMessageItem;
            //    if (item == null)
            //    {
            //        return;
            //    }

            //    var fromMessageId = item.Id;

            //    var limit = 20;
            //    var offset = -limit / 2;

            //    var response = await ClientService.SendAsync(new SearchChatMessages(_chatId, string.Empty, 0, fromMessageId, offset, limit, new SearchMessagesFilterChatPhoto()));
            //    if (response is Telegram.Td.Api.Messages messages)
            //    {
            //        TotalItems = messages.TotalCount;

            //        foreach (var message in messages.MessagesValue.Where(x => x.Id > fromMessageId).OrderBy(x => x.Id))
            //        {
            //            if (message.Content is MessageChatChangePhoto)
            //            {
            //                Items.Add(new GalleryMessageItem(ClientService, message));
            //            }
            //            else
            //            {
            //                TotalItems--;
            //            }
            //        }

            //        OnSelectedItemChanged(_selectedItem);
            //    }
            //}
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

        public override int Position => TotalItems - (Items.Count - base.Position);

        public override MvxObservableCollection<GalleryMedia> Group => Items;

        public void SetAsMain()
        {
            var item = _selectedItem as GalleryChatPhoto;
            if (item == null)
            {
                return;
            }

            ClientService.Send(new SetChatPhoto(_chat.Id, new InputChatPhotoPrevious(item.Id)));
            ToastPopup.Show(_chat.Type is ChatTypeSupergroup supergroup && supergroup.IsChannel
                ? item.IsVideo ? Strings.MainChannelProfileVideoSetHint : Strings.MainChannelProfilePhotoSetHint
                : item.IsVideo ? Strings.MainGroupProfileVideoSetHint : Strings.MainGroupProfilePhotoSetHint);
        }

    }

    //public class GalleryChatPhotoItem : GalleryItem
    //{
    //    private readonly TLChatPhoto _photo;
    //    private readonly ITLDialogWith _from;
    //    private readonly string _caption;

    //    public GalleryChatPhotoItem(TLChatPhoto photo, ITLDialogWith from)
    //    {
    //        _photo = photo;
    //        _from = from;
    //    }

    //    public override object Source => _photo;

    //    public override string Caption => _caption;

    //    public override ITLDialogWith From => _from;

    //    public override int Date => _photo.Date;

    //    public override bool HasStickers => _photo.IsHasStickers;

    //    public override TLInputStickeredMediaBase ToInputStickeredMedia()
    //    {
    //        return new TLInputStickeredMediaPhoto { Id = _photo.ToInputPhoto() };
    //    }
    //}

}
