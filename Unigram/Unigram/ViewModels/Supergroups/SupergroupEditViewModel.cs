using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Entities;
using Unigram.Services;
using Unigram.ViewModels.Delegates;
using Unigram.Views.Popups;
using Unigram.Views.Supergroups;
using Windows.Foundation;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using static Unigram.Services.GenerationService;

namespace Unigram.ViewModels.Supergroups
{
    public class SupergroupEditViewModel : TLViewModelBase,
        IDelegable<ISupergroupEditDelegate>,
        IHandle<UpdateSupergroup>,
        IHandle<UpdateSupergroupFullInfo>,
        IHandle<UpdateBasicGroup>,
        IHandle<UpdateBasicGroupFullInfo>
    {
        public ISupergroupEditDelegate Delegate { get; set; }

        public SupergroupEditViewModel(IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(protoService, cacheService, settingsService, aggregator)
        {
            EditTypeCommand = new RelayCommand(EditTypeExecute);
            EditHistoryCommand = new RelayCommand(EditHistoryExecute);
            EditLinkedChatCommand = new RelayCommand(EditLinkedChatExecute);
            EditStickerSetCommand = new RelayCommand(EditStickerSetExecute);
            EditPhotoCommand = new RelayCommand<StorageMedia>(EditPhotoExecute);
            DeletePhotoCommand = new RelayCommand(DeletePhotoExecute);

            RevokeCommand = new RelayCommand(RevokeExecute);
            DeleteCommand = new RelayCommand(DeleteExecute);

            SendCommand = new RelayCommand(SendExecute);

            MembersCommand = new RelayCommand(MembersExecute);
            AdminsCommand = new RelayCommand(AdminsExecute);
            BannedCommand = new RelayCommand(BannedExecute);
            KickedCommand = new RelayCommand(KickedExecute);
        }

        protected Chat _chat;
        public Chat Chat
        {
            get { return _chat; }
            set { Set(ref _chat, value); }
        }

        private StorageMedia _photo;
        private bool _deletePhoto;

        private string _title;
        public string Title
        {
            get { return _title; }
            set { Set(ref _title, value); }
        }

        private string _about;
        public string About
        {
            get { return _about; }
            set { Set(ref _about, value); }
        }

        private bool _isSignatures;
        public bool IsSignatures
        {
            get { return _isSignatures; }
            set { Set(ref _isSignatures, value); }
        }

        private bool _isAllHistoryAvailable;
        public bool IsAllHistoryAvailable
        {
            get { return _isAllHistoryAvailable; }
            set { Set(ref _isAllHistoryAvailable, value); }
        }

        #region Initialize

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
            else if (chat.Type is ChatTypeBasicGroup basic)
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

        public override Task OnNavigatedFromAsync(IDictionary<string, object> pageState, bool suspending)
        {
            Aggregator.Unsubscribe(this);
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

        #endregion

        public RelayCommand SendCommand { get; }
        private async void SendExecute()
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            var about = _about.Format();
            var title = _title.Trim();
            string oldAbout = null;
            Supergroup supergroup = null;
            SupergroupFullInfo fullInfo = null;

            if (chat.Type is ChatTypeSupergroup)
            {
                var item = ProtoService.GetSupergroup(chat);
                var cache = ProtoService.GetSupergroupFull(chat);

                if (item == null || cache == null)
                {
                    return;
                }

                oldAbout = cache.Description;
                supergroup = item;
                fullInfo = cache;

                if (item.IsChannel && _isSignatures != item.SignMessages)
                {
                    var response = await ProtoService.SendAsync(new ToggleSupergroupSignMessages(item.Id, _isSignatures));
                    if (response is Error)
                    {
                        // TODO:
                    }
                }
            }
            else if (chat.Type is ChatTypeBasicGroup basicGroup)
            {
                var item = ProtoService.GetBasicGroup(basicGroup.BasicGroupId);
                var cache = ProtoService.GetBasicGroupFull(basicGroup.BasicGroupId);

                oldAbout = cache.Description;
            }

            if (!string.Equals(title, chat.Title))
            {
                var response = await ProtoService.SendAsync(new SetChatTitle(chat.Id, title));
                if (response is Error)
                {
                    // TODO:
                }
            }

            if (!string.Equals(about, oldAbout))
            {
                var response = await ProtoService.SendAsync(new SetChatDescription(chat.Id, about));
                if (response is Error)
                {
                    // TODO:
                }
            }

            if (_photo is StorageVideo media)
            {
                var props = await media.File.Properties.GetVideoPropertiesAsync();

                var duration = media.EditState.TrimStopTime - media.EditState.TrimStartTime;
                if (duration.TotalSeconds > 9)
                {
                    media.EditState.TrimStopTime = media.EditState.TrimStartTime + TimeSpan.FromSeconds(9);
                }

                var conversion = new VideoConversion();
                conversion.Mute = true;
                conversion.TrimStartTime = media.EditState.TrimStartTime;
                conversion.TrimStopTime = media.EditState.TrimStopTime;
                conversion.Transcode = true;
                conversion.Transform = true;
                //conversion.Rotation = file.EditState.Rotation;
                conversion.OutputSize = new Size(500, 500);
                //conversion.Mirror = transform.Mirror;
                conversion.CropRectangle = new Rect(
                    media.EditState.Rectangle.X * props.Width,
                    media.EditState.Rectangle.Y * props.Height,
                    media.EditState.Rectangle.Width * props.Width,
                    media.EditState.Rectangle.Height * props.Height);

                var rectangle = conversion.CropRectangle;
                rectangle.Width = Math.Min(conversion.CropRectangle.Width, conversion.CropRectangle.Height);
                rectangle.Height = rectangle.Width;

                conversion.CropRectangle = rectangle;

                var generated = await media.File.ToGeneratedAsync(ConversionType.Transcode, JsonConvert.SerializeObject(conversion));
                var response = await ProtoService.SendAsync(new SetChatPhoto(chat.Id, new InputChatPhotoAnimation(generated, 0)));
            }
            else if (_photo is StoragePhoto photo)
            {
                var generated = await photo.File.ToGeneratedAsync(ConversionType.Compress, JsonConvert.SerializeObject(photo.EditState));
                var response = await ProtoService.SendAsync(new SetProfilePhoto(new InputChatPhotoStatic(generated)));
            }
            else if (_deletePhoto)
            {
                var response = await ProtoService.SendAsync(new SetChatPhoto(chat.Id, null));
                if (response is Error)
                {
                    // TODO:
                }
            }

            if (_isAllHistoryAvailable && chat.Type is ChatTypeBasicGroup)
            {
                var response = await ProtoService.SendAsync(new UpgradeBasicGroupChatToSupergroupChat(chat.Id));
                if (response is Chat result && result.Type is ChatTypeSupergroup super)
                {
                    chat = result;
                    fullInfo = await ProtoService.SendAsync(new GetSupergroupFullInfo(super.SupergroupId)) as SupergroupFullInfo;
                }
                else if (response is Error)
                {
                    // TODO:
                }
            }

            if (fullInfo != null && _isAllHistoryAvailable != fullInfo.IsAllHistoryAvailable)
            {
                var response = await ProtoService.SendAsync(new ToggleSupergroupIsAllHistoryAvailable(supergroup.Id, _isAllHistoryAvailable));
                if (response is Error)
                {
                    // TODO:
                }
            }

            NavigationService.GoBack();
        }

        public RelayCommand<StorageMedia> EditPhotoCommand { get; }
        private async void EditPhotoExecute(StorageMedia file)
        {
            _photo = file;
            _deletePhoto = false;
        }

        public RelayCommand DeletePhotoCommand { get; }
        private void DeletePhotoExecute()
        {
            _photo = null;
            _deletePhoto = true;
        }

        public RelayCommand EditTypeCommand { get; }
        private void EditTypeExecute()
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            NavigationService.Navigate(typeof(SupergroupEditTypePage), chat.Id);
        }

        public RelayCommand EditHistoryCommand { get; }
        private async void EditHistoryExecute()
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            var initialValue = false;

            if (chat.Type is ChatTypeSupergroup)
            {
                var supergroup = CacheService.GetSupergroup(chat);
                if (supergroup == null)
                {
                    return;
                }

                var full = CacheService.GetSupergroupFull(chat);
                if (full == null)
                {
                    return;
                }

                initialValue = full.IsAllHistoryAvailable;
            }

            var items = new[]
            {
                new SelectRadioItem(true, Strings.Resources.ChatHistoryVisible, initialValue) { Footer = Strings.Resources.ChatHistoryVisibleInfo },
                new SelectRadioItem(false, Strings.Resources.ChatHistoryHidden, !initialValue) { Footer = Strings.Resources.ChatHistoryHiddenInfo }
            };

            var dialog = new SelectRadioPopup(items);
            dialog.Title = Strings.Resources.ChatHistory;
            dialog.PrimaryButtonText = Strings.Resources.OK;
            dialog.SecondaryButtonText = Strings.Resources.Cancel;

            var confirm = await dialog.ShowQueuedAsync();
            if (confirm == ContentDialogResult.Primary && dialog.SelectedIndex is bool index)
            {
                IsAllHistoryAvailable = index;
            }
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

        public RelayCommand EditLinkedChatCommand { get; }
        private void EditLinkedChatExecute()
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            var supergroup = CacheService.GetSupergroup(chat);
            if (supergroup == null)
            {
                return;
            }

            NavigationService.Navigate(typeof(SupergroupEditLinkedChatPage), chat.Id);
        }

        public RelayCommand RevokeCommand { get; }
        private async void RevokeExecute()
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            var confirm = await MessagePopup.ShowAsync(Strings.Resources.RevokeAlert, Strings.Resources.RevokeLink, Strings.Resources.RevokeButton, Strings.Resources.Cancel);
            if (confirm != ContentDialogResult.Primary)
            {
                return;
            }

            ProtoService.Send(new GenerateChatInviteLink(chat.Id));
        }

        public RelayCommand DeleteCommand { get; }
        private async void DeleteExecute()
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            var updated = await ProtoService.SendAsync(new GetChat(chat.Id)) as Chat ?? chat;
            var dialog = new DeleteChatPopup(ProtoService, updated, null, false, true);

            var confirm = await dialog.ShowQueuedAsync();
            if (confirm != ContentDialogResult.Primary)
            {
                return;
            }

            Function function;
            if (chat.Type is ChatTypeSupergroup super)
            {
                function = new DeleteSupergroup(super.SupergroupId);
            }
            else
            {
                await ProtoService.SendAsync(new LeaveChat(chat.Id));
                function = new DeleteChatHistory(chat.Id, true, false);
            }

            var response = await ProtoService.SendAsync(function);
            if (response is Ok)
            {
                NavigationService.RemovePeerFromStack(chat.Id);
            }
            else if (response is Error error)
            {
                // TODO: ...
            }
        }

        #region Navigation

        public RelayCommand AdminsCommand { get; }
        private void AdminsExecute()
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            NavigationService.Navigate(typeof(SupergroupAdministratorsPage), chat.Id);
        }

        public RelayCommand BannedCommand { get; }
        private void BannedExecute()
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            NavigationService.Navigate(typeof(SupergroupBannedPage), chat.Id);
        }

        public RelayCommand KickedCommand { get; }
        private void KickedExecute()
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            NavigationService.Navigate(typeof(SupergroupPermissionsPage), chat.Id);
        }

        public RelayCommand MembersCommand { get; }
        private void MembersExecute()
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            NavigationService.Navigate(typeof(SupergroupMembersPage), chat.Id);
        }

        #endregion
    }
}
