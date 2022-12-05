using Rg.DiffUtils;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Navigation.Services;
using Unigram.Services;
using Unigram.ViewModels.Delegates;
using Unigram.ViewModels.Settings;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels.Supergroups
{
    public abstract class SupergroupEditViewModelBase : TLViewModelBase
        , IDelegable<ISupergroupEditDelegate>
        , IHandle
    //IHandle<UpdateSupergroup>,
    //IHandle<UpdateSupergroupFullInfo>,
    //IHandle<UpdateBasicGroup>,
    //IHandle<UpdateBasicGroupFullInfo>
    {
        public ISupergroupEditDelegate Delegate { get; set; }

        public SupergroupEditViewModelBase(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
            _username = new DebouncedProperty<string>(Constants.TypingTimeout, CheckAvailability, UpdateIsValid);
            Items = new DiffObservableCollection<UsernameInfo>(new UsernameInfoDiffHandler(), new DiffOptions { AllowBatching = false, DetectMoves = true });

            SendCommand = new RelayCommand(SendExecute);
        }

        protected Chat _chat;
        public Chat Chat
        {
            get => _chat;
            set => Set(ref _chat, value);
        }

        protected bool _isPublic = true;
        public bool IsPublic
        {
            get => _isPublic;
            set => SetIsPublic(value);
        }

        private void SetIsPublic(bool value)
        {
            if (value && HasTooMuchUsernames)
            {
                NavigationService.ShowLimitReached(new PremiumLimitTypeCreatedPublicChatCount());
                RaisePropertyChanged(nameof(IsPublic));
            }
            else
            {
                if (ClientService.TryGetSupergroup(_chat, out Supergroup supergroup))
                {
                    UpdateUsernames(supergroup.Usernames, Username);
                }

                Set(ref _isPublic, value, nameof(IsPublic));
            }
        }

        private readonly DebouncedProperty<string> _username;
        public string Username
        {
            get => _username;
            set => _username.Set(value);
        }

        protected bool _hasTooMuchUsernames;
        public bool HasTooMuchUsernames
        {
            get => _hasTooMuchUsernames;
            set => Set(ref _hasTooMuchUsernames, value);
        }

        protected string _inviteLink;
        public string InviteLink
        {
            get => _inviteLink;
            set => Set(ref _inviteLink, value);
        }

        public DiffObservableCollection<UsernameInfo> Items { get; private set; }

        protected override Task OnNavigatedToAsync(object parameter, NavigationMode mode, NavigationState state)
        {
            var chatId = (long)parameter;

            Chat = ClientService.GetChat(chatId);

            var chat = _chat;
            if (chat == null)
            {
                return Task.CompletedTask;
            }

            Delegate?.UpdateChat(chat);

            if (chat.Type is ChatTypeSupergroup super)
            {
                var item = ClientService.GetSupergroup(super.SupergroupId);
                var cache = ClientService.GetSupergroupFull(super.SupergroupId);

                UpdateSupergroup(chat, item);

                if (cache == null)
                {
                    ClientService.Send(new GetSupergroupFullInfo(super.SupergroupId));
                }
                else
                {
                    Delegate?.UpdateSupergroupFullInfo(chat, item, cache);
                }

                if (!item.HasEditableUsername())
                {
                    LoadUsername(chat.Id);
                }
            }
            else if (chat.Type is ChatTypeBasicGroup basic)
            {
                var item = ClientService.GetBasicGroup(basic.BasicGroupId);
                var cache = ClientService.GetBasicGroupFull(basic.BasicGroupId);

                Delegate?.UpdateBasicGroup(chat, item);

                if (cache == null)
                {
                    ClientService.Send(new GetBasicGroupFullInfo(basic.BasicGroupId));
                }
                else
                {
                    Delegate?.UpdateBasicGroupFullInfo(chat, item, cache);
                }

                LoadUsername(0);
            }

            return Task.CompletedTask;
        }

        public override void Subscribe()
        {
            Aggregator.Subscribe<UpdateSupergroup>(this, Handle)
                .Subscribe<UpdateSupergroupFullInfo>(Handle)
                .Subscribe<UpdateBasicGroup>(Handle)
                .Subscribe<UpdateBasicGroupFullInfo>(Handle);
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
                BeginOnUIThread(() => UpdateSupergroup(chat, update.Supergroup));
            }
        }

        protected virtual void UpdateSupergroup(Chat chat, Supergroup supergroup)
        {
            Delegate?.UpdateSupergroup(chat, supergroup);
            UpdateUsernames(supergroup.Usernames, Username);
        }

        private void UpdateUsernames(Usernames usernames, string editable = null)
        {
            if (IsPublic == false)
            {
                Items.Clear();
                return;
            }

            if (editable != null)
            {
                static void ReplaceEditable(IList<string> usernames, string original, string editable)
                {
                    for (int i = 0; i < usernames.Count; i++)
                    {
                        if (usernames[i] == original)
                        {
                            usernames[i] = editable;
                            break;
                        }
                    }
                }

                usernames ??= new Usernames
                {
                    ActiveUsernames = Array.Empty<string>(),
                    DisabledUsernames = Array.Empty<string>(),
                    EditableUsername = string.Empty
                };

                ReplaceEditable(usernames.ActiveUsernames, usernames.EditableUsername, editable);
                ReplaceEditable(usernames.DisabledUsernames, usernames.EditableUsername, editable);

                usernames.EditableUsername = editable;
            }

            if (usernames?.ActiveUsernames.Count + usernames?.DisabledUsernames.Count > 1)
            {
                Items.ReplaceDiff(UsernameInfo.FromUsernames(ClientService, usernames, false));
            }
            else if (Items.Count > 0)
            {
                Items.Clear();
            }
        }

        public void ReorderUsernames(UsernameInfo username)
        {
            if (ClientService.TryGetSupergroup(_chat, out Supergroup supergroup))
            {
                var active = supergroup.Usernames?.ActiveUsernames;

                var index = Items.IndexOf(username);
                if (index >= active.Count)
                {
                    UpdateUsernames(supergroup.Usernames, Username);
                }
                else
                {
                    var order = new List<string>();

                    foreach (var info in Items)
                    {
                        if (info.IsActive || info.IsEditable)
                        {
                            order.Add(info.Value);
                        }
                    }

                    ClientService.Send(new ReorderSupergroupActiveUsernames(supergroup.Id, order));
                }
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
                BeginOnUIThread(() => Delegate?.UpdateSupergroupFullInfo(chat, ClientService.GetSupergroup(update.SupergroupId), update.SupergroupFullInfo));
            }
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
                BeginOnUIThread(() => Delegate?.UpdateBasicGroupFullInfo(chat, ClientService.GetBasicGroup(update.BasicGroupId), update.BasicGroupFullInfo));
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
            var response = await ClientService.SendAsync(new CheckChatUsername(chatId, "username"));
            HasTooMuchUsernames = response is CheckChatUsernameResultPublicChatsTooMany;
        }

        public RelayCommand SendCommand { get; }
        protected abstract void SendExecute();

        public void ToggleUsername(UsernameInfo username)
        {
            ClientService.Send(new ToggleUsernameIsActive(username.Value, !username.IsActive));
        }

        #region Username

        private bool _isValid;
        public bool IsValid
        {
            get => _isValid;
            set => Set(ref _isValid, value);
        }

        private bool _isAvailable;
        public bool IsAvailable
        {
            get => _isAvailable;
            set => Set(ref _isAvailable, value);
        }

        private string _errorMessage;
        public string ErrorMessage
        {
            get => _errorMessage;
            set => Set(ref _errorMessage, value);
        }

        public async void CheckAvailability(string text)
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            var supergroup = ClientService.GetSupergroup(chat);
            if (supergroup != null && string.Equals(text, supergroup.EditableUsername()))
            {
                IsLoading = false;
                IsAvailable = false;
                ErrorMessage = null;

                return;
            }

            var chatId = chat.Type is ChatTypeSupergroup ? chat.Id : 0;

            var response = await ClientService.SendAsync(new CheckChatUsername(chatId, text));
            if (response is CheckChatUsernameResultOk)
            {
                IsLoading = false;
                IsAvailable = true;
                ErrorMessage = null;
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
            else if (response is CheckChatUsernameResultUsernamePurchasable)
            {
                IsLoading = false;
                IsAvailable = false;
                ErrorMessage = Strings.Resources.UsernameInUsePurchase;
            }
            else if (response is CheckChatUsernameResultPublicChatsTooMany)
            {
                HasTooMuchUsernames = true;
                NavigationService.ShowLimitReached(new PremiumLimitTypeCreatedPublicChatCount());
            }
            else if (response is Error error)
            {
                IsLoading = false;
                IsAvailable = false;
                ErrorMessage = error.Message;
            }

            RaisePropertyChanged(nameof(Username));
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
                else if (username.Length < 5)
                {
                    ErrorMessage = Strings.Resources.UsernameInvalidShort;
                }
                else if (username.Length > 32)
                {
                    ErrorMessage = Strings.Resources.UsernameInvalidLong;
                }
                else if (username[0] is >= '0' and <= '9')
                {
                    ErrorMessage = Strings.Resources.UsernameInvalidStartNumber;
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

            if (ClientService.TryGetSupergroup(_chat, out Supergroup supergroup))
            {
                UpdateUsernames(supergroup.Usernames, username);
            }

            //RaisePropertyChanged(nameof(IsVisible));
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
                if (i == 0 && char.IsDigit(username[0]))
                {
                    return false;
                }
                else if (!MessageHelper.IsValidUsernameSymbol(username[i]))
                {
                    return false;
                }
            }

            return true;
        }

        #endregion
    }
}
