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
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Td.Api;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Telegram.Views.Supergroups.Popups
{
    public sealed partial class SupergroupEventLogFiltersPopup : ContentPopup
    {
        private readonly IClientService _clientService;

        public SupergroupEventLogFiltersPopup(IClientService clientService, INavigationService navigation, long supergroupId, ChatEventLogFilters filters, IList<long> userIds)
        {
            InitializeComponent();

            _clientService = clientService;

            Title = Strings.EventLog;
            PrimaryButtonText = Strings.OK;
            SecondaryButtonText = Strings.Cancel;

            var supergroup = clientService.GetSupergroup(supergroupId);
            if (supergroup.IsChannel)
            {
                MembersAndAdminsCheck.Content = Strings.EventLogFilterSectionSubscribers;
                ChatSettingsCheck.Content = Strings.EventLogFilterSectionChannelSettings;

                MemberRestrictions.Visibility = Visibility.Collapsed;
                MemberJoins.Content = Strings.EventLogFilterNewSubscribers;
                MemberLeaves.Content = Strings.EventLogFilterLeavingSubscribers2;

                InfoChanges.Content = Strings.EventLogFilterChannelInfo;
            }
            else
            {
                MembersAndAdminsCheck.Content = Strings.EventLogFilterSectionMembers;
                ChatSettingsCheck.Content = Strings.EventLogFilterSectionGroupSettings;

                MemberRestrictions.Content = Strings.EventLogFilterNewRestrictions;
                MemberJoins.Content = Strings.EventLogFilterNewMembers;
                MemberLeaves.Content = Strings.EventLogFilterLeavingMembers2;

                InfoChanges.Content = Strings.EventLogFilterGroupInfo;
            }

            MemberPromotions.IsChecked = filters.MemberPromotions;
            MemberRestrictions.IsChecked = filters.MemberRestrictions;
            MemberJoins.IsChecked = filters.MemberJoins;
            MemberLeaves.IsChecked = filters.MemberLeaves;

            InfoChanges.IsChecked = filters.InfoChanges;
            InviteLinkChanges.IsChecked = filters.InviteLinkChanges;
            VideoChatChanges.IsChecked = filters.VideoChatChanges;

            MessageDeletions.IsChecked = filters.MessageDeletions;
            MessageEdits.IsChecked = filters.MessageEdits;
            MessagePins.IsChecked = filters.MessagePins;

            Populate(clientService, supergroupId, userIds);

            MemberPromotions.Checked += MembersAndAdmins_Checked;
            MemberPromotions.Unchecked += MembersAndAdmins_Checked;
            MemberRestrictions.Checked += MembersAndAdmins_Checked;
            MemberRestrictions.Unchecked += MembersAndAdmins_Checked;
            MemberJoins.Checked += MembersAndAdmins_Checked;
            MemberJoins.Unchecked += MembersAndAdmins_Checked;
            MemberLeaves.Checked += MembersAndAdmins_Checked;
            MemberLeaves.Unchecked += MembersAndAdmins_Checked;

            InfoChanges.Checked += ChatSettings_Checked;
            InfoChanges.Unchecked += ChatSettings_Checked;
            InviteLinkChanges.Checked += ChatSettings_Checked;
            InviteLinkChanges.Unchecked += ChatSettings_Checked;
            VideoChatChanges.Checked += ChatSettings_Checked;
            VideoChatChanges.Unchecked += ChatSettings_Checked;

            MessageDeletions.Checked += Messages_Checked;
            MessageDeletions.Unchecked += Messages_Checked;
            MessageEdits.Checked += Messages_Checked;
            MessageEdits.Unchecked += Messages_Checked;
            MessagePins.Checked += Messages_Checked;
            MessagePins.Unchecked += Messages_Checked;

            MembersAndAdminsCount.Text = CountSelection(MembersAndAdminsRoot, MembersAndAdminsCheck, MembersAndAdmins_Checked);
            ChatSettingsCount.Text = CountSelection(ChatSettingsRoot, ChatSettingsCheck, ChatSettings_Checked);
            MessagesCount.Text = CountSelection(MessagesRoot, MessagesCheck, Messages_Checked);
        }

        private async void Populate(IClientService clientService, long supergroupId, IList<long> selectedIds)
        {
            var response = await clientService.SendAsync(new GetSupergroupMembers(supergroupId, new SupergroupMembersFilterAdministrators(), 0, 200));
            if (response is not ChatMembers members)
            {
                return;
            }

            var userIds = members.Members
                .Select(x => x.MemberId)
                .OfType<MessageSenderUser>()
                .Select(x => x.UserId);

            foreach (var sender in clientService.GetUsers(userIds))
            {
                var photo = new ProfilePicture
                {
                    Width = 28,
                    Height = 28,
                    Margin = new Thickness(0, -4, 8, 0),
                    IsEnabled = false
                };

                photo.SetUser(_clientService, sender, 28);

                var title = new TextBlock
                {
                    Text = sender.FullName()
                };

                Grid.SetColumn(title, 1);

                var content = new Grid();
                content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });
                content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                content.Children.Add(photo);
                content.Children.Add(title);

                var selector = new CheckBox
                {
                    Tag = sender.Id,
                    Content = content,
                    IsChecked = selectedIds.Contains(sender.Id) || selectedIds.Empty()
                };

                selector.Checked += Admins_Checked;
                selector.Unchecked += Admins_Checked;

                AdminsRoot.Children.Add(selector);
            }

            CountSelection(AdminsRoot, AdminsCheck, Admins_Checked);
        }

        private void MembersAndAdmins_Checked(object sender, RoutedEventArgs e)
        {
            if ((CheckBox)sender == MembersAndAdminsCheck)
            {
                MembersAndAdminsCount.Text = ToggleSelection(MembersAndAdminsRoot, MembersAndAdminsCheck, MembersAndAdmins_Checked);
            }
            else
            {
                MembersAndAdminsCount.Text = CountSelection(MembersAndAdminsRoot, MembersAndAdminsCheck, MembersAndAdmins_Checked);
            }
        }

        private void ChatSettings_Checked(object sender, RoutedEventArgs e)
        {
            if ((CheckBox)sender == ChatSettingsCheck)
            {
                ChatSettingsCount.Text = ToggleSelection(ChatSettingsRoot, ChatSettingsCheck, ChatSettings_Checked);
            }
            else
            {
                ChatSettingsCount.Text = CountSelection(ChatSettingsRoot, ChatSettingsCheck, ChatSettings_Checked);
            }
        }

        private void Messages_Checked(object sender, RoutedEventArgs e)
        {
            if ((CheckBox)sender == MessagesCheck)
            {
                MessagesCount.Text = ToggleSelection(MessagesRoot, MessagesCheck, Messages_Checked);
            }
            else
            {
                MessagesCount.Text = CountSelection(MessagesRoot, MessagesCheck, Messages_Checked);
            }
        }

        private void Admins_Checked(object sender, RoutedEventArgs e)
        {
            if ((CheckBox)sender == AdminsCheck)
            {
                ToggleSelection(AdminsRoot, AdminsCheck, Admins_Checked);
            }
            else
            {
                CountSelection(AdminsRoot, AdminsCheck, Admins_Checked);
            }
        }

        private string CountSelection(StackPanel panel, CheckBox parent, RoutedEventHandler handler)
        {
            var count = 0;
            var total = 0;

            foreach (CheckBox child in panel.Children)
            {
                if (child.Visibility == Visibility.Collapsed)
                {
                    continue;
                }
                else if (child.IsChecked == true)
                {
                    count++;
                }

                total++;
            }

            parent.Checked -= handler;
            parent.Unchecked -= handler;

            parent.IsChecked = total == count
                ? true
                : total == 0
                ? false
                : null;

            parent.Checked += handler;
            parent.Unchecked += handler;

            return string.Format("{0}/{1}", count, total);
        }

        private string ToggleSelection(StackPanel panel, CheckBox parent, RoutedEventHandler handler)
        {
            var total = 0;
            var check = parent.IsChecked == true;

            foreach (CheckBox child in panel.Children)
            {
                if (child.Visibility == Visibility.Collapsed)
                {
                    continue;
                }

                total++;

                child.Checked -= handler;
                child.Unchecked -= handler;

                child.IsChecked = check;

                child.Checked += handler;
                child.Unchecked += handler;
            }

            return string.Format("{0}/{1}", check ? total : 0, total);
        }

        public ChatEventLogFilters Filters { get; private set; }

        public IList<long> UserIds { get; private set; }

        private void ContentPopup_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            Filters = new ChatEventLogFilters
            {
                MemberRestrictions = MemberRestrictions.IsChecked == true,
                MemberPromotions = MemberPromotions.IsChecked == true,
                MemberJoins = MemberJoins.IsChecked == true,
                MemberInvites = MemberJoins.IsChecked == true,
                InfoChanges = InfoChanges.IsChecked == true,
                InviteLinkChanges = InviteLinkChanges.IsChecked == true,
                SettingChanges = InfoChanges.IsChecked == true,
                MessageDeletions = MessageDeletions.IsChecked == true,
                MessageEdits = MessageEdits.IsChecked == true,
                MessagePins = MessagePins.IsChecked == true,
                MemberLeaves = MemberLeaves.IsChecked == true,
                VideoChatChanges = VideoChatChanges.IsChecked == true
            };

            if (Filters.MemberRestrictions && Filters.MemberPromotions && Filters.MemberJoins
                && Filters.MemberInvites && Filters.InfoChanges && Filters.InviteLinkChanges
                && Filters.SettingChanges && Filters.MessageDeletions && Filters.MessageEdits
                && Filters.MessagePins && Filters.MemberLeaves && Filters.VideoChatChanges)
            {
                Filters.ForumChanges = true;
                Filters.SubscriptionExtensions = true;
            }

            var userIds = new List<long>();
            var total = 0;

            foreach (CheckBox child in AdminsRoot.Children)
            {
                if (child.IsChecked == true && child.Tag is long userId)
                {
                    userIds.Add(userId);
                }

                total++;
            }

            var areAllAdministratorsSelected = userIds.Count == total;
            UserIds = areAllAdministratorsSelected ? Array.Empty<long>() : userIds;
        }
    }
}
