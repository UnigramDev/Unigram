//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Collections;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Converters;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.ViewModels.Delegates;
using Telegram.ViewModels.Profile;
using Telegram.ViewModels.Supergroups;
using Telegram.Views;
using Telegram.Views.Chats;
using Telegram.Views.Popups;
using Telegram.Views.Premium.Popups;
using Telegram.Views.Supergroups;
using Telegram.Views.Supergroups.Popups;
using Telegram.Views.Users;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Telegram.ViewModels
{
    public partial class ProfileViewModel : ProfileTabsViewModel, IDelegable<IProfileDelegate>, IHandle
    {
        public string LastSeen { get; internal set; }

        public IProfileDelegate Delegate { get; set; }

        private readonly IVoipService _voipService;
        private readonly INotificationsService _notificationsService;
        private readonly ITranslateService _translateService;

        public ProfileViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator, IPlaybackService playbackService, IVoipService voipService, INotificationsService notificationsService, IStorageService storageService, ITranslateService translateService)
            : base(clientService, settingsService, storageService, aggregator, playbackService)
        {
            _voipService = voipService;
            _notificationsService = notificationsService;
            _translateService = translateService;

            SetTimerCommand = new RelayCommand<int?>(SetTimer);
        }

        public ITranslateService TranslateService => _translateService;

        public ProfileSavedChatsTabViewModel SavedChatsTab => _savedChatsViewModel;
        public ProfileStoriesTabViewModel PinnedStoriesTab => _pinnedStoriesTabViewModel;
        public ProfileStoriesTabViewModel ArchivedStoriesTab => _archivedStoriesTabViewModel;
        public ProfileGroupsTabViewModel GroupsTab => _groupsTabViewModel;
        public ProfileChannelsTabViewModel ChannelsTab => _channelsTabViewModel;
        public ProfileGiftsTabViewModel GiftsTab => _giftsTabViewModel;
        public SupergroupMembersViewModel MembersTab => _membersTabVieModel;

        protected ObservableCollection<ChatMember> _members;
        public ObservableCollection<ChatMember> Members
        {
            get => _members;
            set => Set(ref _members, value);
        }

        private double _headerHeight;
        public double HeaderHeight
        {
            get => _headerHeight;
            set
            {
                if (Set(ref _headerHeight, value))
                {
                    PinnedStoriesTab.HeaderHeight = value;
                    ArchivedStoriesTab.HeaderHeight = value;
                }
            }
        }

        public long LinkedChatId { get; private set; }

        protected override Task OnNavigatedToAsync(object parameter, NavigationMode mode, NavigationState state)
        {
            if (parameter is ChatSavedMessagesTopicIdNavigationArgs savedMessagesTopicIdArgs)
            {
                parameter = savedMessagesTopicIdArgs.ChatId;

                if (ClientService.TryGetSavedMessagesTopic(savedMessagesTopicIdArgs.SavedMessagesTopicId, out SavedMessagesTopic topic))
                {
                    SavedMessagesTopic = topic;
                }
            }
            else if (parameter is ChatMessageIdNavigationArgs args)
            {
                parameter = args.ChatId;

                if (ClientService.TryGetTopicInfo(args.ChatId, args.MessageId, out ForumTopicInfo info))
                {
                    Topic = info;
                }
            }

            var chatId = (long)parameter;

            Chat = ClientService.GetChat(chatId);

            var chat = _chat;
            if (chat == null)
            {
                return Task.CompletedTask;
            }

            //Subscribe();
            Delegate?.UpdateChat(chat);

            if (chat.Type is ChatTypePrivate privata)
            {
                var item = ClientService.GetUser(privata.UserId);
                var cache = ClientService.GetUserFull(privata.UserId);

                Delegate?.UpdateUser(chat, item, false);
                ClientService.Send(new GetUserFullInfo(privata.UserId));

                if (cache != null)
                {
                    Delegate?.UpdateUserFullInfo(chat, item, cache, false, false);
                }

                if (item.Type is UserTypeBot { CanBeEdited: true })
                {
                    async void UpdateStarCount()
                    {
                        var response = await ClientService.GetStarTransactionsAsync(new MessageSenderUser(item.Id), string.Empty, null, string.Empty, 1);
                        if (response is StarTransactions transactions)
                        {
                            StarCount = transactions.StarCount;
                        }
                    }

                    UpdateStarCount();
                }
            }
            else if (chat.Type is ChatTypeSecret secretType)
            {
                var secret = ClientService.GetSecretChat(secretType.SecretChatId);
                var item = ClientService.GetUser(secretType.UserId);
                var cache = ClientService.GetUserFull(secretType.UserId);

                Delegate?.UpdateSecretChat(chat, secret);

                Delegate?.UpdateUser(chat, item, true);
                ClientService.Send(new GetUserFullInfo(secret.UserId));

                if (cache != null)
                {
                    Delegate?.UpdateUserFullInfo(chat, item, cache, true, false);
                }
            }
            else if (chat.Type is ChatTypeBasicGroup basic)
            {
                var item = ClientService.GetBasicGroup(basic.BasicGroupId);
                var cache = ClientService.GetBasicGroupFull(basic.BasicGroupId);

                Delegate?.UpdateBasicGroup(chat, item);
                ClientService.Send(new GetBasicGroupFullInfo(basic.BasicGroupId));

                if (cache != null)
                {
                    Delegate?.UpdateBasicGroupFullInfo(chat, item, cache);
                }
            }
            else if (chat.Type is ChatTypeSupergroup super)
            {
                var item = ClientService.GetSupergroup(super.SupergroupId);
                var cache = ClientService.GetSupergroupFull(super.SupergroupId);

                Delegate?.UpdateSupergroup(chat, item);
                ClientService.Send(new GetSupergroupFullInfo(super.SupergroupId));

                if (cache != null)
                {
                    Delegate?.UpdateSupergroupFullInfo(chat, item, cache);
                }
            }

            return base.OnNavigatedToAsync(parameter, mode, state);
        }

        public override void Subscribe()
        {
            Aggregator.Subscribe<UpdateUser>(this, Handle)
                .Subscribe<UpdateUserFullInfo>(Handle)
                .Subscribe<UpdateBasicGroup>(Handle)
                .Subscribe<UpdateBasicGroupFullInfo>(Handle)
                .Subscribe<UpdateSupergroup>(Handle)
                .Subscribe<UpdateSupergroupFullInfo>(Handle)
                .Subscribe<UpdateUserStatus>(Handle)
                .Subscribe<UpdateChatTitle>(Handle)
                .Subscribe<UpdateChatPhoto>(Handle)
                .Subscribe<UpdateChatLastMessage>(Handle)
                .Subscribe<UpdateChatEmojiStatus>(Handle)
                .Subscribe<UpdateChatAccentColors>(Handle)
                .Subscribe<UpdateChatActiveStories>(Handle)
                .Subscribe<UpdateChatNotificationSettings>(Handle);
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
                LinkedChatId = update.UserFullInfo.PersonalChatId;
                BeginOnUIThread(() => Delegate?.UpdateUserFullInfo(chat, ClientService.GetUser(update.UserId), update.UserFullInfo, false, false));
            }
            else if (chat.Type is ChatTypeSecret secret && secret.UserId == update.UserId)
            {
                BeginOnUIThread(() => Delegate?.UpdateUserFullInfo(chat, ClientService.GetUser(update.UserId), update.UserFullInfo, true, false));
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
                BeginOnUIThread(() =>
                {
                    MembersTab.UpdateMembers();
                    Delegate?.UpdateBasicGroup(chat, update.BasicGroup);
                });
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
                BeginOnUIThread(() =>
                {
                    MembersTab.UpdateMembers();
                    Delegate?.UpdateBasicGroupFullInfo(chat, ClientService.GetBasicGroup(update.BasicGroupId), update.BasicGroupFullInfo);
                });
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
                BeginOnUIThread(() =>
                {
                    MembersTab.UpdateMembers();
                    Delegate?.UpdateSupergroup(chat, update.Supergroup);
                });
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
                LinkedChatId = update.SupergroupFullInfo.LinkedChatId;
                BeginOnUIThread(() =>
                {
                    MembersTab.UpdateMembers();
                    Delegate?.UpdateSupergroupFullInfo(chat, ClientService.GetSupergroup(update.SupergroupId), update.SupergroupFullInfo);
                });
            }
        }



        public void Handle(UpdateChatTitle update)
        {
            if (update.ChatId == _chat?.Id)
            {
                BeginOnUIThread(() => Delegate?.UpdateChatTitle(_chat));
            }
            else if (update.ChatId == LinkedChatId && ClientService.TryGetChat(LinkedChatId, out Chat linkedChat))
            {
                BeginOnUIThread(() => Delegate?.UpdateChatTitle(linkedChat));
            }
        }

        public void Handle(UpdateChatPhoto update)
        {
            if (update.ChatId == _chat?.Id)
            {
                BeginOnUIThread(() => Delegate?.UpdateChatPhoto(_chat));
            }
            else if (update.ChatId == LinkedChatId && ClientService.TryGetChat(LinkedChatId, out Chat linkedChat))
            {
                BeginOnUIThread(() => Delegate?.UpdateChatPhoto(linkedChat));
            }
        }

        public void Handle(UpdateChatLastMessage update)
        {
            if (update.ChatId == LinkedChatId && ClientService.TryGetChat(LinkedChatId, out Chat linkedChat))
            {
                BeginOnUIThread(() => Delegate?.UpdateChatLastMessage(linkedChat));
            }
        }

        public void Handle(UpdateChatEmojiStatus update)
        {
            if (update.ChatId == _chat?.Id)
            {
                BeginOnUIThread(() => Delegate?.UpdateChatEmojiStatus(_chat));
            }
        }

        public void Handle(UpdateChatAccentColors update)
        {
            if (update.ChatId == _chat?.Id)
            {
                BeginOnUIThread(() => Delegate?.UpdateChatAccentColors(_chat));
            }
        }

        public void Handle(UpdateChatActiveStories update)
        {
            if (update.ActiveStories.ChatId == _chat?.Id)
            {
                BeginOnUIThread(() => Delegate?.UpdateChatActiveStories(_chat));
            }
        }

        public void Handle(UpdateUserStatus update)
        {
            if (_chat?.Type is ChatTypePrivate privata && privata.UserId == update.UserId || _chat?.Type is ChatTypeSecret secret && secret.UserId == update.UserId)
            {
                BeginOnUIThread(() => Delegate?.UpdateUserStatus(_chat, ClientService.GetUser(update.UserId)));
            }
        }

        public void Handle(UpdateChatNotificationSettings update)
        {
            if (update.ChatId == _chat?.Id)
            {
                BeginOnUIThread(() => Delegate?.UpdateChatNotificationSettings(_chat));
            }
        }

        public async void SendMessage()
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            if (chat.Id == ClientService.Options.MyId)
            {
                await ClientService.SendAsync(new ToggleChatViewAsTopics(chat.Id, false));
            }

            if (NavigationService.IsChatOpen(chat.Id))
            {
                NavigationService.GoBack();
            }
            else
            {
                NavigationService.NavigateToChat(chat);
            }
        }

        public void OpenLinkedChannel()
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            if (ClientService.TryGetUserFull(chat, out UserFullInfo userFullInfo))
            {
                NavigationService.NavigateToChat(userFullInfo.PersonalChatId);
            }
            else if (ClientService.TryGetSupergroupFull(chat, out SupergroupFullInfo supergroupFullInfo))
            {
                NavigationService.NavigateToChat(supergroupFullInfo.LinkedChatId);
            }
        }

        public void OpenStatistics()
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            NavigationService.Navigate(typeof(RevenuePage), chat.Id);
        }

        public void OpenBoosts()
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            NavigationService.Navigate(typeof(ChatBoostsPage), chat.Id);
        }

        public void OpenArchivedStories()
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            NavigationService.Navigate(typeof(ChatStoriesPage), new ChatStoriesArgs(chat.Id, ChatStoriesType.Archive));
        }

        public async void Block()
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            var confirm = await ShowPopupAsync(Strings.AreYouSureBlockContact, Strings.AppName, Strings.OK, Strings.Cancel);
            if (confirm != ContentDialogResult.Primary)
            {
                return;
            }

            ToggleIsBlocked(chat, true);
        }

        public async void Unblock()
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            var confirm = await ShowPopupAsync(Strings.AreYouSureUnblockContact, Strings.AppName, Strings.OK, Strings.Cancel);
            if (confirm != ContentDialogResult.Primary)
            {
                return;
            }

            ToggleIsBlocked(chat, false);
        }

        private void ToggleIsBlocked(Chat chat, bool blocked)
        {
            if (chat.Type is ChatTypePrivate privata)
            {
                ClientService.Send(new SetMessageSenderBlockList(new MessageSenderUser(privata.UserId), blocked ? new BlockListMain() : null));
            }
            else if (chat.Type is ChatTypeSecret secret)
            {
                ClientService.Send(new SetMessageSenderBlockList(new MessageSenderUser(secret.UserId), blocked ? new BlockListMain() : null));
            }
        }

        public async void Share()
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            if (chat.Type is ChatTypePrivate or ChatTypeSecret)
            {
                var user = ClientService.GetUser(chat.Type is ChatTypePrivate privata ? privata.UserId : chat.Type is ChatTypeSecret secret ? secret.UserId : 0);
                if (user != null)
                {
                    await ShowPopupAsync(new ChooseChatsPopup(), new ChooseChatsConfigurationPostMessage(new InputMessageContact(new Contact(user.PhoneNumber, user.FirstName, user.LastName, string.Empty, user.Id))));
                }
            }
        }

        public void CopyPhone()
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            var user = ClientService.GetUser(chat);
            if (user == null)
            {
                return;
            }

            MessageHelper.CopyText(XamlRoot, PhoneNumber.Format(user.PhoneNumber));
        }

        public void CopyId()
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            if (ClientService.TryGetUser(chat, out User user))
            {
                MessageHelper.CopyText(XamlRoot, user.Id.ToString());
            }
            else
            {
                MessageHelper.CopyText(XamlRoot, chat.Id.ToString());
            }
        }

        public string CopyDescription()
        {
            var chat = _chat;
            if (chat == null)
            {
                return null;
            }

            if (chat.Type is ChatTypeSupergroup super)
            {
                var supergroup = ClientService.GetSupergroupFull(super.SupergroupId);
                if (supergroup == null)
                {
                    return null;
                }

                return supergroup.Description;
            }
            else
            {
                var user = ClientService.GetUserFull(chat);
                if (user == null)
                {
                    return null;
                }

                return user.BotInfo?.ShortDescription ?? user.Bio.Text;
            }
        }

        public void CopyUsername()
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            if (chat.Type is ChatTypeSupergroup super)
            {
                var supergroup = ClientService.GetSupergroup(super.SupergroupId);
                if (supergroup == null || !supergroup.HasActiveUsername(out string username))
                {
                    return;
                }

                MessageHelper.CopyText(XamlRoot, $"@{username}");
            }
            else
            {
                var user = ClientService.GetUser(chat);
                if (user == null || !user.HasActiveUsername(out string username))
                {
                    return;
                }

                MessageHelper.CopyText(XamlRoot, $"@{username}");
            }
        }

        public void CopyUsernameLink()
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            if (chat.Type is ChatTypeSupergroup super)
            {
                var supergroup = ClientService.GetSupergroup(super.SupergroupId);
                if (supergroup == null || !supergroup.HasActiveUsername(out string username))
                {
                    return;
                }

                MessageHelper.CopyLink(ClientService, XamlRoot, new InternalLinkTypePublicChat(username, string.Empty, false));
            }
            else
            {
                var user = ClientService.GetUser(chat);
                if (user == null || !user.HasActiveUsername(out string username))
                {
                    return;
                }

                MessageHelper.CopyLink(ClientService, XamlRoot, new InternalLinkTypePublicChat(username, string.Empty, false));
            }
        }

        public async void GiftPremium()
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            if (ClientService.TryGetUser(chat, out User user)
                && ClientService.TryGetUserFull(chat, out UserFullInfo userFull))
            {
                await ShowPopupAsync(new GiftPopup(ClientService, NavigationService, user, userFull.PremiumGiftOptions));
            }
        }

        public async void CreateSecretChat()
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            var confirm = await ShowPopupAsync(Strings.AreYouSureSecretChat, Strings.AreYouSureSecretChatTitle, Strings.Start, Strings.Cancel);
            if (confirm != ContentDialogResult.Primary)
            {
                return;
            }

            if (chat.Type is ChatTypePrivate privata)
            {
                var response = await ClientService.SendAsync(new CreateNewSecretChat(privata.UserId));
                if (response is Chat result)
                {
                    NavigationService.NavigateToChat(result);
                }
            }
        }

        public async void ShowIdenticon()
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            await ShowPopupAsync(new IdenticonPopup(SessionId, chat));
        }

        public async void Invite()
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            if (chat.Type is ChatTypePrivate or ChatTypeSecret)
            {
                var user = ClientService.GetUser(chat);
                if (user == null || user.Type is not UserTypeBot)
                {
                    return;
                }

                await ShowPopupAsync(new ChooseChatsPopup(), new ChooseChatsConfigurationStartBot(user));
            }
            else
            {
                MembersTab.Add();
            }
        }

        public void PrivacyPolicy()
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            if (ClientService.TryGetUserFull(chat, out UserFullInfo fullInfo))
            {
                if (fullInfo.BotInfo?.PrivacyPolicyUrl.Length > 0)
                {
                    MessageHelper.OpenUrl(null, null, fullInfo.BotInfo.PrivacyPolicyUrl);
                }
                else if (fullInfo.BotInfo.Commands.Any(x => string.Equals(x.Command, "privacy", StringComparison.OrdinalIgnoreCase)))
                {
                    ClientService.Send(new SendMessage(chat.Id, 0, null, null, null, new InputMessageText(new FormattedText("/privacy", Array.Empty<TextEntity>()), null, false)));
                    SendMessage();
                }
                else
                {
                    MessageHelper.OpenUrl(null, null, Strings.BotDefaultPrivacyPolicy);
                }
            }
        }

        public void Mute()
        {
            ToggleMute(false);
        }

        public void Unmute()
        {
            ToggleMute(true);
        }

        public void ToggleMute()
        {
            ToggleMute(ClientService.Notifications.IsMuted(_chat));
        }

        private void ToggleMute(bool unmute)
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            _notificationsService.SetMuteFor(chat, ClientService.Notifications.IsMuted(chat) ? 0 : 632053052, XamlRoot);
        }

        public async void OpenUsernameInfo(string username)
        {
            var type = new CollectibleItemTypeUsername(username);
            var response = await ClientService.SendAsync(new GetCollectibleItemInfo(type));

            if (response is CollectibleItemInfo info)
            {
                await ShowPopupAsync(new CollectiblePopup(ClientService, Chat, info, type));
            }
        }

        public async void OpenPhoneInfo()
        {
            if (ClientService.TryGetUser(_chat, out User user))
            {
                var type = new CollectibleItemTypePhoneNumber(user.PhoneNumber);
                var response = await ClientService.SendAsync(new GetCollectibleItemInfo(type));

                if (response is CollectibleItemInfo info)
                {
                    await ShowPopupAsync(new CollectiblePopup(ClientService, Chat, info, type));
                }
            }
        }

        #region Show last seen

        public async void ShowLastSeen()
        {
            if (ClientService.TryGetUser(_chat, out User user))
            {
                var popup = new ChangePrivacyPopup(user, ChangePrivacyType.LastSeen, IsPremium, IsPremiumAvailable);

                var confirm = await ShowPopupAsync(popup);
                if (confirm == ContentDialogResult.Primary)
                {
                    ClientService.Send(new SetUserPrivacySettingRules(new UserPrivacySettingShowStatus(), new UserPrivacySettingRules(new UserPrivacySettingRule[] { new UserPrivacySettingRuleAllowAll() })));
                    ShowToast(Strings.PremiumLastSeenSet, ToastPopupIcon.Info);
                }
                else if (confirm == ContentDialogResult.Secondary && IsPremiumAvailable && !IsPremium)
                {
                    NavigationService.ShowPromo(new PremiumSourceFeature(new PremiumFeatureAdvancedChatManagement()));
                }
            }
        }

        #endregion

        #region Search

        public void Search()
        {
            OpenSearch(string.Empty);
        }

        public void OpenSearch(string query)
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            if (NavigationService.IsChatOpen(chat.Id))
            {
                NavigationService.GoBack(new NavigationState { { "search", query } });
            }
            else
            {
                NavigationService.NavigateToChat(chat, state: new NavigationState { { "search", query } });
            }
        }

        #endregion

        #region Call

        public void VoiceCall()
        {
            Call(false);
        }

        public void VideoCall()
        {
            Call(true);
        }

        private void Call(bool video)
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            if (chat.Type is ChatTypePrivate or ChatTypeSecret)
            {
                _voipService.StartPrivateCall(NavigationService, chat, video);
            }
            else if (chat.VideoChat.GroupCallId == 0)
            {
                _voipService.CreateGroupCall(NavigationService, chat.Id);
            }
            else
            {
                _voipService.JoinGroupCall(NavigationService, chat.Id);
            }
        }

        #endregion

        public void AddToContacts()
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            var user = ClientService.GetUser(chat);
            if (user == null)
            {
                return;
            }

            NavigationService.Navigate(typeof(UserEditPage), user.Id);
        }

        public async void Edit()
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            if (_topic != null)
            {
                var popup = new SupergroupTopicPopup(ClientService, _topic);

                var confirm = await ShowPopupAsync(popup);
                if (confirm == ContentDialogResult.Primary)
                {
                    ClientService.Send(new EditForumTopic(chat.Id, _topic.MessageThreadId, popup.Name, true, popup.SelectedEmojiId));
                }
            }
            else if (chat.Type is ChatTypeSupergroup or ChatTypeBasicGroup)
            {
                NavigationService.Navigate(typeof(SupergroupEditPage), chat.Id);
            }
            else if (chat.Type is ChatTypePrivate or ChatTypeSecret)
            {
                AddToContacts();
            }
        }

        public void Discuss()
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            if (chat.Type is ChatTypeSupergroup)
            {
                var fullInfo = ClientService.GetSupergroupFull(chat);
                if (fullInfo == null)
                {
                    return;
                }

                NavigationService.NavigateToChat(fullInfo.LinkedChatId);
            }
        }

        public void Join()
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            ClientService.Send(new JoinChat(chat.Id));
        }

        public async void ShowPromo()
        {
            if (Chat?.EmojiStatus != null)
            {
                var response = await ClientService.SendAsync(new GetCustomEmojiStickers(new[] { Chat.EmojiStatus.CustomEmojiId }));
                if (response is Stickers stickers)
                {
                    var second = await ClientService.SendAsync(new GetStickerSet(stickers.StickersValue[0].SetId));
                    if (second is StickerSet stickerSet)
                    {
                        NavigationService.ShowPopup(new PromoPopup(ClientService, Chat, stickerSet));
                        return;
                    }
                }
            }

            if (ClientService.TryGetUser(Chat, out User user) && user.IsPremium)
            {
                NavigationService.ShowPopup(new PromoPopup(ClientService, Chat, null));
            }
        }

        public async void DeleteChat()
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            Logger.Info(chat.Type);

            var updated = await ClientService.SendAsync(new GetChat(chat.Id)) as Chat ?? chat;
            var dialog = new DeleteChatPopup(ClientService, updated, null, false);

            var confirm = await ShowPopupAsync(dialog);
            if (confirm == ContentDialogResult.Primary)
            {
                var check = dialog.IsChecked == true;

                if (updated.Type is ChatTypeSecret secret)
                {
                    await ClientService.SendAsync(new CloseSecretChat(secret.SecretChatId));
                }
                else if (updated.Type is ChatTypeBasicGroup or ChatTypeSupergroup)
                {
                    await ClientService.SendAsync(new LeaveChat(updated.Id));
                }

                var user = ClientService.GetUser(updated);
                if (user != null && user.Type is UserTypeRegular)
                {
                    ClientService.Send(new DeleteChatHistory(updated.Id, true, check));
                }
                else
                {
                    if (updated.Type is ChatTypePrivate privata && check)
                    {
                        await ClientService.SendAsync(new SetMessageSenderBlockList(new MessageSenderUser(privata.UserId), new BlockListMain()));
                    }

                    ClientService.Send(new DeleteChatHistory(updated.Id, true, false));
                }
            }
        }

        public async void Delete()
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            var message = Strings.AreYouSureDeleteAndExit;
            if (chat.Type is ChatTypePrivate or ChatTypeSecret)
            {
                message = Strings.AreYouSureDeleteContact;
            }
            else if (chat.Type is ChatTypeSupergroup super)
            {
                message = super.IsChannel ? Strings.ChannelLeaveAlert : Strings.MegaLeaveAlert;
            }

            var confirm = await ShowPopupAsync(message, Strings.AppName, Strings.OK, Strings.Cancel);
            if (confirm == ContentDialogResult.Primary)
            {
                if (chat.Type is ChatTypePrivate privata)
                {
                    ClientService.Send(new RemoveContacts(new[] { privata.UserId }));
                }
                else if (chat.Type is ChatTypeSecret secret)
                {
                    ClientService.Send(new RemoveContacts(new[] { secret.UserId }));
                }
                else
                {
                    if (chat.Type is ChatTypeBasicGroup or ChatTypeSupergroup)
                    {
                        await ClientService.SendAsync(new LeaveChat(chat.Id));
                    }

                    ClientService.Send(new DeleteChatHistory(chat.Id, true, false));
                }
            }

            //var user = _item as TLUser;
            //if (user == null)
            //{
            //    return;
            //}

            //var confirm = await ShowPopupAsync(Strings.AreYouSureDeleteContact, Strings.AppName, Strings.OK, Strings.Cancel);
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

            //    var dialog = ClientService.GetDialog(_item.ToPeer());
            //    if (dialog != null)
            //    {
            //        dialog.RaisePropertyChanged(() => dialog.With);
            //    }
            //}
        }

        #region Mute for

        public async void MuteFor(int? value)
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            if (value is int update)
            {
                _notificationsService.SetMuteFor(chat, update, XamlRoot);
            }
            else
            {
                var mutedFor = Settings.Notifications.GetMuteFor(chat);
                var popup = new ChatMutePopup(mutedFor);

                var confirm = await ShowPopupAsync(popup);
                if (confirm != ContentDialogResult.Primary)
                {
                    return;
                }

                if (mutedFor != popup.Value)
                {
                    _notificationsService.SetMuteFor(chat, popup.Value, XamlRoot);
                }
            }
        }

        #endregion

        #region Set timer

        public RelayCommand<int?> SetTimerCommand { get; }
        private async void SetTimer(int? ttl)
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            if (ttl is int value)
            {
                ClientService.Send(new SetChatMessageAutoDeleteTime(chat.Id, value));
            }
            else
            {
                var dialog = new ChatTtlPopup(chat.Type is ChatTypeSecret ? ChatTtlType.Secret : ChatTtlType.Normal);
                dialog.Value = chat.MessageAutoDeleteTime;

                var confirm = await ShowPopupAsync(dialog);
                if (confirm != ContentDialogResult.Primary)
                {
                    return;
                }

                ClientService.Send(new SetChatMessageAutoDeleteTime(chat.Id, dialog.Value));
            }
        }

        #endregion

        public void OpenMainWebApp()
        {
            if (_chat == null || !ClientService.TryGetUser(_chat, out User user))
            {
                return;
            }

            MessageHelper.NavigateToMainWebApp(ClientService, NavigationService, user, string.Empty);
        }

        #region Supergroup

        public void OpenSimilarChat(Chat chat)
        {
            ClientService.Send(new OpenChatSimilarChat(_chat.Id, chat.Id));
            NavigationService.NavigateToChat(chat);
        }

        public void OpenSavedMessagesTopic(SavedMessagesTopic topic)
        {
            NavigationService.NavigateToChat(_chat.Id, savedMessagesTopicId: topic.Id);
        }

        private long? _starCount;
        public long? StarCount
        {
            get => _starCount;
            set => Set(ref _starCount, value);
        }

        public void OpenBalance()
        {
            var chat = _chat;
            if (chat == null || chat.Type is not ChatTypePrivate privata)
            {
                return;
            }

            NavigationService.Navigate(typeof(ChatStarsPage), new MessageSenderUser(privata.UserId));
        }

        public void OpenAdmins()
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            NavigationService.Navigate(typeof(SupergroupAdministratorsPage), chat.Id);
        }

        public void OpenBanned()
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            NavigationService.Navigate(typeof(SupergroupBannedPage), chat.Id);
        }

        public void OpenKicked()
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            NavigationService.Navigate(typeof(SupergroupPermissionsPage), chat.Id);
        }

        public void OpenMembers()
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            NavigationService.Navigate(typeof(SupergroupMembersPage), chat.Id);
        }

        public virtual ChatMemberCollection CreateMembers(long supergroupId)
        {
            return new ChatMemberCollection(ClientService, supergroupId, new SupergroupMembersFilterRecent());
        }

        #endregion

        #region Context menu

        public void PromoteMember(ChatMember member)
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            NavigationService.ShowPopupAsync(new SupergroupEditAdministratorPopup(), new SupergroupEditMemberArgs(chat.Id, member.MemberId));
        }

        public void RestrictMember(ChatMember member)
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            NavigationService.ShowPopupAsync(new SupergroupEditRestrictedPopup(), new SupergroupEditMemberArgs(chat.Id, member.MemberId));
        }

        public async void RemoveMember(ChatMember member)
        {
            var chat = _chat;
            if (chat == null || _members == null)
            {
                return;
            }

            var index = _members.IndexOf(member);

            _members.Remove(member);

            var response = await ClientService.SendAsync(new SetChatMemberStatus(chat.Id, member.MemberId, new ChatMemberStatusBanned()));
            if (response is Error)
            {
                _members.Insert(index, member);
            }
        }

        #endregion

    }

    public partial class ChatMemberCollection : LegacyIncrementalCollection<ChatMember>
    {
        private readonly IClientService _clientService;
        private readonly long _chatId;
        private readonly ChatMembersFilter _filter2;
        private readonly string _query;

        private readonly long _supergroupId;
        private readonly SupergroupMembersFilter _filter;

        private bool _hasMore;

        public ChatMemberCollection(IClientService clientService, long chatId, string query, ChatMembersFilter filter)
        {
            _clientService = clientService;
            _chatId = chatId;
            _filter2 = filter;
            _query = query;
            _hasMore = true;
        }

        public ChatMemberCollection(IClientService clientService, long supergroupId, SupergroupMembersFilter filter)
        {
            _clientService = clientService;
            _supergroupId = supergroupId;
            _filter = filter;
            _hasMore = true;
        }

        public override async Task<IList<ChatMember>> LoadDataAsync()
        {
            if (_chatId != 0)
            {
                var response = await _clientService.SendAsync(new SearchChatMembers(_chatId, _query, 200, _filter2));
                if (response is ChatMembers members)
                {
                    _hasMore = false;

                    if (_filter2 is null or ChatMembersFilterMembers)
                    {
                        return members.Members.OrderBy(x => x, new ChatMemberComparer(_clientService, true)).ToArray();
                    }

                    return members.Members;
                }
            }
            else
            {
                var response = await _clientService.SendAsync(new GetSupergroupMembers(_supergroupId, _filter, Count, 200));
                if (response is ChatMembers members)
                {
                    if (members.Members.Count < 200)
                    {
                        _hasMore = false;
                    }

                    if ((_filter == null || _filter is SupergroupMembersFilterRecent) && Count == 0 && members.TotalCount <= 200)
                    {
                        return members.Members.OrderBy(x => x, new ChatMemberComparer(_clientService, true)).ToArray();
                    }

                    return members.Members;
                }
            }

            return Array.Empty<ChatMember>();
        }

        protected override bool GetHasMoreItems()
        {
            return _hasMore;
        }
    }

    public partial class ChatMemberGroupedCollection : LegacyIncrementalCollection<object>
    {
        private readonly IClientService _clientService;
        private readonly long _chatId;
        private readonly string _query;

        private readonly long _supergroupId;
        private SupergroupMembersFilter _filter;
        private int _offset;

        private readonly bool _group;

        private bool _hasMore;

        public ChatMemberGroupedCollection(IClientService clientService, long chatId, string query, bool group)
        {
            _clientService = clientService;
            _chatId = chatId;
            _query = query;
            _hasMore = true;
            _group = group;
        }

        public ChatMemberGroupedCollection(IClientService clientService, long supergroupId, bool group)
        {
            _clientService = clientService;
            _supergroupId = supergroupId;
            _filter = group ? new SupergroupMembersFilterContacts() : null;
            _hasMore = true;
            _group = group;
        }

        public override async Task<IList<object>> LoadDataAsync()
        {
            if (_chatId != 0)
            {
                var response = await _clientService.SendAsync(new SearchChatMembers(_chatId, _query, 200, null));
                if (response is ChatMembers members)
                {
                    _hasMore = false;

                    return members.Members.OrderBy(x => x, new ChatMemberComparer(_clientService, true)).ToArray();
                }
            }
            else
            {
                if (_group)
                {

                    var response = await _clientService.SendAsync(new GetSupergroupMembers(_supergroupId, _filter, _offset, 200));
                    if (response is ChatMembers members)
                    {

                        List<ChatMember> items;
                        if ((_filter == null || _filter is SupergroupMembersFilterRecent) && _offset == 0 && members.TotalCount <= 200)
                        {
                            items = members.Members.OrderBy(x => x, new ChatMemberComparer(_clientService, true)).ToList();
                        }
                        else
                        {
                            items = members.Members.ToList();
                        }

                        for (int i = 0; i < items.Count; i++)
                        {
                            var already = this.OfType<ChatMember>().FirstOrDefault(x => x.MemberId.AreTheSame(items[i].MemberId));
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
                                    title = Strings.GroupContacts;
                                    break;
                                case SupergroupMembersFilterBots bots:
                                    title = Strings.ChannelBots;
                                    break;
                                case SupergroupMembersFilterAdministrators administrators:
                                    title = Strings.ChannelAdministrators;
                                    break;
                                case SupergroupMembersFilterRecent recent:
                                    title = Strings.ChannelOtherMembers;
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
                    var response = await _clientService.SendAsync(new GetSupergroupMembers(_supergroupId, _filter, Count, 200));
                    if (response is ChatMembers members)
                    {
                        if (members.Members.Count < 200)
                        {
                            _hasMore = false;
                        }

                        if ((_filter == null || _filter is SupergroupMembersFilterRecent) && Count == 0 && members.TotalCount <= 200)
                        {
                            return members.Members.OrderBy(x => x, new ChatMemberComparer(_clientService, true)).ToArray();
                        }

                        return members.Members.Cast<object>().ToArray();
                    }
                }
            }

            return Array.Empty<ChatMember>();
        }

        protected override bool GetHasMoreItems()
        {
            return _hasMore;
        }
    }

    public partial class ChatMemberComparer : IComparer<ChatMember>
    {
        private readonly IClientService _clientService;
        private readonly bool _epoch;

        public ChatMemberComparer(IClientService clientService, bool epoch)
        {
            _clientService = clientService;
            _epoch = epoch;
        }

        public int Compare(ChatMember x, ChatMember y)
        {
            _clientService.TryGetUser(x.MemberId, out User xUser);
            _clientService.TryGetUser(y.MemberId, out User yUser);

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
