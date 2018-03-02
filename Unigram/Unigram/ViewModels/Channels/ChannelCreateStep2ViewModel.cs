using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TdWindows;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Core.Common;
using Unigram.Services;
using Unigram.Views.Channels;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels.Channels
{
    public class ChannelCreateStep2ViewModel : UnigramViewModelBase
    {
        public ChannelCreateStep2ViewModel(IProtoService protoService, ICacheService cacheService, IEventAggregator aggregator) 
            : base(protoService, cacheService, aggregator)
        {
            AdminedPublicChannels = new MvxObservableCollection<Chat>();

            RevokeLinkCommand = new RelayCommand<Chat>(RevokeLinkExecute);
            SendCommand = new RelayCommand(SendExecute);
        }

        //public override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        //{
        //    Item = null;
        //    Full = null;

        //    var channel = parameter as TLChannel;
        //    var peer = parameter as TLPeerChannel;
        //    if (peer != null)
        //    {
        //        channel = CacheService.GetChat(peer.ChannelId) as TLChannel;
        //    }

        //    if (channel != null)
        //    {
        //        Item = channel;
        //        Username = _item.Username;
        //        IsPublic = _item.HasUsername;

        //        var full = CacheService.GetFullChat(channel.Id) as TLChannelFull;
        //        if (full == null)
        //        {
        //            var response = await LegacyService.GetFullChannelAsync(channel.ToInputChannel());
        //            if (response.IsSucceeded)
        //            {
        //                full = response.Result.FullChat as TLChannelFull;
        //            }
        //        }

        //        if (full != null)
        //        {
        //            Full = full;

        //            if (full.ExportedInvite is TLChatInviteExported exported)
        //            {
        //                InviteLink = exported.Link;
        //            }
        //            else
        //            {
        //                ExportInvite();
        //            }

        //            if (full.IsCanSetUsername)
        //            {
        //                var username = await LegacyService.CheckUsernameAsync(_item.ToInputChannel(), "username");
        //                if (username.IsSucceeded)
        //                {
        //                    HasTooMuchUsernames = false;
        //                }
        //                else
        //                {
        //                    if (username.Error.TypeEquals(TLErrorType.CHANNELS_ADMIN_PUBLIC_TOO_MUCH))
        //                    {
        //                        HasTooMuchUsernames = true;
        //                        LoadAdminedPublicChannels();
        //                    }
        //                }
        //            }
        //        }
        //    }
        //}

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

        public RelayCommand SendCommand { get; }
        private async void SendExecute()
        {
            //var username = _isPublic ? _username?.Trim() : null;

            //if (_item != null && !string.Equals(username, _item.Username))
            //{
            //    var response = await LegacyService.UpdateUsernameAsync(_item.ToInputChannel(), username);
            //    if (response.IsSucceeded)
            //    {
            //        _item.Username = username;
            //        _item.HasUsername = username != null;
            //        _item.RaisePropertyChanged(() => _item.Username);
            //        _item.RaisePropertyChanged(() => _item.HasUsername);
            //    }
            //}

            NavigationService.Navigate(typeof(ChannelCreateStep3Page));
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
                dialog.Message = string.Format(Strings.Resources.RevokeLinkAlert, supergroup.Username, chat.Title);
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
            //if (string.Equals(text, _item?.Username))
            //{
            //    IsLoading = false;
            //    IsAvailable = false;
            //    ErrorMessage = null;

            //    return;
            //}

            //var response = await LegacyService.CheckUsernameAsync(_item.ToInputChannel(), text);
            //if (response.IsSucceeded)
            //{
            //    if (response.Result)
            //    {
            //        IsLoading = false;
            //        IsAvailable = true;
            //        ErrorMessage = null;
            //    }
            //    else
            //    {
            //        IsLoading = false;
            //        IsAvailable = false;
            //        ErrorMessage = Strings.Resources.UsernameInUse;
            //    }
            //}
            //else
            //{
            //    if (response.Error.TypeEquals(TLErrorType.USERNAME_INVALID))
            //    {
            //        IsLoading = false;
            //        IsAvailable = false;
            //        ErrorMessage = Strings.Resources.UsernameInvalid;
            //    }
            //    else if (response.Error.TypeEquals(TLErrorType.USERNAME_OCCUPIED))
            //    {
            //        IsLoading = false;
            //        IsAvailable = false;
            //        ErrorMessage = Strings.Resources.UsernameInUse;
            //    }
            //    else if (response.Error.TypeEquals(TLErrorType.CHANNELS_ADMIN_PUBLIC_TOO_MUCH))
            //    {
            //        HasTooMuchUsernames = true;
            //        LoadAdminedPublicChannels();
            //    }
            //}
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
