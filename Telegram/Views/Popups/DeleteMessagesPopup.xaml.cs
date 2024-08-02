//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Collections.Generic;
using System.Linq;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Converters;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.ViewModels.Supergroups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Telegram.Views.Popups
{
    public sealed partial class DeleteMessagesPopup : ContentPopup
    {
        public SupergroupEditRestrictedViewModel ViewModel => DataContext as SupergroupEditRestrictedViewModel;

        private readonly IClientService _clientService;
        private readonly List<MessageSender> _senders;

        private readonly int _messagesCount;
        private int _deleteAllCount;

        private IList<MessageSender> _reportSpam;
        private IList<MessageSender> _deleteAll;
        private IList<MessageSender> _banUser;

        public DeleteMessagesPopup(IClientService clientService, long savedMessagesTopicId, Chat chat, IList<Message> messages, IDictionary<MessageId, MessageProperties> properties)
        {
            InitializeComponent();

            DataContext = TypeResolver.Current.Resolve<SupergroupEditRestrictedViewModel>(clientService.SessionId);

            Title = messages.Count == 1
                ? savedMessagesTopicId == 0 ? Strings.DeleteSingleMessagesTitle : Strings.UnsaveSingleMessagesTitle
                : string.Format(savedMessagesTopicId == 0 ? Strings.DeleteMessagesTitle : Strings.UnsaveMessagesTitle, Locale.Declension(Strings.R.messages, messages.Count));
            PrimaryButtonText = savedMessagesTopicId == 0 ? Strings.Delete : Strings.Remove;
            SecondaryButtonText = Strings.Cancel;

            var supergroup = clientService.GetSupergroup(chat);
            var senders = messages
                .Select(x => x.SenderId)
                .Distinct(new MessageSenderEqualityComparer())
                .Where(x => !x.IsUser(clientService.Options.MyId))
                .ToList();

            if (senders.Count > 1 && supergroup?.IsChannel is false)
            {
                ReportSpamCheck.Content = Strings.DeleteReportSpam;
                DeleteAllCheck.Content = Strings.DeleteAllFromUsers;
                BanUserCheck.Content = Strings.DeleteBanUsers;

                ReportSpamCount.Text =
                    DeleteAllCount.Text =
                    BanUserCount.Text = senders.Count.ToString();

                PermissionsToggle.Content = Strings.DeleteToggleRestrictUsers;

                BanUserRoot.Visibility = supergroup.CanRestrictMembers()
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }
            else if (senders.Count > 0 && supergroup?.IsChannel is false)
            {
                ReportSpamCheck.Content = Strings.DeleteReportSpam;
                DeleteAllCheck.Content = string.Format(Strings.DeleteAllFrom, clientService.GetTitle(senders[0]));
                BanUserCheck.Content = string.Format(Strings.DeleteBan, clientService.GetTitle(senders[0]));

                ReportSpamCount.Visibility =
                    DeleteAllCount.Visibility =
                    BanUserCount.Visibility = Visibility.Collapsed;

                ReportSpamIcon.Visibility =
                    DeleteAllIcon.Visibility =
                    BanUserIcon.Visibility = Visibility.Collapsed;

                ReportSpamExpander.Margin =
                    DeleteAllExpander.Margin =
                    BanUserExpander.Margin = new Thickness(0, 0, -72, 0);

                PermissionsToggle.Content = Strings.DeleteToggleRestrictUser;

                BanUserRoot.Visibility = supergroup.CanRestrictMembers()
                    ? Visibility.Visible
                    : Visibility.Collapsed;

                _messagesCount = messages.Count;
                clientService.Send(new SearchChatMessages(chat.Id, string.Empty, senders[0], 0, 0, 1, null, 0, 0), result =>
                {
                    if (result is FoundChatMessages found)
                    {
                        _deleteAllCount = found.TotalCount;
                    }
                });
            }
            else
            {
                AdditionalRoot.Visibility = Visibility.Collapsed;
                BasicRoot.Visibility = Visibility.Visible;

                RevokeCheck.Visibility = Visibility.Collapsed;

                var mapped = messages.ToDictionary(x => new MessageId(x));

                var canBeDeletedForAllUsers = properties.Values.All(x => x.CanBeDeletedForAllUsers);
                var canBeDeletedOnlyForSelf = properties.Values.All(x => x.CanBeDeletedOnlyForSelf);
                var anyCanBeDeletedForAllUsers = properties.Any(x => mapped[x.Key].IsOutgoing && x.Value.CanBeDeletedForAllUsers);

                if (savedMessagesTopicId != 0)
                {
                    TextBlockHelper.SetMarkdown(Message, messages.Count == 1
                        ? Strings.AreYouSureUnsaveSingleMessage
                        : Strings.AreYouSureUnsaveFewMessages);
                }
                else if (chat.Type is ChatTypePrivate or ChatTypeBasicGroup)
                {
                    if (anyCanBeDeletedForAllUsers && !canBeDeletedForAllUsers)
                    {
                        TextBlockHelper.SetMarkdown(Message, chat.Type is ChatTypePrivate && clientService.TryGetUser(chat, out User user)
                            ? string.Format(Strings.DeleteMessagesText, Locale.Declension(Strings.R.messages, messages.Count), user.FirstName)
                            : string.Format(Strings.DeleteMessagesTextGroup, Locale.Declension(Strings.R.messages, messages.Count)));

                        RevokeCheck.IsChecked = true;
                        RevokeCheck.Visibility = Visibility.Visible;
                        RevokeCheck.Content = Strings.DeleteMessagesOption;
                    }
                    else
                    {
                        TextBlockHelper.SetMarkdown(Message, messages.Count == 1
                            ? Strings.AreYouSureDeleteSingleMessage
                            : Strings.AreYouSureDeleteFewMessages);

                        if (canBeDeletedForAllUsers)
                        {
                            RevokeCheck.IsChecked = true;
                            RevokeCheck.Visibility = Visibility.Visible;
                            RevokeCheck.Content = chat.Type is ChatTypePrivate && clientService.TryGetUser(chat, out User user)
                                ? string.Format(Strings.DeleteMessagesOptionAlso, user.FirstName)
                                : Strings.DeleteForAll;
                        }
                    }
                }
                else if (chat.Type is ChatTypeSupergroup super && !super.IsChannel)
                {
                    TextBlockHelper.SetMarkdown(Message, messages.Count == 1
                        ? Strings.AreYouSureDeleteSingleMessageMega
                        : Strings.AreYouSureDeleteFewMessagesMega);
                }
                else
                {
                    if (messages.Count == 1 && messages[0].Content is MessagePremiumGiveaway giveaway)
                    {
                        Title = Strings.BoostingGiveawayDeleteMsgTitle;
                        TextBlockHelper.SetMarkdown(Message, string.Format(Strings.BoostingGiveawayDeleteMsgText, Formatter.DateAt(giveaway.Parameters.WinnersSelectionDate)));
                    }
                    else
                    {
                        TextBlockHelper.SetMarkdown(Message, messages.Count == 1
                            ? Strings.AreYouSureDeleteSingleMessage
                            : Strings.AreYouSureDeleteFewMessages);
                    }
                }
            }

            _clientService = clientService;
            _senders = senders;

            _reportSpam = Array.Empty<MessageSender>();
            _deleteAll = Array.Empty<MessageSender>();
            _banUser = Array.Empty<MessageSender>();

            ViewModel.CanSendBasicMessages = chat.Permissions.CanSendBasicMessages;
            ViewModel.CanSendPhotos = chat.Permissions.CanSendPhotos;
            ViewModel.CanSendVideos = chat.Permissions.CanSendVideos;
            ViewModel.CanSendOtherMessages = chat.Permissions.CanSendOtherMessages;
            ViewModel.CanSendAudios = chat.Permissions.CanSendAudios;
            ViewModel.CanSendDocuments = chat.Permissions.CanSendDocuments;
            ViewModel.CanSendVoiceNotes = chat.Permissions.CanSendVoiceNotes;
            ViewModel.CanSendVideoNotes = chat.Permissions.CanSendVideoNotes;
            ViewModel.CanSendPolls = chat.Permissions.CanSendPolls;
            ViewModel.CanAddLinkPreviews = chat.Permissions.CanAddLinkPreviews;
            ViewModel.CanInviteUsers = chat.Permissions.CanInviteUsers;
            ViewModel.CanPinMessages = chat.Permissions.CanPinMessages;
            ViewModel.CanChangeInfo = chat.Permissions.CanChangeInfo;

            CanSendBasicMessages.IsEnabled = chat.Permissions.CanSendBasicMessages;
            CanSendPhotos.IsEnabled = chat.Permissions.CanSendPhotos;
            CanSendVideos.IsEnabled = chat.Permissions.CanSendVideos;
            CanSendOtherMessages.IsEnabled = chat.Permissions.CanSendOtherMessages;
            CanSendAudios.IsEnabled = chat.Permissions.CanSendAudios;
            CanSendDocuments.IsEnabled = chat.Permissions.CanSendDocuments;
            CanSendVoiceNotes.IsEnabled = chat.Permissions.CanSendVoiceNotes;
            CanSendVideoNotes.IsEnabled = chat.Permissions.CanSendVideoNotes;
            CanSendPolls.IsEnabled = chat.Permissions.CanSendPolls;
            CanAddLinkPreviews.IsEnabled = chat.Permissions.CanAddLinkPreviews;
            CanInviteUsers.IsEnabled = chat.Permissions.CanInviteUsers;
            CanPinMessages.IsEnabled = chat.Permissions.CanPinMessages;
            CanChangeInfo.IsEnabled = chat.Permissions.CanChangeInfo;

            UpdatePermissions();
        }

        #region Binding

        private string ConvertCanSendCount(int count)
        {
            return $"{count}/9";
        }

        #endregion

        public bool Revoke
        {
            get => RevokeCheck.IsChecked ?? false;
            set => RevokeCheck.IsChecked = value;
        }

        public IList<MessageSender> BanUser => _banUser;

        public IList<MessageSender> ReportSpam => _reportSpam;

        public IList<MessageSender> DeleteAll => _deleteAll;

        public ChatMemberStatus SelectedStatus => PermissionsPanel.Visibility == Visibility.Collapsed
                    ? new ChatMemberStatusBanned()
                    : ViewModel.SelectedStatus;

        private void ReportSpam_Expanded(object sender, EventArgs e)
        {
            Populate(ReportSpamRoot, ReportSpam_Checked);
        }

        private void DeleteAll_Expanded(object sender, EventArgs e)
        {
            Populate(DeleteAllRoot, DeleteAll_Checked);
        }

        private void BanUser_Expanded(object sender, EventArgs e)
        {
            Populate(BanUserRoot, BanUser_Checked);
        }

        private void Populate(StackPanel root, RoutedEventHandler handler)
        {
            if (root.Children.Count > 0)
            {
                return;
            }

            foreach (var sender in _senders)
            {
                var photo = new ProfilePicture
                {
                    Width = 28,
                    Height = 28,
                    Margin = new Thickness(0, -4, 8, 0),
                    IsEnabled = false
                };

                photo.SetMessageSender(_clientService, sender, 28);

                var title = new TextBlock
                {
                    Text = _clientService.GetTitle(sender)
                };

                Grid.SetColumn(title, 1);

                var content = new Grid();
                content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });
                content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                content.Children.Add(photo);
                content.Children.Add(title);

                var selector = new CheckBox
                {
                    Tag = sender,
                    Content = content,
                    IsChecked = false
                };

                selector.Checked += handler;
                selector.Unchecked += handler;

                root.Children.Add(selector);
            }
        }

        private void ReportSpam_Checked(object sender, RoutedEventArgs e)
        {
            if (sender == ReportSpamCheck)
            {
                _reportSpam = Toggle(ReportSpamCheck.IsChecked == true, ReportSpamRoot, ReportSpamCount, ReportSpam_Checked);
            }
            else
            {
                _reportSpam = UpdateSelection(ReportSpamRoot, ReportSpamCount, ReportSpamCheck, ReportSpam_Checked);
            }
        }

        private void DeleteAll_Checked(object sender, RoutedEventArgs e)
        {
            if (sender == DeleteAllCheck)
            {
                _deleteAll = Toggle(DeleteAllCheck.IsChecked == true, DeleteAllRoot, DeleteAllCount, DeleteAll_Checked);
            }
            else
            {
                _deleteAll = UpdateSelection(DeleteAllRoot, DeleteAllCount, DeleteAllCheck, DeleteAll_Checked);
            }

            if (_messagesCount > 0)
            {
                var count = DeleteAllCheck.IsChecked == true ? _deleteAllCount : _messagesCount;
                Title = count == 1
                    ? Strings.DeleteSingleMessagesTitle
                    : string.Format(Strings.DeleteMessagesTitle, Locale.Declension(Strings.R.messages, count));
            }
        }

        private void BanUser_Checked(object sender, RoutedEventArgs e)
        {
            if (sender == BanUserCheck)
            {
                _banUser = Toggle(BanUserCheck.IsChecked == true, BanUserRoot, BanUserCount, BanUser_Checked);
            }
            else
            {
                _banUser = UpdateSelection(BanUserRoot, BanUserCount, BanUserCheck, BanUser_Checked);
            }

            UpdatePermissions();
        }

        private IList<MessageSender> Toggle(bool check, StackPanel root, AnimatedTextBlock count, RoutedEventHandler handler)
        {
            foreach (var child in root.Children)
            {
                if (child is CheckBox selector)
                {
                    selector.Checked -= handler;
                    selector.Unchecked -= handler;

                    selector.IsChecked = check;

                    selector.Checked += handler;
                    selector.Unchecked += handler;
                }
            }

            count.Text = _senders.Count.ToString();

            return check ? _senders.ToList() : Array.Empty<MessageSender>();
        }

        private IList<MessageSender> UpdateSelection(StackPanel root, AnimatedTextBlock count, CheckBox checkBox, RoutedEventHandler handler)
        {
            var totalCount = 0;
            var senders = new List<MessageSender>();

            foreach (var child in root.Children)
            {
                if (child is CheckBox selector && selector.Tag is MessageSender sender)
                {
                    if (selector.IsChecked == true)
                    {
                        totalCount++;
                        senders.Add(sender);
                    }
                }
            }

            count.Text = totalCount == 0
                ? _senders.Count.ToString()
                : totalCount.ToString();

            checkBox.Checked -= handler;
            checkBox.Unchecked -= handler;

            checkBox.IsChecked = totalCount == _senders.Count
                ? true
                : totalCount == 0
                ? false
                : null;

            checkBox.Checked += handler;
            checkBox.Unchecked += handler;

            return senders;
        }

        private void PermissionsToggle_Click(object sender, RoutedEventArgs e)
        {
            PermissionsPanel.Visibility = PermissionsPanel.Visibility == Visibility.Visible
                ? Visibility.Collapsed
                : Visibility.Visible;

            UpdatePermissions();
        }

        private void UpdatePermissions()
        {
            PermissionsHeader.Text = _senders.Count == 1
                ? Strings.UserRestrictionsCanDo
                : Locale.Declension(Strings.R.UserRestrictionsCanDoUsers, _banUser.Count);

            PermissionsToggle.Content = PermissionsPanel.Visibility == Visibility.Visible
                ? _senders.Count == 1 ? Strings.DeleteToggleBanUser : Strings.DeleteToggleBanUsers
                : _senders.Count == 1 ? Strings.DeleteToggleRestrictUser : Strings.DeleteToggleRestrictUsers;

            BanUserCheck.Content = PermissionsPanel.Visibility == Visibility.Visible
                ? _senders.Count == 1 ? string.Format(Strings.DeleteRestrict, _clientService.GetTitle(_senders[0])) : Strings.DeleteRestrictUsers
                : _senders.Count == 1 ? string.Format(Strings.DeleteBan, _clientService.GetTitle(_senders[0])) : Strings.DeleteBanUsers;
        }
    }
}
