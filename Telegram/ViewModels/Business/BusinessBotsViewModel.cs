//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Rg.DiffUtils;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Telegram.Collections;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.ViewModels.Folders;
using Telegram.Views.Popups;
using Windows.Foundation;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Navigation;

namespace Telegram.ViewModels.Business
{
    public partial class BusinessBotsViewModel : BusinessFeatureViewModelBase
    {
        public BusinessBotsViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
            Results = new SearchCollection<User, BotsCollection>(UpdateResults, new UserDiffHandler());
        }

        class UserDiffHandler : IDiffHandler<User>
        {
            public bool CompareItems(User oldItem, User newItem)
            {
                return oldItem.Id == newItem.Id;
            }

            public void UpdateItem(User oldItem, User newItem)
            {

            }
        }

        public BotsCollection UpdateResults(object sender, string value)
        {
            return new BotsCollection(ClientService, value);
        }

        private long _botUserId;
        public long BotUserId
        {
            get => _botUserId;
            set => Invalidate(ref _botUserId, value);
        }

        public void Clear()
        {
            BotUserId = 0;
        }

        public SearchCollection<User, BotsCollection> Results { get; private set; }

        #region Manage messages

        private bool? _canManageMessages;
        public bool? CanManageMessages
        {
            get => _canManageMessages;
            set
            {
                var allowed1 = _canManageMessages == true && value == null;
                var allowed2 = _canManageMessages == null && value == false;

                if (allowed1 || allowed2)
                {
                    if (allowed2)
                    {
                        var values = new[]
                        {
                            CanReply,
                            CanReadMessages,
                            CanDeleteSentMessages,
                            CanDeleteAllMessages
                        };

                        allowed2 = values.Count(x => x) > 0;
                    }

                    if (!Set(ref _canManageMessages, allowed1 ? null : allowed2 ? null : true))
                    {
                        RaisePropertyChanged();
                    }

                    Invalidate(ref _canReply, allowed1 ? false : !allowed2, nameof(CanReply));
                    Invalidate(ref _canReadMessages, allowed1 ? false : !allowed2, nameof(CanReadMessages));
                    Invalidate(ref _canDeleteSentMessages, allowed1 ? false : !allowed2, nameof(CanDeleteSentMessages));
                    Invalidate(ref _canDeleteAllMessages, allowed1 ? false : !allowed2, nameof(CanDeleteAllMessages));

                    InvalidateManageMessages(false);
                }
                else
                {
                    RaisePropertyChanged();
                }
            }
        }

        private bool _canReply = true;
        public bool CanReply
        {
            get => _canReply;
            set => InvalidateManageMessages(ref _canReply, value);
        }

        private bool _canReadMessages = true;
        public bool CanReadMessages
        {
            get => _canReadMessages;
            set => InvalidateManageMessages(ref _canReadMessages, value);
        }

        private bool _canDeleteSentMessages = true;
        public bool CanDeleteSentMessages
        {
            get => _canDeleteSentMessages;
            set => InvalidateManageMessages(ref _canDeleteSentMessages, value);
        }

        private bool _canDeleteAllMessages = true;
        public bool CanDeleteAllMessages
        {
            get => _canDeleteAllMessages;
            set => InvalidateManageMessages(ref _canDeleteAllMessages, value);
        }

        private void InvalidateManageMessages<T>(ref T storage, T value, [CallerMemberName] string propertyName = null)
        {
            if (Invalidate(ref storage, value, propertyName))
            {
                InvalidateManageMessages(true);
            }
        }

        private void InvalidateManageMessages(bool update)
        {
            var values = new[]
            {
                CanReply,
                CanReadMessages,
                CanDeleteSentMessages,
                CanDeleteAllMessages
            };

            var count = values.Count(x => x);

            if (update)
            {
                Set(ref _canManageMessages, count == 4 ? true : null, nameof(CanManageMessages));
            }

            Set(ref _manageMessagesCount, $"{count + 1}/5", nameof(ManageMessagesCount));
        }

        private string _manageMessagesCount;
        public string ManageMessagesCount => _manageMessagesCount;

        #endregion

        #region Manage profile

        private bool? _canManageProfile;
        public bool? CanManageProfile
        {
            get => _canManageProfile;
            set
            {
                Set(ref _canManageProfile, value);

                if (value.HasValue)
                {
                    Invalidate(ref _canEditName, value.Value, nameof(CanEditName));
                    Invalidate(ref _canEditBio, value.Value, nameof(CanEditBio));
                    Invalidate(ref _canEditProfilePhoto, value.Value, nameof(CanEditProfilePhoto));
                    Invalidate(ref _canEditUsername, value.Value, nameof(CanEditUsername));

                    InvalidateManageProfile();
                }
            }
        }

        private bool _canEditName = true;
        public bool CanEditName
        {
            get => _canEditName;
            set
            {
                InvalidateManageProfile(ref _canEditName, value);
            }
        }

        private bool _canEditBio = true;
        public bool CanEditBio
        {
            get => _canEditBio;
            set => InvalidateManageProfile(ref _canEditBio, value);
        }

        private bool _canEditProfilePhoto = true;
        public bool CanEditProfilePhoto
        {
            get => _canEditProfilePhoto;
            set => InvalidateManageProfile(ref _canEditProfilePhoto, value);
        }

        private bool _canEditUsername = true;
        public bool CanEditUsername
        {
            get => _canEditUsername;
            set => InvalidateManageProfile(ref _canEditUsername, value);
        }

        private void InvalidateManageProfile<T>(ref T storage, T value, [CallerMemberName] string propertyName = null)
        {
            if (Invalidate(ref storage, value, propertyName))
            {
                InvalidateManageProfile();
            }
        }

        private void InvalidateManageProfile()
        {
            var values = new[]
            {
                CanEditName,
                CanEditBio,
                CanEditProfilePhoto,
                CanEditUsername
            };

            var count = values.Count(x => x);

            Set(ref _canManageProfile, count == 0 ? false : count == 4 ? true : null, nameof(CanManageProfile));
            Set(ref _manageProfileCount, $"{count}/4", nameof(ManageProfileCount));
        }

        private string _manageProfileCount;
        public string ManageProfileCount => _manageProfileCount;

        #endregion

        #region Manage gifts

        private bool? _canManageGifts;
        public bool? CanManageGifts
        {
            get => _canManageGifts;
            set
            {
                Set(ref _canManageGifts, value);

                if (value.HasValue)
                {
                    Set(ref _canViewGiftsAndStars, value.Value, nameof(CanViewGiftsAndStars));
                    Set(ref _canSellGifts, value.Value, nameof(CanSellGifts));
                    Set(ref _canChangeGiftSettings, value.Value, nameof(CanChangeGiftSettings));
                    Set(ref _canTransferGifts, value.Value, nameof(CanTransferGifts));
                    Set(ref _canTransferStars, value.Value, nameof(CanTransferStars));

                    InvalidateManageGifts();
                }
            }
        }

        private bool _canViewGiftsAndStars = true;
        public bool CanViewGiftsAndStars
        {
            get => _canViewGiftsAndStars;
            set => InvalidateManageGifts(ref _canViewGiftsAndStars, value);
        }

        private bool _canSellGifts = true;
        public bool CanSellGifts
        {
            get => _canSellGifts;
            set => InvalidateManageGifts(ref _canSellGifts, value);
        }

        private bool _canChangeGiftSettings = true;
        public bool CanChangeGiftSettings
        {
            get => _canChangeGiftSettings;
            set => InvalidateManageGifts(ref _canChangeGiftSettings, value);
        }

        private bool _canTransferGifts = true;
        public bool CanTransferGifts
        {
            get => _canTransferGifts;
            set => InvalidateManageGifts(ref _canTransferGifts, value);
        }

        private bool _canTransferStars = true;
        public bool CanTransferStars
        {
            get => _canTransferStars;
            set => InvalidateManageGifts(ref _canTransferStars, value);
        }

        private void InvalidateManageGifts<T>(ref T storage, T value, [CallerMemberName] string propertyName = null)
        {
            if (Invalidate(ref storage, value, propertyName))
            {
                InvalidateManageGifts();
            }
        }

        private void InvalidateManageGifts()
        {
            var values = new[]
            {
                CanViewGiftsAndStars,
                CanSellGifts,
                CanChangeGiftSettings,
                CanTransferGifts,
                CanTransferStars
            };

            var count = values.Count(x => x);

            Set(ref _canManageGifts, count == 0 ? false : count == 5 ? true : null, nameof(CanManageGifts));
            Set(ref _manageGiftsCount, $"{count}/5", nameof(ManageGiftsCount));
        }

        private string _manageGiftsCount;
        public string ManageGiftsCount => _manageGiftsCount;

        #endregion

        private bool _canManageStories = true;
        public bool CanManageStories
        {
            get => _canManageStories;
            set => Set(ref _canManageStories, value);
        }

        protected override async Task OnNavigatedToAsync(UserFullInfo cached, NavigationMode mode, NavigationState state)
        {
            var response = await ClientService.SendAsync(new GetBusinessConnectedBot());
            if (response is BusinessConnectedBot connectedBot)
            {
                _cached = connectedBot;

                BotUserId = connectedBot.BotUserId;

                Set(ref _canReply, connectedBot.Rights.CanReply, nameof(CanReply));
                Set(ref _canReadMessages, connectedBot.Rights.CanReadMessages, nameof(CanReadMessages));
                Set(ref _canDeleteSentMessages, connectedBot.Rights.CanDeleteSentMessages, nameof(CanDeleteSentMessages));
                Set(ref _canDeleteAllMessages, connectedBot.Rights.CanDeleteAllMessages, nameof(CanDeleteAllMessages));

                InvalidateManageMessages(true);

                Set(ref _canEditName, connectedBot.Rights.CanEditName, nameof(CanEditName));
                Set(ref _canEditBio, connectedBot.Rights.CanEditBio, nameof(CanEditBio));
                Set(ref _canEditProfilePhoto, connectedBot.Rights.CanEditProfilePhoto, nameof(CanEditProfilePhoto));
                Set(ref _canEditUsername, connectedBot.Rights.CanEditUsername, nameof(CanEditUsername));

                InvalidateManageProfile();

                Set(ref _canViewGiftsAndStars, connectedBot.Rights.CanViewGiftsAndStars, nameof(CanViewGiftsAndStars));
                Set(ref _canSellGifts, connectedBot.Rights.CanSellGifts, nameof(CanSellGifts));
                Set(ref _canChangeGiftSettings, connectedBot.Rights.CanChangeGiftSettings, nameof(CanChangeGiftSettings));
                Set(ref _canTransferGifts, connectedBot.Rights.CanTransferAndUpgradeGifts, nameof(CanTransferGifts));
                Set(ref _canTransferStars, connectedBot.Rights.CanTransferStars, nameof(CanTransferStars));

                InvalidateManageGifts();

                CanManageStories = connectedBot.Rights.CanManageStories;

                UpdateRecipients(connectedBot.Recipients);
            }
        }

        public bool IsExclude
        {
            get => _recipientsType == BusinessRecipientsType.Exclude;
            set
            {
                if (value)
                {
                    SetRecipientsType(BusinessRecipientsType.Exclude);
                }
            }
        }

        public bool IsInclude
        {
            get => _recipientsType == BusinessRecipientsType.Include;
            set
            {
                if (value)
                {
                    SetRecipientsType(BusinessRecipientsType.Include);
                }
            }
        }

        private BusinessRecipientsType _recipientsType;
        public BusinessRecipientsType RecipientsType
        {
            get => _recipientsType;
            set => SetRecipientsType(value);
        }

        private void SetRecipientsType(BusinessRecipientsType value, bool update = true)
        {
            if (Invalidate(ref _recipientsType, value, nameof(RecipientsType)))
            {
                IncludedChats.Clear();
                ExcludedChats.Clear();

                RaisePropertyChanged(nameof(IsExclude));
                RaisePropertyChanged(nameof(IsInclude));
            }
        }

        public MvxObservableCollection<ChatFolderElement> ExcludedChats { get; } = new();
        public MvxObservableCollection<ChatFolderElement> IncludedChats { get; } = new();

        public async void AddExcluded()
        {
            var result = await ChooseChatsPopup.AddExecute(NavigationService, false, IsExclude, true, ExcludedChats.ToList());
            if (result != null)
            {
                ExcludedChats.ReplaceWith(result);

                var ids = result
                    .OfType<FolderChat>()
                    .Select(x => x.ChatId)
                    .ToHashSet();

                var excluded = IncludedChats
                    .OfType<FolderChat>()
                    .ToList();

                foreach (var item in excluded)
                {
                    if (ids.Contains(item.ChatId))
                    {
                        IncludedChats.Remove(item);
                    }
                }

                RaisePropertyChanged(nameof(HasChanged));
            }
        }

        public async void AddIncluded()
        {
            var result = await ChooseChatsPopup.AddExecute(NavigationService, true, true, true, IncludedChats.ToList());
            if (result != null)
            {
                IncludedChats.ReplaceWith(result);

                var ids = result
                    .OfType<FolderChat>()
                    .Select(x => x.ChatId)
                    .ToHashSet();

                var excluded = ExcludedChats
                    .OfType<FolderChat>()
                    .ToList();

                foreach (var item in excluded)
                {
                    if (ids.Contains(item.ChatId))
                    {
                        ExcludedChats.Remove(item);
                    }
                }

                RaisePropertyChanged(nameof(HasChanged));
            }
        }

        public void RemoveIncluded(ChatFolderElement chat)
        {
            IncludedChats.Remove(chat);
            RaisePropertyChanged(nameof(HasChanged));
        }

        public void RemoveExcluded(ChatFolderElement chat)
        {
            ExcludedChats.Remove(chat);
            RaisePropertyChanged(nameof(HasChanged));
        }

        protected void UpdateRecipients(BusinessRecipients recipients)
        {
            SetRecipientsType(recipients.ExcludeSelected
                ? BusinessRecipientsType.Exclude
                : BusinessRecipientsType.Include);

            IncludedChats.Clear();
            ExcludedChats.Clear();

            var target = recipients.ExcludeSelected
                ? ExcludedChats
                : IncludedChats;

            if (recipients.SelectExistingChats) target.Add(new FolderFlag(ChatListFolderFlags.ExistingChats));
            if (recipients.SelectNewChats) target.Add(new FolderFlag(ChatListFolderFlags.NewChats));
            if (recipients.SelectContacts) target.Add(new FolderFlag(ChatListFolderFlags.IncludeContacts));
            if (recipients.SelectNonContacts) target.Add(new FolderFlag(ChatListFolderFlags.IncludeNonContacts));

            foreach (var chatId in recipients.ChatIds)
            {
                IncludedChats.Add(new FolderChat(chatId));
            }

            foreach (var chatId in recipients.ExcludedChatIds)
            {
                ExcludedChats.Add(new FolderChat(chatId));
            }

            RaisePropertyChanged(nameof(HasChanged));
        }

        protected BusinessBotRights GetRights()
        {
            return new BusinessBotRights
            {
                CanReply = CanReply,
                CanReadMessages = CanReadMessages,
                CanDeleteSentMessages = CanDeleteSentMessages,
                CanDeleteAllMessages = CanDeleteAllMessages,

                CanEditName = CanEditName,
                CanEditBio = CanEditBio,
                CanEditProfilePhoto = CanEditProfilePhoto,
                CanEditUsername = CanEditUsername,

                CanViewGiftsAndStars = CanViewGiftsAndStars,
                CanSellGifts = CanSellGifts,
                CanChangeGiftSettings = CanChangeGiftSettings,
                CanTransferAndUpgradeGifts = CanTransferGifts,
                CanTransferStars = CanTransferStars,
            };
        }

        protected BusinessRecipients GetRecipients()
        {
            var recipients = new BusinessRecipients
            {
                ExcludeSelected = RecipientsType == BusinessRecipientsType.Exclude,
                ChatIds = new List<long>(),
                ExcludedChatIds = new List<long>()
            };

            var target = recipients.ExcludeSelected
                ? ExcludedChats
                : IncludedChats;

            foreach (var item in target)
            {
                if (item is FolderFlag flag)
                {
                    if (flag.Flag == ChatListFolderFlags.IncludeContacts) recipients.SelectContacts = true;
                    if (flag.Flag == ChatListFolderFlags.IncludeNonContacts) recipients.SelectNonContacts = true;
                    if (flag.Flag == ChatListFolderFlags.ExistingChats) recipients.SelectExistingChats = true;
                    if (flag.Flag == ChatListFolderFlags.NewChats) recipients.SelectNewChats = true;
                }
            }

            foreach (var item in IncludedChats)
            {
                if (item is FolderChat chat)
                {
                    recipients.ChatIds.Add(chat.ChatId);
                }
            }

            foreach (var item in ExcludedChats)
            {
                if (item is FolderChat chat)
                {
                    recipients.ExcludedChatIds.Add(chat.ChatId);
                }
            }

            return recipients;
        }

        public override bool HasChanged => !_cached.AreTheSame(GetSettings());

        protected override async void ContinueImpl(NavigatingEventArgs args)
        {
            var settings = GetSettings();
            if (settings.AreTheSame(_cached))
            {
                _completed = true;
                NavigationService.GoBack(args);
                return;
            }

            var response = await ClientService.SendAsync(settings == null
                ? new DeleteBusinessConnectedBot(_cached.BotUserId)
                : new SetBusinessConnectedBot(settings));
            if (response is Ok)
            {
                _completed = true;
                NavigationService.GoBack(args);
            }
            else if (response is Error error)
            {
                ShowToast(error);
            }
        }

        private BusinessConnectedBot _cached;
        private BusinessConnectedBot GetSettings()
        {
            if (BotUserId == 0)
            {
                return null;
            }

            return new BusinessConnectedBot
            {
                BotUserId = BotUserId,
                Rights = GetRights(),
                Recipients = GetRecipients()
            };
        }

        public partial class BotsCollection : ObservableCollection<User>, ISupportIncrementalLoading
        {
            private readonly IClientService _clientService;
            private readonly string _query;

            private readonly HashSet<long> _ids = new();

            public BotsCollection(IClientService clientService, string query)
            {
                _clientService = clientService;
                _query = query;
            }

            public IAsyncOperation<LoadMoreItemsResult> LoadMoreItemsAsync(uint count)
            {
                return AsyncInfo.Run(async token =>
                {
                    var totalCount = 0u;

                    void ProcessResult(object result)
                    {
                        if (result is Td.Api.Chats chats)
                        {
                            foreach (var chat in _clientService.GetChats(chats.ChatIds))
                            {
                                if (_clientService.TryGetUser(chat, out User user))
                                {
                                    if (user.Type is UserTypeBot && !_ids.Contains(user.Id))
                                    {
                                        _ids.Add(user.Id);

                                        Add(user);
                                        totalCount++;
                                    }
                                }
                            }
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(_query))
                    {
                        var response1 = await _clientService.SendAsync(new SearchChats(_query, new SearchChatTypeFilterBot(), 50));
                        ProcessResult(response1);

                        var response2 = await _clientService.SendAsync(new SearchChatsOnServer(_query, new SearchChatTypeFilterBot(), 50));
                        ProcessResult(response2);

                        var response3 = await _clientService.SendAsync(new SearchPublicChats(_query, new SearchChatTypeFilterBot()));
                        ProcessResult(response3);
                    }

                    HasMoreItems = false;

                    return new LoadMoreItemsResult
                    {
                        Count = totalCount
                    };
                });
            }

            public bool HasMoreItems { get; private set; } = true;
        }
    }
}
