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
    public class SupergroupEditViewModel : SupergroupEditViewModelBase,
        IHandle<UpdateSupergroup>,
        IHandle<UpdateSupergroupFullInfo>,
        IHandle<UpdateChatTitle>,
        IHandle<UpdateChatPhoto>
    {
        public SupergroupEditViewModel(IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(protoService, cacheService, settingsService, aggregator)
        {
            EditPhotoCommand = new RelayCommand<StorageFile>(EditPhotoExecute);
            EditStickerSetCommand = new RelayCommand(EditStickerSetExecute);
            DeleteCommand = new RelayCommand(DeleteExecute);
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



        protected override async void SendExecute()
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
                        if (error.TypeEquals(ErrorType.CHANNELS_ADMIN_PUBLIC_TOO_MUCH))
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
                var message = super.IsChannel ? Strings.Resources.ChannelDeleteAlert : Strings.Resources.MegaDeleteAlert;
                var confirm = await TLMessageDialog.ShowAsync(message, Strings.Resources.AppName, Strings.Resources.OK, Strings.Resources.Cancel);
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
    }
}
