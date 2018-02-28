using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using TdWindows;
using Telegram.Api;
using Telegram.Api.Helpers;
using Telegram.Api.Services;
using Telegram.Api.TL;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Core.Common;
using Unigram.Services;
using Unigram.Views.Channels;
using Unigram.Views.Supergroups;
using Windows.Storage;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels.Supergroups
{
    public class SupergroupEditViewModel : UnigramViewModelBase,
        IHandle<UpdateSupergroup>,
        IHandle<UpdateSupergroupFullInfo>,
        IHandle<UpdateChatTitle>,
        IHandle<UpdateChatPhoto>
    {
        public ISupergroupDelegate Delegate { get; set; }

        public SupergroupEditViewModel(IProtoService protoService, ICacheService cacheService, IEventAggregator aggregator)
            : base(protoService, cacheService, aggregator)
        {
            AdminedPublicChannels = new MvxObservableCollection<Chat>();

            SendCommand = new RelayCommand(SendExecute);
            EditPhotoCommand = new RelayCommand<StorageFile>(EditPhotoExecute);
            EditStickerSetCommand = new RelayCommand(EditStickerSetExecute);
            RevokeLinkCommand = new RelayCommand<Chat>(RevokeLinkExecute);
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

        private string _about;
        public string About
        {
            get
            {
                return _about;
            }
            set
            {
                Set(ref _about, value);
            }
        }

        private bool _isPublic = true;
        public bool IsPublic
        {
            get
            {
                return _isPublic;
            }
            set
            {
                Set(ref _isPublic, value);
            }
        }

        private bool _isDemocracy;
        public bool IsDemocracy
        {
            get
            {
                return _isDemocracy;
            }
            set
            {
                Set(ref _isDemocracy, value);
            }
        }

        private bool _isSignatures;
        public bool IsSignatures
        {
            get
            {
                return _isSignatures;
            }
            set
            {
                Set(ref _isSignatures, value);
            }
        }
        private bool _isAllHistoryAvailable;
        public bool IsAllHistoryAvailable
        {
            get
            {
                return _isAllHistoryAvailable;
            }
            set
            {
                Set(ref _isAllHistoryAvailable, value);
            }
        }

        private string _username;
        public string Username
        {
            get
            {
                return _username;
            }
            set
            {
                Set(ref _username, value);
            }
        }

        private bool _hasTooMuchUsernames;
        public bool HasTooMuchUsernames
        {
            get
            {
                return _hasTooMuchUsernames;
            }
            set
            {
                Set(ref _hasTooMuchUsernames, value);
            }
        }

        private string _inviteLink;
        public string InviteLink
        {
            get
            {
                return _inviteLink;
            }
            set
            {
                Set(ref _inviteLink, value);
            }
        }

        public MvxObservableCollection<Chat> AdminedPublicChannels { get; private set; }

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

            if (chat.Type is ChatTypeSupergroup super)
            {
                var item = ProtoService.GetSupergroup(super.SupergroupId);
                var cache = ProtoService.GetSupergroupFull(super.SupergroupId);

                Delegate?.UpdateSupergroup(chat, item);

                if (cache == null)
                {
                    ProtoService.Send(new GetSupergroupFullInfo(super.SupergroupId));
                }
                else
                {
                    Delegate?.UpdateSupergroupFullInfo(chat, item, cache);
                }
            }

            return Task.CompletedTask;
        }

        public void Handle(UpdateSupergroup update)
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            if (chat.Type is ChatTypeSupergroup super && super.SupergroupId == update.Supergroup.Id)
            {
                BeginOnUIThread(() => Delegate?.UpdateSupergroup(chat, update.Supergroup));
            }
        }

        public void Handle(UpdateSupergroupFullInfo update)
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            if (chat.Type is ChatTypeSupergroup super && super.SupergroupId == update.SupergroupId)
            {
                BeginOnUIThread(() => Delegate?.UpdateSupergroupFullInfo(chat, ProtoService.GetSupergroup(update.SupergroupId), update.SupergroupFullInfo));
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



        private async void LoadAdminedPublicChannels()
        {
            if (AdminedPublicChannels.Count > 0)
            {
                return;
            }

            var response = await ProtoService.SendAsync(new GetCreatedPublicChats());
            if (response is TdWindows.Chats chats)
            {
                var result = new List<Chat>();

                foreach (var id in chats.ChatIds)
                {
                    var chat = ProtoService.GetChat(id);
                    if (chat != null)
                    {
                        result.Add(chat);
                    }
                }

                AdminedPublicChannels.ReplaceWith(result);
            }
            else if (response is Error error)
            {
                Execute.ShowDebugMessage("channels.getAdminedPublicChannels error " + error);
            }
        }

        private async void ExportInvite()
        {
            //if (_item == null || _inviteLink != null)
            //{
            //    return;
            //}

            //var response = await LegacyService.ExportInviteAsync(_item.ToInputChannel());
            //if (response.IsSucceeded && response.Result is TLChatInviteExported invite)
            //{
            //    if (_full != null)
            //    {
            //        _full.ExportedInvite = response.Result;
            //        _full.RaisePropertyChanged(() => _full.ExportedInvite);
            //    }

            //    InviteLink = invite.Link;
            //}
            //else
            //{
            //    Execute.ShowDebugMessage("channels.ExportInvite error " + response.Error);
            //}
        }

        public RelayCommand SendCommand { get; }
        private async void SendExecute()
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            if (chat.Type is ChatTypeSupergroup supergroup)
            {
                var item = ProtoService.GetSupergroup(supergroup.SupergroupId);
                var cache = ProtoService.GetSupergroupFull(supergroup.SupergroupId);

                if (item == null || cache == null)
                {
                    return;
                }

                var about = _about.Format();
                var title = _title.Trim();
                var username = _isPublic ? _username?.Trim() : string.Empty;

                if (!string.Equals(username, item.Username))
                {
                    var response = await ProtoService.SendAsync(new SetSupergroupUsername(item.Id, username));
                    if (response is Error error)
                    {
                        if (error.TypeEquals(TLErrorType.CHANNELS_ADMIN_PUBLIC_TOO_MUCH))
                        {
                            HasTooMuchUsernames = true;
                            LoadAdminedPublicChannels();
                        }
                        // TODO:

                        return;
                    }
                }

                if (!string.Equals(title, chat.Title))
                {
                    var response = await ProtoService.SendAsync(new SetChatTitle(chat.Id, title));
                    if (response is Error)
                    {
                        // TODO:
                    }
                }

                if (!string.Equals(about, cache.Description))
                {
                    var response = await ProtoService.SendAsync(new SetSupergroupDescription(item.Id, about));
                    if (response is Error)
                    {
                        // TODO:
                    }
                }

                if (_isDemocracy != item.AnyoneCanInvite)
                {
                    var response = await ProtoService.SendAsync(new ToggleSupergroupInvites(item.Id, _isDemocracy));
                    if (response is Error)
                    {
                        // TODO:
                    }
                }

                if (_isSignatures != item.SignMessages)
                {
                    var response = await ProtoService.SendAsync(new ToggleSupergroupSignMessages(item.Id, _isSignatures));
                    if (response is Error)
                    {
                        // TODO:
                    }
                }

                if (_isAllHistoryAvailable != cache.IsAllHistoryAvailable)
                {
                    var response = await ProtoService.SendAsync(new ToggleSupergroupIsAllHistoryAvailable(item.Id, _isAllHistoryAvailable));
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

        public RelayCommand EditStickerSetCommand { get; }
        private void EditStickerSetExecute()
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            NavigationService.Navigate(typeof(SupergroupEditStickerSetPage), chat.Id);
        }

        public RelayCommand<Chat> RevokeLinkCommand { get; }
        private async void RevokeLinkExecute(Chat chat)
        {
            if (chat.Type is ChatTypeSupergroup super)
            {
                var supergroup = ProtoService.GetSupergroup(super.SupergroupId);
                if (supergroup == null)
                {
                    return;
                }

                var dialog = new TLMessageDialog();
                dialog.Title = Strings.Android.AppName;
                dialog.Message = string.Format(Strings.Android.RevokeLinkAlert, supergroup.Username, chat.Title);
                dialog.PrimaryButtonText = Strings.Android.RevokeButton;
                dialog.SecondaryButtonText = Strings.Android.Cancel;

                var confirm = await dialog.ShowQueuedAsync();
                if (confirm == ContentDialogResult.Primary)
                {
                    var response = await ProtoService.SendAsync(new SetSupergroupUsername(supergroup.Id, string.Empty));
                    if (response is Ok)
                    {
                        HasTooMuchUsernames = false;
                        AdminedPublicChannels.Clear();
                    }
                }
            }
        }

        public RelayCommand DeleteCommand { get; }
        private async void DeleteExecute()
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            if (chat.Type is ChatTypeSupergroup super)
            {
                var message = super.IsChannel ? Strings.Android.ChannelDeleteAlert : Strings.Android.MegaDeleteAlert;
                var confirm = await TLMessageDialog.ShowAsync(message, Strings.Android.AppName, Strings.Android.OK, Strings.Android.Cancel);
                if (confirm == ContentDialogResult.Primary)
                {
                    var response = await ProtoService.SendAsync(new DeleteSupergroup(super.SupergroupId));
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

        #region Username

        private bool _isValid;
        public bool IsValid
        {
            get
            {
                return _isValid;
            }
            set
            {
                Set(ref _isValid, value);
            }
        }

        private bool _isAvailable;
        public bool IsAvailable
        {
            get
            {
                return _isAvailable;
            }
            set
            {
                Set(ref _isAvailable, value);
            }
        }

        private string _errorMessage;
        public string ErrorMessage
        {
            get
            {
                return _errorMessage;
            }
            set
            {
                Set(ref _errorMessage, value);
            }
        }

        public async void CheckAvailability(string text)
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            if (chat.Type is ChatTypeSupergroup supergroup)
            {
                var item = ProtoService.GetSupergroup(supergroup.SupergroupId);
                if (item == null)
                {
                    return;
                }

                if (string.Equals(text, item?.Username))
                {
                    IsLoading = false;
                    IsAvailable = false;
                    ErrorMessage = null;

                    return;
                }

                var response = await ProtoService.SendAsync(new SearchPublicChat(text));
                if (response is Chat result)
                {
                    if (result.Type is ChatTypeSupergroup check && check.SupergroupId == supergroup.SupergroupId)
                    {
                        IsLoading = false;
                        IsAvailable = true;
                        ErrorMessage = null;
                    }
                    else
                    {
                        IsLoading = false;
                        IsAvailable = false;
                        ErrorMessage = Strings.Android.UsernameInUse;
                    }
                }
                else if (response is Error error)
                {
                    if (error.TypeEquals(TLErrorType.USERNAME_INVALID))
                    {
                        IsLoading = false;
                        IsAvailable = false;
                        ErrorMessage = Strings.Android.UsernameInvalid;
                    }
                    else if (error.TypeEquals(TLErrorType.USERNAME_OCCUPIED))
                    {
                        IsLoading = false;
                        IsAvailable = false;
                        ErrorMessage = Strings.Android.UsernameInUse;
                    }
                    else if (error.TypeEquals(TLErrorType.USERNAME_NOT_OCCUPIED))
                    {
                        IsLoading = false;
                        IsAvailable = true;
                        ErrorMessage = null;
                    }
                    else if (error.TypeEquals(TLErrorType.CHANNELS_ADMIN_PUBLIC_TOO_MUCH))
                    {
                        HasTooMuchUsernames = true;
                        LoadAdminedPublicChannels();
                    }
                }
            }
        }

        public bool UpdateIsValid(string username)
        {
            IsValid = IsValidUsername(username);
            IsLoading = false;
            IsAvailable = false;

            if (!IsValid)
            {
                if (string.IsNullOrEmpty(username))
                {
                    ErrorMessage = null;
                }
                else if (_username.Length < 5)
                {
                    ErrorMessage = Strings.Android.UsernameInvalidShort;
                }
                else if (_username.Length > 32)
                {
                    ErrorMessage = Strings.Android.UsernameInvalidLong;
                }
                else
                {
                    ErrorMessage = Strings.Android.UsernameInvalid;
                }
            }
            else
            {
                IsLoading = true;
                ErrorMessage = null;
            }

            return IsValid;
        }

        public bool IsValidUsername(string username)
        {
            if (string.IsNullOrEmpty(username))
            {
                return false;
            }

            if (username.Length < 5)
            {
                return false;
            }

            if (username.Length > 32)
            {
                return false;
            }

            for (int i = 0; i < username.Length; i++)
            {
                if (!MessageHelper.IsValidUsernameSymbol(username[i]))
                {
                    return false;
                }
            }

            return true;
        }

        #endregion
    }
}
