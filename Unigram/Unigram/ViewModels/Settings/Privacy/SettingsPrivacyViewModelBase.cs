using System.Collections.Generic;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Collections;
using Unigram.Common;
using Unigram.Services;
using Unigram.Views.Popups;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels.Settings
{
    public class SettingsPrivacyViewModelBase : TLMultipleViewModelBase, IHandle<UpdateUserPrivacySettingRules>
    {
        private readonly UserPrivacySetting _inputKey;

        private UserPrivacySettingRules _rules;

        public SettingsPrivacyViewModelBase(IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator, UserPrivacySetting inputKey)
            : base(protoService, cacheService, settingsService, aggregator)
        {
            _inputKey = inputKey;

            AlwaysCommand = new RelayCommand(AlwaysExecute);
            NeverCommand = new RelayCommand(NeverExecute);
            SendCommand = new RelayCommand(SendExecute);

            Aggregator.Subscribe(this);
        }

        public override Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            //if (mode != NavigationMode.Back)
            {
                UpdatePrivacyAsync();
            }

            return base.OnNavigatedToAsync(parameter, mode, state);
        }

        //public void Handle(TLUpdatePrivacy update)
        //{
        //    if (update.Key.TypeId == _key)
        //    {
        //        UpdatePrivacy(new TLAccountPrivacyRules { Rules = update.Rules });
        //    }
        //}

        public void Handle(UpdateUserPrivacySettingRules update)
        {
            if (update.Setting.TypeEquals(_inputKey))
            {
                UpdatePrivacy(update.Rules);
            }
        }

        private void UpdatePrivacyAsync()
        {
            ProtoService.Send(new GetUserPrivacySettingRules(_inputKey), result =>
            {
                if (result is UserPrivacySettingRules rules)
                {
                    UpdatePrivacy(rules);
                }
            });
        }

        private void UpdatePrivacy(UserPrivacySettingRules rules)
        {
            _rules = rules;
            var badge = string.Empty;
            PrivacyValue? primary = null;
            var restricted = 0;
            var allowed = 0;
            UserPrivacySettingRuleAllowUsers allowedUsers = null;
            UserPrivacySettingRuleAllowChatMembers allowedChatMembers = null;
            UserPrivacySettingRuleRestrictUsers restrictedUsers = null;
            UserPrivacySettingRuleRestrictChatMembers restrictedChatMembers = null;
            foreach (var current in rules.Rules)
            {
                if (current is UserPrivacySettingRuleAllowAll && primary == null)
                {
                    primary = PrivacyValue.AllowAll;
                    badge = Strings.Resources.LastSeenEverybody;
                }
                else if (current is UserPrivacySettingRuleAllowContacts && primary == null)
                {
                    primary = PrivacyValue.AllowContacts;
                    badge = Strings.Resources.LastSeenContacts;
                }
                else if (current is UserPrivacySettingRuleRestrictAll && primary == null)
                {
                    primary = PrivacyValue.DisallowAll;
                    badge = Strings.Resources.LastSeenNobody;
                }
                else if (current is UserPrivacySettingRuleRestrictUsers disallowUsers)
                {
                    restrictedUsers = disallowUsers;
                    restricted += disallowUsers.UserIds.Count;
                }
                else if (current is UserPrivacySettingRuleAllowUsers allowUsers)
                {
                    allowedUsers = allowUsers;
                    allowed += allowUsers.UserIds.Count;
                }
                else if (current is UserPrivacySettingRuleRestrictChatMembers restrictChatMembers)
                {
                    restrictedChatMembers = restrictChatMembers;

                    foreach (var chatId in restrictChatMembers.ChatIds)
                    {
                        var chat = CacheService.GetChat(chatId);
                        if (chat == null)
                        {
                            continue;
                        }

                        if (CacheService.TryGetBasicGroup(chat, out BasicGroup basicGroup))
                        {
                            restricted += basicGroup.MemberCount;
                        }
                        else if (CacheService.TryGetSupergroup(chat, out Supergroup supergroup))
                        {
                            restricted += supergroup.MemberCount;
                        }
                    }
                }
                else if (current is UserPrivacySettingRuleAllowChatMembers allowChatMembers)
                {
                    allowedChatMembers = allowChatMembers;

                    foreach (var chatId in allowChatMembers.ChatIds)
                    {
                        var chat = CacheService.GetChat(chatId);
                        if (chat == null)
                        {
                            continue;
                        }

                        if (CacheService.TryGetBasicGroup(chat, out BasicGroup basicGroup))
                        {
                            allowed += basicGroup.MemberCount;
                        }
                        else if (CacheService.TryGetSupergroup(chat, out Supergroup supergroup))
                        {
                            allowed += supergroup.MemberCount;
                        }
                    }
                }
            }

            if (primary == null)
            {
                primary = PrivacyValue.DisallowAll;
                badge = Strings.Resources.LastSeenNobody;
            }

            var list = new List<string>();
            if (restricted > 0)
            {
                list.Add("-" + restricted);
            }
            if (allowed > 0)
            {
                list.Add("+" + allowed);
            }

            if (list.Count > 0)
            {
                badge = string.Format("{0} ({1})", badge, string.Join(", ", list));
            }

            _restrictedUsers = restrictedUsers ?? new UserPrivacySettingRuleRestrictUsers(new int[0]);
            _restrictedChatMembers = restrictedChatMembers ?? new UserPrivacySettingRuleRestrictChatMembers(new long[0]);

            _allowedUsers = allowedUsers ?? new UserPrivacySettingRuleAllowUsers(new int[0]);
            _allowedChatMembers = allowedChatMembers ?? new UserPrivacySettingRuleAllowChatMembers(new long[0]);

            BeginOnUIThread(() =>
            {
                SelectedItem = primary ?? PrivacyValue.DisallowAll;

                Badge = badge;
                AllowedBadge = allowed > 0 ? Locale.Declension("Users", allowed) : Strings.Resources.EmpryUsersPlaceholder;
                RestrictedBadge = restricted > 0 ? Locale.Declension("Users", restricted) : Strings.Resources.EmpryUsersPlaceholder;
            });

        }

        private string _badge;
        public string Badge
        {
            get
            {
                return _badge;
            }
            set
            {
                Set(ref _badge, value);
            }
        }

        private PrivacyValue _selectedItem;
        public PrivacyValue SelectedItem
        {
            get
            {
                return _selectedItem;
            }
            set
            {
                Set(ref _selectedItem, value);
            }
        }

        private string _allowedBadge;
        public string AllowedBadge
        {
            get => _allowedBadge;
            set => Set(ref _allowedBadge, value);
        }

        private UserPrivacySettingRuleAllowUsers _allowedUsers;
        private UserPrivacySettingRuleAllowChatMembers _allowedChatMembers;



        private string _restrictedBadge;
        public string RestrictedBadge
        {
            get => _restrictedBadge;
            set => Set(ref _restrictedBadge, value);
        }

        private UserPrivacySettingRuleRestrictUsers _restrictedUsers;
        private UserPrivacySettingRuleRestrictChatMembers _restrictedChatMembers;

        public RelayCommand AlwaysCommand { get; }
        public async void AlwaysExecute()
        {
            if (_allowedUsers == null ||
                _allowedChatMembers == null)
            {
                return;
            }

            var chats = new List<long>();
            var users = new List<int>();

            foreach (var id in _allowedUsers.UserIds)
            {
                var chat = await ProtoService.SendAsync(new CreatePrivateChat(id, true)) as Chat;
                if (chat != null)
                {
                    chats.Add(chat.Id);
                }
            }

            foreach (var id in _allowedChatMembers.ChatIds)
            {
                chats.Add(id);
            }

            var dialog = SharePopup.GetForCurrentView();
            dialog.ViewModel.AllowEmptySelection = true;

            switch (_inputKey)
            {
                case UserPrivacySettingAllowCalls allowCalls:
                case UserPrivacySettingAllowPeerToPeerCalls allowP2PCalls:
                case UserPrivacySettingAllowChatInvites allowChatInvites:
                case UserPrivacySettingShowProfilePhoto showProfilePhoto:
                case UserPrivacySettingShowLinkInForwardedMessages showLinkInForwardedMessages:
                default:
                    dialog.ViewModel.Title = Strings.Resources.AlwaysAllow;
                    break;
                case UserPrivacySettingShowStatus showStatus:
                    dialog.ViewModel.Title = Strings.Resources.AlwaysShareWithTitle;
                    break;
            }

            var confirm = await dialog.PickAsync(chats, SearchChatsType.PrivateAndGroups);
            if (confirm != ContentDialogResult.Primary)
            {
                return;
            }

            chats.Clear();
            users.Clear();

            foreach (var chat in dialog.ViewModel.SelectedItems)
            {
                if (chat.Type is ChatTypePrivate privata)
                {
                    users.Add(privata.UserId);
                }
                else
                {
                    chats.Add(chat.Id);
                }
            }

            _allowedUsers = new UserPrivacySettingRuleAllowUsers(users);
            _allowedChatMembers = new UserPrivacySettingRuleAllowChatMembers(chats);

            AllowedBadge = GetBadge(_allowedUsers.UserIds, _allowedChatMembers.ChatIds);
        }

        public RelayCommand NeverCommand { get; }
        public async void NeverExecute()
        {
            if (_restrictedUsers == null ||
                _restrictedChatMembers == null)
            {
                return;
            }

            var chats = new List<long>();
            var users = new List<int>();

            foreach (var id in _restrictedUsers.UserIds)
            {
                var chat = await ProtoService.SendAsync(new CreatePrivateChat(id, true)) as Chat;
                if (chat != null)
                {
                    chats.Add(chat.Id);
                }
            }

            foreach (var id in _restrictedChatMembers.ChatIds)
            {
                chats.Add(id);
            }

            var dialog = SharePopup.GetForCurrentView();

            switch (_inputKey)
            {
                case UserPrivacySettingAllowCalls allowCalls:
                case UserPrivacySettingAllowPeerToPeerCalls allowP2PCalls:
                case UserPrivacySettingAllowChatInvites allowChatInvites:
                case UserPrivacySettingShowProfilePhoto showProfilePhoto:
                case UserPrivacySettingShowLinkInForwardedMessages showLinkInForwardedMessages:
                default:
                    dialog.ViewModel.Title = Strings.Resources.NeverAllow;
                    break;
                case UserPrivacySettingShowStatus showStatus:
                    dialog.ViewModel.Title = Strings.Resources.NeverShareWithTitle;
                    break;
            }

            var confirm = await dialog.PickAsync(chats, SearchChatsType.PrivateAndGroups);
            if (confirm != ContentDialogResult.Primary)
            {
                return;
            }

            chats.Clear();
            users.Clear();

            foreach (var chat in dialog.ViewModel.SelectedItems)
            {
                if (chat.Type is ChatTypePrivate privata)
                {
                    users.Add(privata.UserId);
                }
                else
                {
                    chats.Add(chat.Id);
                }
            }

            _restrictedUsers = new UserPrivacySettingRuleRestrictUsers(users);
            _restrictedChatMembers = new UserPrivacySettingRuleRestrictChatMembers(chats);
            RestrictedBadge = GetBadge(_restrictedUsers.UserIds, _restrictedChatMembers.ChatIds);
        }

        public RelayCommand SendCommand { get; }
        public async void SendExecute()
        {
            var response = await SendAsync();
            if (response is Ok)
            {
                NavigationService.GoBack();
            }
            else if (response is Error error)
            {

            }
        }

        public Task<BaseObject> SendAsync()
        {
            var rules = new List<UserPrivacySettingRule>();

            if (_restrictedUsers != null && _restrictedUsers.UserIds.Count > 0 && _selectedItem != PrivacyValue.DisallowAll)
            {
                rules.Add(_restrictedUsers);
            }
            if (_restrictedChatMembers != null && _restrictedChatMembers.ChatIds.Count > 0 && _selectedItem != PrivacyValue.DisallowAll)
            {
                rules.Add(_restrictedChatMembers);
            }

            if (_allowedUsers != null && _allowedUsers.UserIds.Count > 0 && _selectedItem != PrivacyValue.AllowAll)
            {
                rules.Add(_allowedUsers);
            }
            if (_allowedChatMembers != null && _allowedChatMembers.ChatIds.Count > 0 && _selectedItem != PrivacyValue.AllowAll)
            {
                rules.Add(_allowedChatMembers);
            }

            switch (_selectedItem)
            {
                case PrivacyValue.AllowAll:
                    rules.Add(new UserPrivacySettingRuleAllowAll());
                    break;
                case PrivacyValue.AllowContacts:
                    rules.Add(new UserPrivacySettingRuleAllowContacts());
                    break;
                case PrivacyValue.DisallowAll:
                    rules.Add(new UserPrivacySettingRuleRestrictAll());
                    break;
            }

            return ProtoService.SendAsync(new SetUserPrivacySettingRules(_inputKey, new UserPrivacySettingRules(rules)));
        }

        private string GetBadge(IList<int> userIds, IList<long> chatIds)
        {
            var count = userIds.Count;

            foreach (var chatId in chatIds)
            {
                var chat = CacheService.GetChat(chatId);
                if (chat == null)
                {
                    continue;
                }

                if (CacheService.TryGetBasicGroup(chat, out BasicGroup basicGroup))
                {
                    count += basicGroup.MemberCount;
                }
                else if (CacheService.TryGetSupergroup(chat, out Supergroup supergroup))
                {
                    count += supergroup.MemberCount;
                }
            }

            return count > 0 ? Locale.Declension("Users", count) : Strings.Resources.EmpryUsersPlaceholder;
        }
    }

    public enum PrivacyValue
    {
        AllowAll,
        AllowContacts,
        DisallowAll
    }
}
