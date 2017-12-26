using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api;
using Telegram.Api.Aggregator;
using Telegram.Api.Helpers;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Telegram.Api.Services.Cache.EventArgs;
using Telegram.Api.Services.FileManager;
using Telegram.Api.TL;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Core.Common;
using Unigram.Views.Channels;
using Windows.Storage;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels.Channels
{
    public class ChannelEditViewModel : ChannelDetailsViewModel
    {
        private readonly IUploadFileManager _uploadFileManager;

        public ChannelEditViewModel(IMTProtoService protoService, ICacheService cacheService, ITelegramEventAggregator aggregator, IUploadFileManager uploadFileManager)
            : base(protoService, cacheService, aggregator)
        {
            _uploadFileManager = uploadFileManager;

            AdminedPublicChannels = new MvxObservableCollection<TLChannel>();

            SendCommand = new RelayCommand(SendExecute);
            EditPhotoCommand = new RelayCommand<StorageFile>(EditPhotoExecute);
            EditStickerSetCommand = new RelayCommand(EditStickerSetExecute);
            RevokeLinkCommand = new RelayCommand<TLChannel>(RevokeLinkExecute);
            DeleteCommand = new RelayCommand(DeleteExecute);
        }

        public bool CanEditSignatures
        {
            get
            {
                return _item != null && _item.IsBroadcast;
            }
        }

        public bool CanEditHiddenPreHistory
        {
            get
            {
                return _item != null && _full != null && _item.IsMegaGroup && !_item.HasUsername;
            }
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
        private bool _isHiddenPreHistory;
        public bool IsHiddenPreHistory
        {
            get
            {
                return _isHiddenPreHistory;
            }
            set
            {
                Set(ref _isHiddenPreHistory, value);
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

        public MvxObservableCollection<TLChannel> AdminedPublicChannels { get; private set; }

        public override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            // SHOULD NOT CALL BASE!

            Item = null;
            Full = null;
            Title = null;
            About = null;

            var channel = parameter as TLChannel;
            var peer = parameter as TLPeerChannel;
            if (peer != null)
            {
                channel = CacheService.GetChat(peer.ChannelId) as TLChannel;
            }

            if (channel != null)
            {
                Item = channel;
                Title = _item.Title;
                Username = _item.Username;
                IsPublic = _item.HasUsername;
                IsSignatures = _item.IsSignatures;

                RaisePropertyChanged(() => CanEditSignatures);

                var full = CacheService.GetFullChat(channel.Id) as TLChannelFull;
                if (full == null)
                {
                    var response = await ProtoService.GetFullChannelAsync(channel.ToInputChannel());
                    if (response.IsSucceeded)
                    {
                        full = response.Result.FullChat as TLChannelFull;
                    }
                }

                if (full != null)
                {
                    Full = full;
                    About = _full.About;
                    IsHiddenPreHistory = _full.IsHiddenPreHistory;

                    if (full.ExportedInvite is TLChatInviteExported exported)
                    {
                        InviteLink = exported.Link;
                    }
                    else
                    {
                        ExportInvite();
                    }

                    RaisePropertyChanged(() => CanEditHiddenPreHistory);

                    if (full.IsCanSetUsername)
                    {
                        var username = await ProtoService.CheckUsernameAsync(_item.ToInputChannel(), "username");
                        if (username.IsSucceeded)
                        {
                            HasTooMuchUsernames = false;
                        }
                        else
                        {
                            if (username.Error.TypeEquals(TLErrorType.CHANNELS_ADMIN_PUBLIC_TOO_MUCH))
                            {
                                HasTooMuchUsernames = true;
                                LoadAdminedPublicChannels();
                            }
                        }
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

            var response = await ProtoService.GetAdminedPublicChannelsAsync();
            if (response.IsSucceeded)
            {
                AdminedPublicChannels.ReplaceWith(response.Result.Chats.OfType<TLChannel>());
            }
            else
            {
                Execute.ShowDebugMessage("channels.getAdminedPublicChannels error " + response.Error);
            }
        }

        private async void ExportInvite()
        {
            if (_item == null || _inviteLink != null)
            {
                return;
            }

            var response = await ProtoService.ExportInviteAsync(_item.ToInputChannel());
            if (response.IsSucceeded && response.Result is TLChatInviteExported invite)
            {
                if (_full != null)
                {
                    _full.ExportedInvite = response.Result;
                    _full.RaisePropertyChanged(() => _full.ExportedInvite);
                }

                InviteLink = invite.Link;
            }
            else
            {
                Execute.ShowDebugMessage("channels.exportInvite error " + response.Error);
            }
        }

        public RelayCommand SendCommand { get; }
        private async void SendExecute()
        {
            var item = _item;
            if (item == null)
            {
                return;
            }

            var full = _full;
            if (full == null)
            {
                return;
            }

            var about = _about.Format();
            var title = _title.Trim();
            var username = _isPublic ? _username?.Trim() : null;

            if (item != null && !string.Equals(username, item.Username))
            {
                var response = await ProtoService.UpdateUsernameAsync(item.ToInputChannel(), username);
                if (response.IsSucceeded)
                {
                    item.Username = username;
                    item.HasUsername = username != null;
                    item.RaisePropertyChanged(() => item.Username);
                    item.RaisePropertyChanged(() => item.HasUsername);
                }
            }

            if (item != null && !string.Equals(title, item.Title))
            {
                var response = await ProtoService.EditTitleAsync(item, title);
                if (response.IsSucceeded)
                {
                    item.Title = title;
                    item.RaisePropertyChanged(() => item.Title);
                }
                else
                {
                    // TODO:
                    return;
                }
            }

            if (full != null && !string.Equals(about, full.About))
            {
                var response = await ProtoService.EditAboutAsync(item, about);
                if (response.IsSucceeded)
                {
                    full.About = about;
                    full.RaisePropertyChanged(() => full.About);
                }
                else
                {
                    // TODO:
                    return;
                }
            }

            if (_isSignatures != item.IsSignatures)
            {
                var response = await ProtoService.ToggleSignaturesAsync(item.ToInputChannel(), _isSignatures);
                if (response.IsSucceeded)
                {

                }
                else
                {
                    // TODO:
                    return;
                }
            }

            if (full != null && _isHiddenPreHistory != full.IsHiddenPreHistory)
            {
                var response = await ProtoService.TogglePreHistoryHiddenAsync(item.ToInputChannel(), _isHiddenPreHistory);
                if (response.IsSucceeded)
                {

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
                var response = await ProtoService.EditPhotoAsync(_item, new TLInputChatUploadedPhoto { File = upload.ToInputFile() });
                if (response.IsSucceeded)
                {

                }
            }
        }

        public RelayCommand EditStickerSetCommand { get; }
        private void EditStickerSetExecute()
        {
            NavigationService.Navigate(typeof(ChannelEditStickerSetPage), _item.ToPeer());
        }

        public RelayCommand<TLChannel> RevokeLinkCommand { get; }
        private async void RevokeLinkExecute(TLChannel channel)
        {
            var dialog = new TLMessageDialog();
            dialog.Title = Strings.Android.AppName;
            dialog.Message = string.Format(Strings.Android.RevokeLinkAlert, channel.Username, channel.DisplayName);
            dialog.PrimaryButtonText = Strings.Android.RevokeButton;
            dialog.SecondaryButtonText = Strings.Android.Cancel;

            var confirm = await dialog.ShowQueuedAsync();
            if (confirm == ContentDialogResult.Primary)
            {
                var response = await ProtoService.UpdateUsernameAsync(channel.ToInputChannel(), string.Empty);
                if (response.IsSucceeded)
                {
                    channel.Username = null;
                    channel.HasUsername = false;
                    channel.RaisePropertyChanged(() => channel.Username);
                    channel.RaisePropertyChanged(() => channel.HasUsername);

                    HasTooMuchUsernames = false;
                    AdminedPublicChannels.Clear();
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

            var message = item.IsMegaGroup ? Strings.Android.MegaDeleteAlert : Strings.Android.ChannelDeleteAlert;
            var confirm = await TLMessageDialog.ShowAsync(message, Strings.Android.AppName, Strings.Android.OK, Strings.Android.Cancel);
            if (confirm == ContentDialogResult.Primary)
            {
                var response = await ProtoService.DeleteChannelAsync(item);
                if (response.IsSucceeded)
                {
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
            if (string.Equals(text, _item?.Username))
            {
                IsLoading = false;
                IsAvailable = false;
                ErrorMessage = null;

                return;
            }

            var response = await ProtoService.CheckUsernameAsync(_item.ToInputChannel(), text);
            if (response.IsSucceeded)
            {
                if (response.Result)
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
            else
            {
                if (response.Error.TypeEquals(TLErrorType.USERNAME_INVALID))
                {
                    IsLoading = false;
                    IsAvailable = false;
                    ErrorMessage = Strings.Android.UsernameInvalid;
                }
                else if (response.Error.TypeEquals(TLErrorType.USERNAME_OCCUPIED))
                {
                    IsLoading = false;
                    IsAvailable = false;
                    ErrorMessage = Strings.Android.UsernameInUse;
                }
                else if (response.Error.TypeEquals(TLErrorType.CHANNELS_ADMIN_PUBLIC_TOO_MUCH))
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
