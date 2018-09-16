using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Converters;
using Unigram.Core.Common;
using Unigram.Entities;
using Unigram.Services;
using Unigram.ViewModels.Delegates;
using Unigram.Views.Channels;
using Unigram.Views.Supergroups;
using Windows.Storage;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels.Supergroups
{
    public class SupergroupEditViewModelBase : TLViewModelBase, IDelegable<ISupergroupDelegate>
    {
        public ISupergroupDelegate Delegate { get; set; }

        public SupergroupEditViewModelBase(IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(protoService, cacheService, settingsService, aggregator)
        {
            AdminedPublicChannels = new MvxObservableCollection<Chat>();

            SendCommand = new RelayCommand(SendExecute);
            RevokeLinkCommand = new RelayCommand<Chat>(RevokeLinkExecute);
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

        protected bool _isPublic = true;
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

        protected string _username;
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

        protected bool _hasTooMuchUsernames;
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

        protected string _inviteLink;
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

                if (string.IsNullOrEmpty(item.Username))
                {
                    LoadUsername(chat.Id);
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



        private async void LoadUsername(long chatId)
        {
            var response = await ProtoService.SendAsync(new CheckChatUsername(chatId, "username"));
            if (response is Ok)
            {
                HasTooMuchUsernames = false;
            }
            else if (response is CheckChatUsernameResultPublicChatsTooMuch)
            {
                HasTooMuchUsernames = true;
                LoadAdminedPublicChannels();
            }
        }

        protected async void LoadAdminedPublicChannels()
        {
            if (AdminedPublicChannels.Count > 0)
            {
                return;
            }

            var response = await ProtoService.SendAsync(new GetCreatedPublicChats());
            if (response is Telegram.Td.Api.Chats chats)
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

        public RelayCommand SendCommand { get; }
        protected virtual async void SendExecute()
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

                var username = _isPublic ? _username?.Trim() : string.Empty;

                if (!string.Equals(username, item.Username))
                {
                    var response = await ProtoService.SendAsync(new SetSupergroupUsername(item.Id, username));
                    if (response is Error error)
                    {
                        if (error.TypeEquals(ErrorType.CHANNELS_ADMIN_PUBLIC_TOO_MUCH))
                        {
                            HasTooMuchUsernames = true;
                            LoadAdminedPublicChannels();
                        }
                        // TODO:

                        return;
                    }
                }

                NavigationService.GoBack();
            }
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
                dialog.Title = Strings.Resources.AppName;
                dialog.Message = string.Format(Strings.Resources.RevokeLinkAlert, MeUrlPrefixConverter.Convert(CacheService, supergroup.Username, true), chat.Title);
                dialog.PrimaryButtonText = Strings.Resources.RevokeButton;
                dialog.SecondaryButtonText = Strings.Resources.Cancel;

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

                var response = await ProtoService.SendAsync(new CheckChatUsername(chat.Id, text));
                if (response is CheckChatUsernameResultOk)
                {
                    IsLoading = false;
                    IsAvailable = false;
                    ErrorMessage = Strings.Resources.UsernameInUse;
                }
                else if (response is CheckChatUsernameResultUsernameInvalid)
                {
                    IsLoading = false;
                    IsAvailable = false;
                    ErrorMessage = Strings.Resources.UsernameInvalid;
                }
                else if (response is CheckChatUsernameResultUsernameOccupied)
                {
                    IsLoading = false;
                    IsAvailable = false;
                    ErrorMessage = Strings.Resources.UsernameInUse;
                }
                else if (response is CheckChatUsernameResultPublicChatsTooMuch)
                {
                    HasTooMuchUsernames = true;
                    LoadAdminedPublicChannels();
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
                    ErrorMessage = Strings.Resources.UsernameInvalidShort;
                }
                else if (_username.Length > 32)
                {
                    ErrorMessage = Strings.Resources.UsernameInvalidLong;
                }
                else
                {
                    ErrorMessage = Strings.Resources.UsernameInvalid;
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
