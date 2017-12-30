using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Aggregator;
using Telegram.Api.Helpers;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Telegram.Api.Services.Cache.EventArgs;
using Telegram.Api.Services.FileManager;
using Telegram.Api.TL;
using Unigram.Collections;
using Unigram.Common;
using Unigram.Controls;
using Windows.Storage;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels.Chats
{
    public class ChatEditViewModel : ChatDetailsViewModel
    {
        private readonly IUploadFileManager _uploadFileManager;

        public ChatEditViewModel(IMTProtoService protoService, ICacheService cacheService, ITelegramEventAggregator aggregator, IUploadFileManager uploadFileManager)
            : base(protoService, cacheService, aggregator)
        {
            _uploadFileManager = uploadFileManager;

            SendCommand = new RelayCommand(SendExecute);
            EditPhotoCommand = new RelayCommand<StorageFile>(EditPhotoExecute);
            DeleteCommand = new RelayCommand(DeleteExecute);
        }

        private string _title;
        public string Title
        {
            get
            {
                return _title;
            }
            set
            {
                Set(ref _title, value);
            }
        }

        public override Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            // SHOULD NOT CALL BASE!

            Item = null;
            Full = null;

            var chat = parameter as TLChat;
            var peer = parameter as TLPeerChat;
            if (peer != null)
            {
                chat = CacheService.GetChat(peer.ChatId) as TLChat;
            }

            if (chat != null)
            {
                Item = chat;
                Title = chat.Title;
            }

            return Task.CompletedTask;
        }

        public RelayCommand SendCommand { get; }
        private async void SendExecute()
        {
            var title = _title.Trim();

            if (_item != null && !string.Equals(title, _item.Title))
            {
                var response = await ProtoService.EditChatTitleAsync(_item.Id, title);
                if (response.IsSucceeded)
                {
                    _item.Title = title;
                    _item.RaisePropertyChanged(() => _item.Title);
                }
                else
                {
                    // TODO:
                    return;
                }
            }

            NavigationService.GoBack();
        }

        public RelayCommand<StorageFile> EditPhotoCommand { get; }
        private async void EditPhotoExecute(StorageFile file)
        {
            var fileLocation = new TLFileLocation
            {
                VolumeId = TLLong.Random(),
                LocalId = TLInt.Random(),
                Secret = TLLong.Random(),
                DCId = 0
            };

            var fileName = string.Format("{0}_{1}_{2}.jpg", fileLocation.VolumeId, fileLocation.LocalId, fileLocation.Secret);
            var fileCache = await FileUtils.CreateTempFileAsync(fileName);

            await file.CopyAndReplaceAsync(fileCache);
            var fileScale = fileCache;

            var basicProps = await fileScale.GetBasicPropertiesAsync();
            var imageProps = await fileScale.Properties.GetImagePropertiesAsync();

            var fileId = TLLong.Random();
            var upload = await _uploadFileManager.UploadFileAsync(fileId, fileCache.Name);
            if (upload != null)
            {
                var response = await ProtoService.EditChatPhotoAsync(_item.Id, new TLInputChatUploadedPhoto { File = upload.ToInputFile() });
                if (response.IsSucceeded)
                {

                }
            }
        }

        public RelayCommand DeleteCommand { get; }
        private async void DeleteExecute()
        {
            var item = _item;
            if (item == null)
            {
                return;
            }

            var confirm = await TLMessageDialog.ShowAsync(Strings.Android.AreYouSureDeleteAndExit, Strings.Android.AppName, Strings.Android.OK, Strings.Android.Cancel);
            if (confirm == ContentDialogResult.Primary)
            {
                if (item.IsLeft || item.HasMigratedTo)
                {
                    goto Skip;
                }

                var delete = await ProtoService.DeleteChatUserAsync(item.Id, new TLInputUserSelf());
                if (delete.IsSucceeded)
                {

                }
                else
                {
                    await new TLMessageDialog(delete.Error.ErrorMessage ?? "Error message", delete.Error.ErrorCode.ToString()).ShowQueuedAsync();
                    return;
                }

                Skip:
                var peer = item.ToInputPeer();
                var offset = 0;

                do
                {
                    var response = await ProtoService.DeleteHistoryAsync(false, peer, 0);
                    if (response.IsSucceeded)
                    {
                        offset = response.Result.Offset;
                    }
                    else
                    {
                        await new TLMessageDialog(response.Error.ErrorMessage ?? "Error message", response.Error.ErrorCode.ToString()).ShowQueuedAsync();
                        return;
                    }
                }
                while (offset > 0);

                var dialog = CacheService.GetDialog(item.ToPeer());
                if (dialog != null)
                {
                    CacheService.DeleteDialog(dialog);
                    Aggregator.Publish(new DialogRemovedEventArgs(dialog));
                }

                NavigationService.RemovePeerFromStack(item.ToPeer());
            }
        }
    }
}
