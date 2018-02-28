using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TdWindows;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Services;
using Windows.Storage;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels.BasicGroups
{
    public class BasicGroupEditViewModel : UnigramViewModelBase,
        IHandle<UpdateBasicGroup>,
        IHandle<UpdateBasicGroupFullInfo>,
        IHandle<UpdateChatTitle>,
        IHandle<UpdateChatPhoto>
    {
        public IBasicGroupDelegate Delegate { get; set; }

        public BasicGroupEditViewModel(IProtoService protoService, ICacheService cacheService, IEventAggregator aggregator)
            : base(protoService, cacheService, aggregator)
        {
            SendCommand = new RelayCommand(SendExecute);
            EditPhotoCommand = new RelayCommand<StorageFile>(EditPhotoExecute);
            DeleteCommand = new RelayCommand(DeleteExecute);
        }

        protected Chat _chat;
        public Chat Chat
        {
            get
            {
                return _chat;
            }
            set
            {
                Set(ref _chat, value);
            }
        }

        private StorageFile _photo;

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
            var chatId = (long)parameter;

            Chat = ProtoService.GetChat(chatId);

            var chat = _chat;
            if (chat == null)
            {
                return Task.CompletedTask;
            }

            Aggregator.Subscribe(this);
            Delegate?.UpdateChat(chat);

            if (chat.Type is ChatTypeBasicGroup basic)
            {
                var item = ProtoService.GetBasicGroup(basic.BasicGroupId);
                var cache = ProtoService.GetBasicGroupFull(basic.BasicGroupId);

                Delegate?.UpdateBasicGroup(chat, item);

                if (cache == null)
                {
                    ProtoService.Send(new GetBasicGroupFullInfo(basic.BasicGroupId));
                }
                else
                {
                    Delegate?.UpdateBasicGroupFullInfo(chat, item, cache);
                }
            }

            return Task.CompletedTask;
        }

        public void Handle(UpdateBasicGroup update)
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            if (chat.Type is ChatTypeBasicGroup basic && basic.BasicGroupId == update.BasicGroup.Id)
            {
                BeginOnUIThread(() => Delegate?.UpdateBasicGroup(chat, update.BasicGroup));
            }
        }

        public void Handle(UpdateBasicGroupFullInfo update)
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            if (chat.Type is ChatTypeBasicGroup basic && basic.BasicGroupId == update.BasicGroupId)
            {
                BeginOnUIThread(() => Delegate?.UpdateBasicGroupFullInfo(chat, ProtoService.GetBasicGroup(update.BasicGroupId), update.BasicGroupFullInfo));
            }
        }



        public void Handle(UpdateChatTitle update)
        {
            if (update.ChatId == _chat?.Id)
            {
                BeginOnUIThread(() => Delegate?.UpdateChatTitle(_chat));
            }
        }

        public void Handle(UpdateChatPhoto update)
        {
            if (update.ChatId == _chat?.Id)
            {
                BeginOnUIThread(() => Delegate?.UpdateChatPhoto(_chat));
            }
        }

        public RelayCommand SendCommand { get; }
        private async void SendExecute()
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            if (chat.Type is ChatTypeBasicGroup basic)
            {
                var item = ProtoService.GetSupergroup(basic.BasicGroupId);
                var cache = ProtoService.GetSupergroupFull(basic.BasicGroupId);

                if (item == null || cache == null)
                {
                    return;
                }

                var title = _title.Trim();

                if (!string.Equals(title, chat.Title))
                {
                    var response = await ProtoService.SendAsync(new SetChatTitle(chat.Id, title));
                    if (response is Error)
                    {
                        // TODO:
                    }
                }

                if (_photo != null)
                {
                    var response = await ProtoService.SendAsync(new SetChatPhoto(chat.Id, await _photo.ToGeneratedAsync()));
                    if (response is Error)
                    {
                        // TODO:
                    }
                }

                NavigationService.GoBack();
            }
        }

        public RelayCommand<StorageFile> EditPhotoCommand { get; }
        private async void EditPhotoExecute(StorageFile file)
        {
            _photo = file;
        }

        public RelayCommand DeleteCommand { get; }
        private async void DeleteExecute()
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            if (chat.Type is ChatTypeBasicGroup basic)
            {
                var message = Strings.Resources.MegaDeleteAlert;
                var confirm = await TLMessageDialog.ShowAsync(message, Strings.Resources.AppName, Strings.Resources.OK, Strings.Resources.Cancel);
                if (confirm == ContentDialogResult.Primary)
                {
                    await ProtoService.SendAsync(new SetChatMemberStatus(chat.Id, ProtoService.GetMyId(), new ChatMemberStatusLeft()));

                    var response = await ProtoService.SendAsync(new DeleteChatHistory(chat.Id, true));
                    if (response is Ok)
                    {
                        NavigationService.RemovePeerFromStack(chat.Id);
                    }
                    else if (response is Error error)
                    {
                        // TODO: ...
                    }
                }
            }
        }
    }
}
