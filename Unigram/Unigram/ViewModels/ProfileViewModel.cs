using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Collections;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Converters;
using Unigram.Navigation.Services;
using Unigram.Services;
using Unigram.ViewModels.Chats;
using Unigram.ViewModels.Delegates;
using Unigram.ViewModels.Supergroups;
using Unigram.ViewModels.Users;
using Unigram.Views;
using Unigram.Views.Chats;
using Unigram.Views.Popups;
using Unigram.Views.Supergroups;
using Unigram.Views.Users;
using Windows.ApplicationModel.DataTransfer;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels
{
    public class ProfileViewModel : TLMultipleViewModelBase,
        IDelegable<IProfileDelegate>,
        IHandle<UpdateUser>,
        IHandle<UpdateUserFullInfo>,
        IHandle<UpdateBasicGroup>,
        IHandle<UpdateBasicGroupFullInfo>,
        IHandle<UpdateSupergroup>,
        IHandle<UpdateSupergroupFullInfo>,
        IHandle<UpdateUserStatus>,
        IHandle<UpdateChatTitle>,
        IHandle<UpdateChatPhoto>,
        IHandle<UpdateChatNotificationSettings>,
        IHandle<UpdateFile>
    {
        public string LastSeen { get; internal set; }

        public IProfileDelegate Delegate { get; set; }

        private readonly IVoipService _voipService;
        private readonly INotificationsService _notificationsService;

        private readonly ChatSharedMediaViewModel _chatSharedMediaViewModel;
        private readonly UserCommonChatsViewModel _userCommonChatsViewModel;
        private readonly SupergroupMembersViewModel _supergroupMembersVieModel;

        public ProfileViewModel(IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator, IVoipService voipService, INotificationsService notificationsService, ChatSharedMediaViewModel chatSharedMediaViewModel, UserCommonChatsViewModel userCommonChatsViewModel, SupergroupMembersViewModel supergroupMembersViewModel)
            : base(protoService, cacheService, settingsService, aggregator)
        {
            _voipService = voipService;
            _notificationsService = notificationsService;

            _chatSharedMediaViewModel = chatSharedMediaViewModel;
            _userCommonChatsViewModel = userCommonChatsViewModel;
            _supergroupMembersVieModel = supergroupMembersViewModel;
            _supergroupMembersVieModel.IsEmbedded = true;

            SendMessageCommand = new RelayCommand(SendMessageExecute);
            SystemCallCommand = new RelayCommand(SystemCallExecute);
            BlockCommand = new RelayCommand(BlockExecute);
            UnblockCommand = new RelayCommand(UnblockExecute);
            ReportCommand = new RelayCommand(ReportExecute);
            CallCommand = new RelayCommand<bool>(CallExecute);
            CopyPhoneCommand = new RelayCommand(CopyPhoneExecute);
            CopyDescriptionCommand = new RelayCommand(CopyDescriptionExecute);
            CopyUsernameCommand = new RelayCommand(CopyUsernameExecute);
            CopyUsernameLinkCommand = new RelayCommand(CopyUsernameLinkExecute);
            AddCommand = new RelayCommand(AddExecute);
            DiscussCommand = new RelayCommand(DiscussExecute);
            EditCommand = new RelayCommand(EditExecute);
            DeleteCommand = new RelayCommand(DeleteExecute);
            ShareCommand = new RelayCommand(ShareExecute);
            SecretChatCommand = new RelayCommand(SecretChatExecute);
            SetTimerCommand = new RelayCommand(SetTimerExecute);
            IdenticonCommand = new RelayCommand(IdenticonExecute);
            MigrateCommand = new RelayCommand(MigrateExecute);
            InviteCommand = new RelayCommand(InviteExecute);
            ToggleMuteCommand = new RelayCommand(ToggleMuteExecute);
            StatisticsCommand = new RelayCommand(StatisticsExecute);
            MemberPromoteCommand = new RelayCommand<ChatMember>(MemberPromoteExecute);
            MemberRestrictCommand = new RelayCommand<ChatMember>(MemberRestrictExecute);
            MemberRemoveCommand = new RelayCommand<ChatMember>(MemberRemoveExecute);

            MembersCommand = new RelayCommand(MembersExecute);
            AdminsCommand = new RelayCommand(AdminsExecute);
            BannedCommand = new RelayCommand(BannedExecute);
            KickedCommand = new RelayCommand(KickedExecute);

            Children.Add(chatSharedMediaViewModel);
            Children.Add(userCommonChatsViewModel);
            Children.Add(supergroupMembersViewModel);
        }

        public ChatSharedMediaViewModel ChatSharedMedia => _chatSharedMediaViewModel;
        public UserCommonChatsViewModel UserCommonChats => _userCommonChatsViewModel;
        public SupergroupMembersViewModel SupergroupMembers => _supergroupMembersVieModel;

        protected Chat _chat;
        public Chat Chat
        {
            get { return _chat; }
            set { Set(ref _chat, value); }
        }

        protected ObservableCollection<ChatMember> _members;
        public ObservableCollection<ChatMember> Members
        {
            get { return _members; }
            set { Set(ref _members, value); }
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

            if (chat.Type is ChatTypePrivate privata)
            {
                var item = ProtoService.GetUser(privata.UserId);
                var cache = ProtoService.GetUserFull(privata.UserId);

                Delegate?.UpdateUser(chat, item, false);

                if (cache == null)
                {
                    ProtoService.Send(new GetUserFullInfo(privata.UserId));
                }
                else
                {
                    Delegate?.UpdateUserFullInfo(chat, item, cache, false, false);
                }
            }
            else if (chat.Type is ChatTypeSecret secretType)
            {
                var secret = ProtoService.GetSecretChat(secretType.SecretChatId);
                var item = ProtoService.GetUser(secretType.UserId);
                var cache = ProtoService.GetUserFull(secretType.UserId);

                Delegate?.UpdateSecretChat(chat, secret);
                Delegate?.UpdateUser(chat, item, true);

                if (cache == null)
                {
                    ProtoService.Send(new GetUserFullInfo(secret.UserId));
                }
                else
                {
                    Delegate?.UpdateUserFullInfo(chat, item, cache, true, false);
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
            else if (chat.Type is ChatTypeSupergroup super)
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

            return base.OnNavigatedToAsync(parameter, mode, state);
        }

        public override Task OnNavigatedFromAsync(IDictionary<string, object> pageState, bool suspending)
        {
            Aggregator.Unsubscribe(this);
            return Task.CompletedTask;
        }



        public void Handle(UpdateUser update)
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            if (chat.Type is ChatTypePrivate privata && privata.UserId == update.User.Id)
            {
                BeginOnUIThread(() => Delegate?.UpdateUser(chat, update.User, false));
            }
            else if (chat.Type is ChatTypeSecret secret && secret.UserId == update.User.Id)
            {
                BeginOnUIThread(() => Delegate?.UpdateUser(chat, update.User, true));
            }
        }

        public void Handle(UpdateUserFullInfo update)
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            if (chat.Type is ChatTypePrivate privata && privata.UserId == update.UserId)
            {
                BeginOnUIThread(() => Delegate?.UpdateUserFullInfo(chat, ProtoService.GetUser(update.UserId), update.UserFullInfo, false, false));
            }
            else if (chat.Type is ChatTypeSecret secret && secret.UserId == update.UserId)
            {
                BeginOnUIThread(() => Delegate?.UpdateUserFullInfo(chat, ProtoService.GetUser(update.UserId), update.UserFullInfo, true, false));
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

        public void Handle(UpdateUserStatus update)
        {
            if (_chat?.Type is ChatTypePrivate privata && privata.UserId == update.UserId || _chat?.Type is ChatTypeSecret secret && secret.UserId == update.UserId)
            {
                BeginOnUIThread(() => Delegate?.UpdateUserStatus(_chat, ProtoService.GetUser(update.UserId)));
            }
        }

        public void Handle(UpdateChatNotificationSettings update)
        {
            if (update.ChatId == _chat?.Id)
            {
                BeginOnUIThread(() => Delegate?.UpdateChatNotificationSettings(_chat));
            }
        }

        public void Handle(UpdateFile update)
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            BeginOnUIThread(() => Delegate?.UpdateFile(update.File));
        }

        public RelayCommand SendMessageCommand { get; }
        private void SendMessageExecute()
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            if (chat.Type is ChatTypeSupergroup)
            {
                var fullInfo = CacheService.GetSupergroupFull(chat);
                if (fullInfo != null && fullInfo.LinkedChatId != 0)
                {
                    NavigationService.NavigateToChat(fullInfo.LinkedChatId);
                    return;
                }
            }

            NavigationService.NavigateToChat(chat);
        }

        public RelayCommand StatisticsCommand { get; }
        private void StatisticsExecute()
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            NavigationService.Navigate(typeof(ChatStatisticsPage), chat.Id);

            //var fullInfo = CacheService.GetSupergroupFull(chat);
            //if (fullInfo == null || !fullInfo.CanViewStatistics)
            //{
            //    return;
            //}

            //var response = await ProtoService.SendAsync(new GetChatStatisticsUrl(chat.Id, string.Empty));
            //if (response is ChatStatisticsUrl url)
            //{
            //    await Launcher.LaunchUriAsync(new Uri(url.Url));
            //}
        }

        public RelayCommand SystemCallCommand { get; }
        private void SystemCallExecute()
        {
            //var user = Item as TLUser;
            //if (user != null)
            //{
            //    if (ApiInformation.IsTypePresent("Windows.ApplicationModel.Calls.PhoneCallManager"))
            //    {
            //        PhoneCallManager.ShowPhoneCallUI($"+{user.Phone}", user.FullName);
            //    }
            //    else
            //    {
            //        // TODO
            //    }
            //}
        }

        public RelayCommand BlockCommand { get; }
        private async void BlockExecute()
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            var confirm = await MessagePopup.ShowAsync(Strings.Resources.AreYouSureBlockContact, Strings.Resources.AppName, Strings.Resources.OK, Strings.Resources.Cancel);
            if (confirm != ContentDialogResult.Primary)
            {
                return;
            }

            if (chat.Type is ChatTypePrivate privata)
            {
                ProtoService.Send(new BlockUser(privata.UserId));
            }
            else if (chat.Type is ChatTypeSecret secret)
            {
                ProtoService.Send(new BlockUser(secret.UserId));
            }
        }

        public RelayCommand UnblockCommand { get; }
        private async void UnblockExecute()
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            var confirm = await MessagePopup.ShowAsync(Strings.Resources.AreYouSureUnblockContact, Strings.Resources.AppName, Strings.Resources.OK, Strings.Resources.Cancel);
            if (confirm != ContentDialogResult.Primary)
            {
                return;
            }

            if (chat.Type is ChatTypePrivate privata)
            {
                ProtoService.Send(new UnblockUser(privata.UserId));
            }
            else if (chat.Type is ChatTypeSecret secret)
            {
                ProtoService.Send(new UnblockUser(secret.UserId));
            }
        }

        public RelayCommand ShareCommand { get; }
        private async void ShareExecute()
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            if (chat.Type is ChatTypePrivate || chat.Type is ChatTypeSecret)
            {
                var user = ProtoService.GetUser(chat.Type is ChatTypePrivate privata ? privata.UserId : chat.Type is ChatTypeSecret secret ? secret.UserId : 0);
                if (user != null)
                {
                    await SharePopup.GetForCurrentView().ShowAsync(new InputMessageContact(new Telegram.Td.Api.Contact(user.PhoneNumber, user.FirstName, user.LastName, string.Empty, user.Id)));
                }
            }
        }

        public RelayCommand ReportCommand { get; }
        private async void ReportExecute()
        {
            //var user = Item as TLUser;
            //if (user != null)
            //{
            //    var opt1 = new RadioButton { Content = Strings.Resources.ReportChatSpam, Margin = new Thickness(0, 8, 0, 8), HorizontalAlignment = HorizontalAlignment.Stretch };
            //    var opt2 = new RadioButton { Content = Strings.Resources.ReportChatViolence, Margin = new Thickness(0, 8, 0, 8), HorizontalAlignment = HorizontalAlignment.Stretch };
            //    var opt3 = new RadioButton { Content = Strings.Resources.ReportChatPornography, Margin = new Thickness(0, 8, 0, 8), HorizontalAlignment = HorizontalAlignment.Stretch };
            //    var opt4 = new RadioButton { Content = Strings.Resources.ReportChatOther, Margin = new Thickness(0, 8, 0, 8), HorizontalAlignment = HorizontalAlignment.Stretch, IsChecked = true };
            //    var stack = new StackPanel();
            //    stack.Children.Add(opt1);
            //    stack.Children.Add(opt2);
            //    stack.Children.Add(opt3);
            //    stack.Children.Add(opt4);
            //    stack.Margin = new Thickness(12, 16, 12, 0);

            //    var dialog = new ContentDialog { Style = BootStrapper.Current.Resources["ModernContentDialogStyle"] as Style };
            //    dialog.Content = stack;
            //    dialog.Title = Strings.Resources.ReportChat;
            //    dialog.IsPrimaryButtonEnabled = true;
            //    dialog.IsSecondaryButtonEnabled = true;
            //    dialog.PrimaryButtonText = Strings.Resources.OK;
            //    dialog.SecondaryButtonText = Strings.Resources.Cancel;

            //    var dialogResult = await dialog.ShowQueuedAsync();
            //    if (dialogResult == ContentDialogResult.Primary)
            //    {
            //        var reason = opt1.IsChecked == true
            //            ? new TLInputReportReasonSpam()
            //            : (opt2.IsChecked == true
            //                ? new TLInputReportReasonViolence()
            //                : (opt3.IsChecked == true
            //                    ? new TLInputReportReasonPornography()
            //                    : (TLReportReasonBase)new TLInputReportReasonOther()));

            //        if (reason is TLInputReportReasonOther other)
            //        {
            //            var input = new InputDialog();
            //            input.Title = Strings.Resources.ReportChat;
            //            input.PlaceholderText = Strings.Resources.ReportChatDescription;
            //            input.IsPrimaryButtonEnabled = true;
            //            input.IsSecondaryButtonEnabled = true;
            //            input.PrimaryButtonText = Strings.Resources.OK;
            //            input.SecondaryButtonText = Strings.Resources.Cancel;

            //            var inputResult = await input.ShowQueuedAsync();
            //            if (inputResult == ContentDialogResult.Primary)
            //            {
            //                other.Text = input.Text;
            //            }
            //            else
            //            {
            //                return;
            //            }
            //        }

            //        var result = await LegacyService.ReportPeerAsync(user.ToInputPeer(), reason);
            //        if (result.IsSucceeded && result.Result)
            //        {
            //            await new MessagePopup("Resources.ReportSpamNotification", "Unigram").ShowQueuedAsync();
            //        }
            //    }
            //}
        }

        public RelayCommand CopyPhoneCommand { get; }
        private void CopyPhoneExecute()
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            var user = CacheService.GetUser(chat);
            if (user == null)
            {
                return;
            }

            var dataPackage = new DataPackage();
            dataPackage.SetText($"+{user.PhoneNumber}");
            ClipboardEx.TrySetContent(dataPackage);
        }

        public RelayCommand CopyDescriptionCommand { get; }
        private void CopyDescriptionExecute()
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            if (chat.Type is ChatTypeSupergroup super)
            {
                var supergroup = CacheService.GetSupergroupFull(super.SupergroupId);
                if (supergroup == null)
                {
                    return;
                }

                var dataPackage = new DataPackage();
                dataPackage.SetText(supergroup.Description);
                ClipboardEx.TrySetContent(dataPackage);
            }
            else
            {
                var user = CacheService.GetUserFull(chat);
                if (user == null)
                {
                    return;
                }

                var dataPackage = new DataPackage();
                dataPackage.SetText(user.Bio);
                ClipboardEx.TrySetContent(dataPackage);
            }
        }

        public RelayCommand CopyUsernameCommand { get; }
        private void CopyUsernameExecute()
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            if (chat.Type is ChatTypeSupergroup super)
            {
                var supergroup = CacheService.GetSupergroup(super.SupergroupId);
                if (supergroup == null)
                {
                    return;
                }

                var dataPackage = new DataPackage();
                dataPackage.SetText($"@{supergroup.Username}");
                ClipboardEx.TrySetContent(dataPackage);
            }
            else
            {
                var user = CacheService.GetUser(chat);
                if (user == null)
                {
                    return;
                }

                var dataPackage = new DataPackage();
                dataPackage.SetText($"@{user.Username}");
                ClipboardEx.TrySetContent(dataPackage);
            }
        }

        public RelayCommand CopyUsernameLinkCommand { get; }
        private void CopyUsernameLinkExecute()
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            if (chat.Type is ChatTypeSupergroup super)
            {
                var supergroup = CacheService.GetSupergroup(super.SupergroupId);
                if (supergroup == null)
                {
                    return;
                }

                var dataPackage = new DataPackage();
                dataPackage.SetText(MeUrlPrefixConverter.Convert(CacheService, supergroup.Username));
                ClipboardEx.TrySetContent(dataPackage);
            }
            else
            {
                var user = CacheService.GetUser(chat);
                if (user == null)
                {
                    return;
                }

                var dataPackage = new DataPackage();
                dataPackage.SetText(MeUrlPrefixConverter.Convert(CacheService, user.Username));
                ClipboardEx.TrySetContent(dataPackage);
            }
        }

        public RelayCommand SecretChatCommand { get; }
        private async void SecretChatExecute()
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            var confirm = await MessagePopup.ShowAsync(Strings.Resources.AreYouSureSecretChat, Strings.Resources.AppName, Strings.Resources.OK, Strings.Resources.Cancel);
            if (confirm != ContentDialogResult.Primary)
            {
                return;
            }

            if (chat.Type is ChatTypePrivate privata)
            {
                var response = await ProtoService.SendAsync(new CreateNewSecretChat(privata.UserId));
                if (response is Chat result)
                {
                    NavigationService.NavigateToChat(result);
                }
            }
        }

        public RelayCommand IdenticonCommand { get; }
        private void IdenticonExecute()
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            NavigationService.Navigate(typeof(IdenticonPage), chat.Id);
        }

        public RelayCommand MigrateCommand { get; }
        private async void MigrateExecute()
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            var confirm = await MessagePopup.ShowAsync(Strings.Resources.ConvertGroupInfo2 + "\n\n" + Strings.Resources.ConvertGroupInfo3, Strings.Resources.ConvertGroup, Strings.Resources.OK, Strings.Resources.Cancel);
            if (confirm != ContentDialogResult.Primary)
            {
                return;
            }

            var warning = await MessagePopup.ShowAsync(Strings.Resources.ConvertGroupAlert, Strings.Resources.ConvertGroupAlertWarning, Strings.Resources.OK, Strings.Resources.Cancel);
            if (warning != ContentDialogResult.Primary)
            {
                return;
            }

            var response = await ProtoService.SendAsync(new UpgradeBasicGroupChatToSupergroupChat(chat.Id));
            if (response is Chat upgraded)
            {
                NavigationService.NavigateToChat(upgraded);
                NavigationService.RemoveSkip(1);
            }
        }

        public RelayCommand InviteCommand { get; }
        private async void InviteExecute()
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            if (chat.Type is ChatTypePrivate || chat.Type is ChatTypeSecret)
            {
                var user = ProtoService.GetUser(chat);
                if (user == null)
                {
                    return;
                }

                await SharePopup.GetForCurrentView().ShowAsync(user);
            }
            else
            {
                var selected = await SharePopup.PickChatAsync(Strings.Resources.SelectContact);
                var user = CacheService.GetUser(selected);

                if (user == null)
                {
                    return;
                }

                var confirm = await MessagePopup.ShowAsync(string.Format(Strings.Resources.AddToTheGroup, user.GetFullName()), Strings.Resources.AppName, Strings.Resources.OK, Strings.Resources.Cancel);
                if (confirm != ContentDialogResult.Primary)
                {
                    return;
                }

                var response = await ProtoService.SendAsync(new AddChatMember(chat.Id, user.Id, CacheService.Options.ForwardedMessageCountMax));
                if (response is Error error)
                {

                }
            }
        }

        public RelayCommand ToggleMuteCommand { get; }
        private void ToggleMuteExecute()
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            _notificationsService.SetMuteFor(chat, CacheService.GetNotificationSettingsMuteFor(chat) > 0 ? 0 : 632053052);
        }

        #region Call

        public RelayCommand<bool> CallCommand { get; }
        private void CallExecute(bool video)
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            _voipService.Start(chat.Id, video);
        }

        #endregion

        public RelayCommand AddCommand { get; }
        private async void AddExecute()
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            var user = CacheService.GetUser(chat);
            if (user == null)
            {
                return;
            }

            var fullInfo = CacheService.GetUserFull(chat);
            if (fullInfo == null)
            {
                return;
            }

            var dialog = new EditUserNamePopup(user.FirstName, user.LastName, fullInfo.NeedPhoneNumberPrivacyException);

            var confirm = await dialog.ShowQueuedAsync();
            if (confirm == ContentDialogResult.Primary)
            {
                ProtoService.Send(new AddContact(new Contact(user.PhoneNumber, dialog.FirstName, dialog.LastName, string.Empty, user.Id),
                    fullInfo.NeedPhoneNumberPrivacyException ? dialog.SharePhoneNumber : true));
            }
        }

        public RelayCommand EditCommand { get; }
        private void EditExecute()
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            if (chat.Type is ChatTypeSupergroup || chat.Type is ChatTypeBasicGroup)
            {
                NavigationService.Navigate(typeof(SupergroupEditPage), chat.Id);
            }
            else if (chat.Type is ChatTypePrivate || chat.Type is ChatTypeSecret)
            {
                AddExecute();
            }
        }

        public RelayCommand DiscussCommand { get; }
        private void DiscussExecute()
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            if (chat.Type is ChatTypeSupergroup)
            {
                var fullInfo = CacheService.GetSupergroupFull(chat);
                if (fullInfo == null)
                {
                    return;
                }

                NavigationService.NavigateToChat(fullInfo.LinkedChatId);
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

            var message = Strings.Resources.AreYouSureDeleteAndExit;
            if (chat.Type is ChatTypePrivate || chat.Type is ChatTypeSecret)
            {
                message = Strings.Resources.AreYouSureDeleteContact;
            }
            else if (chat.Type is ChatTypeSupergroup super)
            {
                message = super.IsChannel ? Strings.Resources.ChannelLeaveAlert : Strings.Resources.MegaLeaveAlert;
            }

            var confirm = await MessagePopup.ShowAsync(message, Strings.Resources.AppName, Strings.Resources.OK, Strings.Resources.Cancel);
            if (confirm == ContentDialogResult.Primary)
            {
                if (chat.Type is ChatTypePrivate privata)
                {
                    ProtoService.Send(new RemoveContacts(new[] { privata.UserId }));
                }
                else if (chat.Type is ChatTypeSecret secret)
                {
                    ProtoService.Send(new RemoveContacts(new[] { secret.UserId }));
                }
                else
                {
                    if (chat.Type is ChatTypeBasicGroup || chat.Type is ChatTypeSupergroup)
                    {
                        await ProtoService.SendAsync(new LeaveChat(chat.Id));
                    }

                    ProtoService.Send(new DeleteChatHistory(chat.Id, true, false));
                }
            }

            //var user = _item as TLUser;
            //if (user == null)
            //{
            //    return;
            //}

            //var confirm = await MessagePopup.ShowAsync(Strings.Resources.AreYouSureDeleteContact, Strings.Resources.AppName, Strings.Resources.OK, Strings.Resources.Cancel);
            //if (confirm != ContentDialogResult.Primary)
            //{
            //    return;
            //}

            //var response = await LegacyService.DeleteContactAsync(user.ToInputUser());
            //if (response.IsSucceeded)
            //{
            //    // TODO: delete from synced contacts

            //    Aggregator.Publish(new TLUpdateContactLink
            //    {
            //        UserId = response.Result.User.Id,
            //        MyLink = response.Result.MyLink,
            //        ForeignLink = response.Result.ForeignLink
            //    });

            //    user.RaisePropertyChanged(() => user.HasFirstName);
            //    user.RaisePropertyChanged(() => user.HasLastName);
            //    user.RaisePropertyChanged(() => user.FirstName);
            //    user.RaisePropertyChanged(() => user.LastName);
            //    user.RaisePropertyChanged(() => user.FullName);
            //    user.RaisePropertyChanged(() => user.DisplayName);

            //    user.RaisePropertyChanged(() => user.HasPhone);
            //    user.RaisePropertyChanged(() => user.Phone);

            //    RaisePropertyChanged(() => IsEditEnabled);
            //    RaisePropertyChanged(() => IsAddEnabled);

            //    var dialog = CacheService.GetDialog(_item.ToPeer());
            //    if (dialog != null)
            //    {
            //        dialog.RaisePropertyChanged(() => dialog.With);
            //    }
            //}
        }

        #region Set timer

        public RelayCommand SetTimerCommand { get; }
        private async void SetTimerExecute()
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            var secretChat = CacheService.GetSecretChat(chat);
            if (secretChat == null)
            {
                return;
            }

            var dialog = new ChatTtlPopup();
            dialog.Value = secretChat.Ttl;

            var confirm = await dialog.ShowQueuedAsync();
            if (confirm != ContentDialogResult.Primary)
            {
                return;
            }

            ProtoService.Send(new SendChatSetTtlMessage(chat.Id, dialog.Value));
        }

        #endregion

        #region Supergroup

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

        public virtual ChatMemberCollection CreateMembers(int supergroupId)
        {
            return new ChatMemberCollection(ProtoService, supergroupId, new SupergroupMembersFilterRecent());
        }

        public void Find(string query)
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            if (chat.Type is ChatTypeSupergroup supergroup)
            {
                Search = new ChatMemberCollection(ProtoService, supergroup.SupergroupId, new SupergroupMembersFilterSearch(query));
            }
            else if (chat.Type is ChatTypeBasicGroup basicGroup)
            {
                Search = new ChatMemberCollection(ProtoService, chat.Id, query, new ChatMembersFilterMembers());
            }
        }

        private ChatMemberCollection _search;
        public ChatMemberCollection Search
        {
            get
            {
                return _search;
            }
            set
            {
                Set(ref _search, value);
            }
        }

        #endregion

        #region Context menu

        public RelayCommand<ChatMember> MemberPromoteCommand { get; }
        private void MemberPromoteExecute(ChatMember member)
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            NavigationService.Navigate(typeof(SupergroupEditAdministratorPage), state: NavigationState.GetChatMember(chat.Id, member.UserId));
        }

        public RelayCommand<ChatMember> MemberRestrictCommand { get; }
        private void MemberRestrictExecute(ChatMember member)
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            NavigationService.Navigate(typeof(SupergroupEditRestrictedPage), state: NavigationState.GetChatMember(chat.Id, member.UserId));
        }

        public RelayCommand<ChatMember> MemberRemoveCommand { get; }
        private async void MemberRemoveExecute(ChatMember member)
        {
            var chat = _chat;
            if (chat == null || _members == null)
            {
                return;
            }

            var index = _members.IndexOf(member);

            _members.Remove(member);

            var response = await ProtoService.SendAsync(new SetChatMemberStatus(chat.Id, member.UserId, new ChatMemberStatusBanned()));
            if (response is Error)
            {
                _members.Insert(index, member);
            }
        }

        #endregion



        public async void OpenUsername(string username)
        {
            var response = await ProtoService.SendAsync(new SearchPublicChat(username));
            if (response is Chat chat)
            {
                if (chat.Type is ChatTypePrivate privata)
                {
                    var user = ProtoService.GetUser(privata.UserId);
                    if (user?.Type is UserTypeBot)
                    {
                        NavigationService.NavigateToChat(chat);
                    }
                    else
                    {
                        NavigationService.Navigate(typeof(ProfilePage), chat.Id);
                    }
                }
                else
                {
                    NavigationService.NavigateToChat(chat);
                }
            }
        }

        public async void OpenUser(int userId)
        {
            var response = await ProtoService.SendAsync(new CreatePrivateChat(userId, false));
            if (response is Chat chat)
            {
                var user = ProtoService.GetUser(userId);
                if (user?.Type is UserTypeBot)
                {
                    NavigationService.NavigateToChat(chat);
                }
                else
                {
                    NavigationService.Navigate(typeof(ProfilePage), chat.Id);
                }
            }
        }

        public async void OpenUrl(string url, bool untrust)
        {
            if (MessageHelper.TryCreateUri(url, out Uri uri))
            {
                if (MessageHelper.IsTelegramUrl(uri))
                {
                    MessageHelper.OpenTelegramUrl(ProtoService, NavigationService, uri);
                }
                else
                {
                    //if (message?.Media is TLMessageMediaWebPage webpageMedia)
                    //{
                    //    if (webpageMedia.WebPage is TLWebPage webpage && webpage.HasCachedPage && webpage.Url.Equals(navigation))
                    //    {
                    //        var service = WindowWrapper.Current().NavigationServices.GetByFrameId("Main");
                    //        if (service != null)
                    //        {
                    //            service.Navigate(typeof(InstantPage), webpageMedia);
                    //            return;
                    //        }
                    //    }
                    //}

                    if (untrust)
                    {
                        var confirm = await MessagePopup.ShowAsync(string.Format(Strings.Resources.OpenUrlAlert, url), Strings.Resources.AppName, Strings.Resources.OK, Strings.Resources.Cancel);
                        if (confirm != ContentDialogResult.Primary)
                        {
                            return;
                        }
                    }

                    try
                    {
                        await Windows.System.Launcher.LaunchUriAsync(uri);
                    }
                    catch { }
                }
            }
        }
    }

    public class ChatMemberCollection : IncrementalCollection<ChatMember>
    {
        private readonly IProtoService _protoService;
        private readonly long _chatId;
        private readonly ChatMembersFilter _filter2;
        private readonly string _query;

        private readonly int _supergroupId;
        private readonly SupergroupMembersFilter _filter;

        private bool _hasMore;

        public ChatMemberCollection(IProtoService protoService, long chatId, string query, ChatMembersFilter filter)
        {
            _protoService = protoService;
            _chatId = chatId;
            _filter2 = filter;
            _query = query;
            _hasMore = true;
        }

        public ChatMemberCollection(IProtoService protoService, int supergroupId, SupergroupMembersFilter filter)
        {
            _protoService = protoService;
            _supergroupId = supergroupId;
            _filter = filter;
            _hasMore = true;
        }

        public override async Task<IList<ChatMember>> LoadDataAsync()
        {
            if (_chatId != 0)
            {
                var response = await _protoService.SendAsync(new SearchChatMembers(_chatId, _query, 200, _filter2));
                if (response is ChatMembers members)
                {
                    _hasMore = false;

                    if (_filter2 == null || _filter2 is ChatMembersFilterMembers)
                    {
                        return members.Members.OrderBy(x => x, new ChatMemberComparer(_protoService, true)).ToArray();
                    }

                    return members.Members;
                }
            }
            else
            {
                var response = await _protoService.SendAsync(new GetSupergroupMembers(_supergroupId, _filter, Count, 200));
                if (response is ChatMembers members)
                {
                    if (members.Members.Count < 200)
                    {
                        _hasMore = false;
                    }

                    if ((_filter == null || _filter is SupergroupMembersFilterRecent) && Count == 0 && members.TotalCount <= 200)
                    {
                        return members.Members.OrderBy(x => x, new ChatMemberComparer(_protoService, true)).ToArray();
                    }

                    return members.Members;
                }
            }

            return new ChatMember[0];
        }

        protected override bool GetHasMoreItems()
        {
            return _hasMore;
        }
    }

    public class ChatMemberGroupedCollection : IncrementalCollection<object>
    {
        private readonly IProtoService _protoService;
        private readonly long _chatId;
        private readonly string _query;

        private readonly int _supergroupId;
        private SupergroupMembersFilter _filter;
        private int _offset;

        private bool _group;

        private bool _hasMore;

        public ChatMemberGroupedCollection(IProtoService protoService, long chatId, string query, bool group)
        {
            _protoService = protoService;
            _chatId = chatId;
            _query = query;
            _hasMore = true;
            _group = group;
        }

        public ChatMemberGroupedCollection(IProtoService protoService, int supergroupId, bool group)
        {
            _protoService = protoService;
            _supergroupId = supergroupId;
            _filter = group ? new SupergroupMembersFilterContacts() : null;
            _hasMore = true;
            _group = group;
        }

        public override async Task<IList<object>> LoadDataAsync()
        {
            if (_chatId != 0)
            {
                var response = await _protoService.SendAsync(new SearchChatMembers(_chatId, _query, 200, null));
                if (response is ChatMembers members)
                {
                    _hasMore = false;

                    return members.Members.OrderBy(x => x, new ChatMemberComparer(_protoService, true)).ToArray();
                }
            }
            else
            {
                if (_group)
                {

                    var response = await _protoService.SendAsync(new GetSupergroupMembers(_supergroupId, _filter, _offset, 200));
                    if (response is ChatMembers members)
                    {

                        List<ChatMember> items;
                        if ((_filter == null || _filter is SupergroupMembersFilterRecent) && _offset == 0 && members.TotalCount <= 200)
                        {
                            items = members.Members.OrderBy(x => x, new ChatMemberComparer(_protoService, true)).ToList();
                        }
                        else
                        {
                            items = members.Members.ToList();
                        }

                        for (int i = 0; i < items.Count; i++)
                        {
                            var already = this.OfType<ChatMember>().FirstOrDefault(x => x.UserId == items[i].UserId);
                            if (already != null)
                            {
                                items.RemoveAt(i);
                                i--;
                            }
                        }

                        string title = null;
                        if (_offset == 0)
                        {
                            switch (_filter)
                            {
                                case SupergroupMembersFilterContacts contacts:
                                    title = Strings.Resources.GroupContacts;
                                    break;
                                case SupergroupMembersFilterBots bots:
                                    title = Strings.Resources.ChannelBots;
                                    break;
                                case SupergroupMembersFilterAdministrators administrators:
                                    title = Strings.Resources.ChannelAdministrators;
                                    break;
                                case SupergroupMembersFilterRecent recent:
                                    title = Strings.Resources.ChannelOtherMembers;
                                    break;
                            }
                        }



                        _offset += members.Members.Count;

                        if (members.Members.Count < 200)
                        {
                            switch (_filter)
                            {
                                case SupergroupMembersFilterContacts contacts:
                                    _filter = new SupergroupMembersFilterBots();
                                    _offset = 0;
                                    break;
                                case SupergroupMembersFilterBots bots:
                                    _filter = new SupergroupMembersFilterAdministrators();
                                    _offset = 0;
                                    break;
                                case SupergroupMembersFilterAdministrators administrators:
                                    _filter = new SupergroupMembersFilterRecent();
                                    _offset = 0;
                                    break;
                                case SupergroupMembersFilterRecent recent:
                                    _hasMore = false;
                                    break;
                            }
                        }

                        if (title != null && items.Count > 0)
                        {
                            return new object[] { title }.Union(items).ToArray();
                        }
                        else
                        {
                            return items.Cast<object>().ToArray();
                        }
                    }
                }
                else
                {
                    var response = await _protoService.SendAsync(new GetSupergroupMembers(_supergroupId, _filter, Count, 200));
                    if (response is ChatMembers members)
                    {
                        if (members.Members.Count < 200)
                        {
                            _hasMore = false;
                        }

                        if ((_filter == null || _filter is SupergroupMembersFilterRecent) && Count == 0 && members.TotalCount <= 200)
                        {
                            return members.Members.OrderBy(x => x, new ChatMemberComparer(_protoService, true)).ToArray();
                        }

                        return members.Members.Cast<object>().ToArray();
                    }
                }
            }

            return new ChatMember[0];
        }

        protected override bool GetHasMoreItems()
        {
            return _hasMore;
        }
    }

    public class ChatMemberComparer : IComparer<ChatMember>
    {
        private readonly IProtoService _protoService;
        private readonly bool _epoch;

        public ChatMemberComparer(IProtoService protoService, bool epoch)
        {
            _protoService = protoService;
            _epoch = epoch;
        }

        public int Compare(ChatMember x, ChatMember y)
        {
            var xUser = _protoService.GetUser(x.UserId);
            var yUser = _protoService.GetUser(y.UserId);

            if (xUser == null || yUser == null)
            {
                return -1;
            }

            if (_epoch)
            {
                var epoch = LastSeenConverter.GetIndex(yUser).CompareTo(LastSeenConverter.GetIndex(xUser));
                if (epoch == 0)
                {
                    var fullName = xUser.FirstName.CompareTo(yUser.FirstName);
                    if (fullName == 0)
                    {
                        return yUser.Id.CompareTo(xUser.Id);
                    }

                    return fullName;
                }

                return epoch;
            }
            else
            {
                var fullName = xUser.FirstName.CompareTo(yUser.FirstName);
                if (fullName == 0)
                {
                    return yUser.Id.CompareTo(xUser.Id);
                }

                return fullName;
            }
        }
    }
}
