using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Navigation;
using Windows.UI.Xaml.Controls;
using Windows.UI.Popups;
using Unigram.Controls;
using Template10.Common;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Media;
using Windows.Foundation.Metadata;
using Windows.ApplicationModel.Calls;
using System.Diagnostics;
using Unigram.Views;
using Windows.ApplicationModel.Contacts;
using System.Collections.ObjectModel;
using Unigram.Common;
using System.Linq;
using Unigram.Controls.Views;
using Unigram.Views.Users;
using Unigram.Converters;
using System.Runtime.CompilerServices;
using Unigram.Views.Dialogs;
using Unigram.Services;
using Telegram.Td.Api;
using Unigram.Views.Channels;
using Unigram.Collections;
using Unigram.ViewModels.Chats;
using Unigram.Views.Supergroups;
using Unigram.Views.Chats;
using Unigram.Core.Services;
using Unigram.ViewModels.Delegates;
using Unigram.Views.BasicGroups;

namespace Unigram.ViewModels
{
    public class ProfileViewModel : UnigramViewModelBase,
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
        IHandle<UpdateNotificationSettings>,
        IHandle<UpdateFile>
    {
        public string LastSeen { get; internal set; }

        public IProfileDelegate Delegate { get; set; }

        public ProfileViewModel(IProtoService protoService, ICacheService cacheService, IEventAggregator aggregator)
            : base(protoService, cacheService, aggregator)
        {
            SendMessageCommand = new RelayCommand(SendMessageExecute);
            MediaCommand = new RelayCommand(MediaExecute);
            CommonChatsCommand = new RelayCommand(CommonChatsExecute);
            SystemCallCommand = new RelayCommand(SystemCallExecute);
            BlockCommand = new RelayCommand(BlockExecute);
            UnblockCommand = new RelayCommand(UnblockExecute);
            ReportCommand = new RelayCommand(ReportExecute);
            CallCommand = new RelayCommand(CallExecute);
            AddCommand = new RelayCommand(AddExecute);
            EditCommand = new RelayCommand(EditExecute);
            DeleteCommand = new RelayCommand(DeleteExecute);
            ShareCommand = new RelayCommand(ShareExecute);
            SecretChatCommand = new RelayCommand(SecretChatExecute);
            IdenticonCommand = new RelayCommand(IdenticonExecute);
            MigrateCommand = new RelayCommand(MigrateExecute);
            InviteCommand = new RelayCommand(InviteExecute);
            ToggleMuteCommand = new RelayCommand<bool>(ToggleMuteExecute);
            MemberPromoteCommand = new RelayCommand<ChatMember>(MemberPromoteExecute);
            MemberRestrictCommand = new RelayCommand<ChatMember>(MemberRestrictExecute);
            MemberRemoveCommand = new RelayCommand<ChatMember>(MemberRemoveExecute);

            AdminsCommand = new RelayCommand(AdminsExecute);
            BannedCommand = new RelayCommand(BannedExecute);
            KickedCommand = new RelayCommand(KickedExecute);
            ParticipantsCommand = new RelayCommand(ParticipantsExecute);
            AdminLogCommand = new RelayCommand(AdminLogExecute);
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

        protected ObservableCollection<ChatMember> _members;
        public ObservableCollection<ChatMember> Members
        {
            get
            {
                return _members;
            }
            set
            {
                Set(ref _members, value);
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
                    Delegate?.UpdateUserFullInfo(chat, item, cache, false);
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
                    Delegate?.UpdateUserFullInfo(chat, item, cache, true);
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

            return Task.CompletedTask;
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
                BeginOnUIThread(() => Delegate?.UpdateUserFullInfo(chat, ProtoService.GetUser(update.UserId), update.UserFullInfo, false));
            }
            else if (chat.Type is ChatTypeSecret secret && secret.UserId == update.UserId)
            {
                BeginOnUIThread(() => Delegate?.UpdateUserFullInfo(chat, ProtoService.GetUser(update.UserId), update.UserFullInfo, true));
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

        public void Handle(UpdateNotificationSettings update)
        {
            if (update.Scope is NotificationSettingsScopeChat chat && chat.ChatId == _chat?.Id)
            {
                BeginOnUIThread(() => RaisePropertyChanged(() => Chat));
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

            NavigationService.NavigateToChat(chat);
        }

        public RelayCommand MediaCommand { get; }
        private void MediaExecute()
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            NavigationService.Navigate(typeof(DialogSharedMediaPage), chat.Id);
        }

        public RelayCommand CommonChatsCommand { get; }
        private void CommonChatsExecute()
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            if (chat.Type is ChatTypePrivate privata)
            {
                NavigationService.Navigate(typeof(UserCommonChatsPage), privata.UserId);
            }
            else if (chat.Type is ChatTypeSecret secret)
            {
                NavigationService.Navigate(typeof(UserCommonChatsPage), secret.UserId);
            }
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

            var confirm = await TLMessageDialog.ShowAsync(Strings.Resources.AreYouSureBlockContact, Strings.Resources.AppName, Strings.Resources.OK, Strings.Resources.Cancel);
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

            var confirm = await TLMessageDialog.ShowAsync(Strings.Resources.AreYouSureUnblockContact, Strings.Resources.AppName, Strings.Resources.OK, Strings.Resources.Cancel);
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
                    await ShareView.GetForCurrentView().ShowAsync(new InputMessageContact(new Telegram.Td.Api.Contact(user.PhoneNumber, user.FirstName, user.LastName, user.Id)));
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
            //            await new TLMessageDialog("Resources.ReportSpamNotification", "Unigram").ShowQueuedAsync();
            //        }
            //    }
            //}
        }

        public RelayCommand SecretChatCommand { get; }
        private async void SecretChatExecute()
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            var confirm = await TLMessageDialog.ShowAsync(Strings.Resources.AreYouSureSecretChat, Strings.Resources.AppName, Strings.Resources.OK, Strings.Resources.Cancel);
            if (confirm != ContentDialogResult.Primary)
            {
                return;
            }

            if (chat.Type is ChatTypePrivate privata)
            {
                Function request;

                var existing = ProtoService.GetSecretChatForUser(privata.UserId);
                if (existing != null)
                {
                    request = new CreateSecretChat(existing.Id);
                }
                else
                {
                    request = new CreateNewSecretChat(privata.UserId);
                }

                var response = await ProtoService.SendAsync(request);
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

            var confirm = await TLMessageDialog.ShowAsync(Strings.Resources.ConvertGroupInfo2 + "\n\n" + Strings.Resources.ConvertGroupInfo3, Strings.Resources.ConvertGroup, Strings.Resources.OK, Strings.Resources.Cancel);
            if (confirm != ContentDialogResult.Primary)
            {
                return;
            }

            var warning = await TLMessageDialog.ShowAsync(Strings.Resources.ConvertGroupAlert, Strings.Resources.ConvertGroupAlertWarning, Strings.Resources.OK, Strings.Resources.Cancel);
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
        private void InviteExecute()
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            NavigationService.Navigate(typeof(ChatInvitePage), chat.Id);
        }

        public RelayCommand<bool> ToggleMuteCommand { get; }
        private void ToggleMuteExecute(bool unmute)
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            ProtoService.Send(new SetNotificationSettings(new NotificationSettingsScopeChat(chat.Id), new NotificationSettings(unmute ? 0 : 632053052, chat.NotificationSettings.Sound, chat.NotificationSettings.ShowPreview)));
        }

        #region Call

        public RelayCommand CallCommand { get; }
        private async void CallExecute()
        {
            //if (_item == null || _full == null)
            //{
            //    return;
            //}

            //if (_full.IsPhoneCallsAvailable && !_item.IsSelf && ApiInformation.IsApiContractPresent("Windows.ApplicationModel.Calls.CallsVoipContract", 1))
            //{
            //    try
            //    {
            //        var coordinator = VoipCallCoordinator.GetDefault();
            //        var result = await coordinator.ReserveCallResourcesAsync("Unigram.Tasks.VoIPCallTask");
            //        if (result == VoipPhoneCallResourceReservationStatus.Success)
            //        {
            //            await VoIPConnection.Current.SendRequestAsync("voip.startCall", _item);
            //        }
            //    }
            //    catch
            //    {
            //        await TLMessageDialog.ShowAsync("Something went wrong. Please, try to close and relaunch the app.", "Unigram", "OK");
            //    }
            //}
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

            if (chat.Type is ChatTypePrivate || chat.Type is ChatTypeSecret)
            {
                var user = ProtoService.GetUser(chat);
                if (user == null)
                {
                    return;
                }

                var dialog = new EditUserNameView(user.FirstName, user.LastName);

                var confirm = await dialog.ShowQueuedAsync();
                if (confirm == ContentDialogResult.Primary)
                {
                    ProtoService.Send(new ImportContacts(new[] { new Telegram.Td.Api.Contact(user.PhoneNumber, dialog.FirstName, dialog.LastName, user.Id) }));
                }
            }
        }

        public RelayCommand EditCommand { get; }
        private async void EditExecute()
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            if (chat.Type is ChatTypeSupergroup)
            {
                NavigationService.Navigate(typeof(SupergroupEditPage), chat.Id);
            }
            else if (chat.Type is ChatTypeBasicGroup)
            {
                NavigationService.Navigate(typeof(BasicGroupEditPage), chat.Id);
            }
            else if (chat.Type is ChatTypePrivate || chat.Type is ChatTypeSecret)
            {
                var user = ProtoService.GetUser(chat);
                if (user == null)
                {
                    return;
                }

                var dialog = new EditUserNameView(user.FirstName, user.LastName);

                var confirm = await dialog.ShowQueuedAsync();
                if (confirm == ContentDialogResult.Primary)
                {
                    ProtoService.Send(new ImportContacts(new[] { new Telegram.Td.Api.Contact(user.PhoneNumber, dialog.FirstName, dialog.LastName, user.Id) }));
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

            var message = Strings.Resources.AreYouSureDeleteAndExit;
            if (chat.Type is ChatTypePrivate || chat.Type is ChatTypeSecret)
            {
                message = Strings.Resources.AreYouSureDeleteThisChat;
            }
            else if (chat.Type is ChatTypeSupergroup super)
            {
                message = super.IsChannel ? Strings.Resources.ChannelLeaveAlert : Strings.Resources.MegaLeaveAlert;
            }

            var confirm = await TLMessageDialog.ShowAsync(message, Strings.Resources.AppName, Strings.Resources.OK, Strings.Resources.Cancel);
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
                        await ProtoService.SendAsync(new SetChatMemberStatus(chat.Id, ProtoService.GetMyId(), new ChatMemberStatusLeft()));
                    }

                    ProtoService.Send(new DeleteChatHistory(chat.Id, true));
                }
            }

            //var user = _item as TLUser;
            //if (user == null)
            //{
            //    return;
            //}

            //var confirm = await TLMessageDialog.ShowAsync(Strings.Resources.AreYouSureDeleteContact, Strings.Resources.AppName, Strings.Resources.OK, Strings.Resources.Cancel);
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

            NavigationService.Navigate(typeof(SupergroupRestrictedPage), chat.Id);
        }

        public RelayCommand ParticipantsCommand { get; }
        private void ParticipantsExecute()
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            NavigationService.Navigate(typeof(SupergroupMembersPage), chat.Id);
        }

        public RelayCommand AdminLogCommand { get; }
        private void AdminLogExecute()
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            NavigationService.Navigate(typeof(SupergroupEventLogPage), chat.Id);
        }

        public virtual ChatMemberCollection CreateMembers(int supergroupId)
        {
            return new ChatMemberCollection(ProtoService, supergroupId, new SupergroupMembersFilterRecent());
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

            NavigationService.Navigate(typeof(SupergroupEditAdministratorPage), new ChatMemberNavigation(chat.Id, member.UserId));
        }

        public RelayCommand<ChatMember> MemberRestrictCommand { get; }
        private void MemberRestrictExecute(ChatMember member)
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            NavigationService.Navigate(typeof(SupergroupEditRestrictedPage), new ChatMemberNavigation(chat.Id, member.UserId));
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
            var navigation = url;
            if (navigation.StartsWith("http") == false)
            {
                navigation = "http://" + url;
            }

            if (Uri.TryCreate(navigation, UriKind.Absolute, out Uri uri))
            {
                if (MessageHelper.IsTelegramUrl(uri))
                {
                    //HandleTelegramUrl(message, navigation);
                    MessageHelper.OpenTelegramUrl(ProtoService, NavigationService, url);
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
                        var confirm = await TLMessageDialog.ShowAsync(string.Format(Strings.Resources.OpenUrlAlert, url), Strings.Resources.AppName, Strings.Resources.OK, Strings.Resources.Cancel);
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
        private readonly int _supergroupId;
        private readonly SupergroupMembersFilter _filter;

        private bool _hasMore;

        public ChatMemberCollection(IProtoService protoService, int supergroupId, SupergroupMembersFilter filter)
        {
            _protoService = protoService;
            _supergroupId = supergroupId;
            _filter = filter;
            _hasMore = true;
        }

        public override async Task<IList<ChatMember>> LoadDataAsync()
        {
            var response = await _protoService.SendAsync(new GetSupergroupMembers(_supergroupId, _filter, Count, 200));
            if (response is ChatMembers members)
            {
                if (members.Members.Count < 200)
                {
                    _hasMore = false;
                }

                if (_filter == null && members.TotalCount <= 200)
                {
                    return members.Members.OrderBy(x => x, new ChatMemberComparer(_protoService, true)).ToList();
                }

                return members.Members;
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
