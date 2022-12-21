using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Controls.Cells;
using Unigram.Services;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Unigram.Views.Popups
{
    public sealed partial class SupergroupEventLogFiltersPopup : ContentPopup
    {
        private IClientService _clientService;

        public ChatEventLogFilters Filters { get; private set; }
        public IList<long> UserIds { get; private set; }

        public SupergroupEventLogFiltersPopup()
        {
            InitializeComponent();

            Title = Strings.Resources.Settings;
            PrimaryButtonText = Strings.Resources.OK;
            SecondaryButtonText = Strings.Resources.Cancel;
        }

        public Task<ContentDialogResult> ShowAsync(IClientService clientService, long supergroupId, ChatEventLogFilters filters, IList<long> userIds)
        {
            _clientService = clientService;

            filters ??= new ChatEventLogFilters(true, true, true, true, true, true, true, true, true, true, true, true, true);

            MemberRestrictions.IsChecked = filters.MemberRestrictions;
            MemberPromotions.IsChecked = filters.MemberPromotions;
            MemberJoins.IsChecked = filters.MemberJoins || filters.MemberInvites;
            InfoChanges.IsChecked = filters.InfoChanges || filters.SettingChanges;
            InviteLinkChanges.IsChecked = filters.InviteLinkChanges;
            MessageDeletions.IsChecked = filters.MessageDeletions;
            MessageEdits.IsChecked = filters.MessageEdits;
            MessagePins.IsChecked = filters.MessagePins;
            MemberLeaves.IsChecked = filters.MemberLeaves;
            VideoChatChanges.IsChecked = filters.VideoChatChanges;
            ForumChanges.IsChecked = filters.ForumChanges;

            Event_Toggled(null, null);

            clientService.Send(new GetSupergroupMembers(supergroupId, new SupergroupMembersFilterAdministrators(), 0, 200), response =>
            {
                if (response is ChatMembers members)
                {
                    this.BeginOnUIThread(() =>
                    {
                        List.Items.Clear();

                        if (clientService.Options.AntiSpamBotUserId != 0)
                        {
                            var antiSpamSender = new MessageSenderUser(clientService.Options.AntiSpamBotUserId);
                            var antiSpam = new ChatMember(antiSpamSender, 0, 0, new ChatMemberStatusAdministrator());

                            List.Items.Add(antiSpam);

                            if (userIds.Contains(antiSpamSender.UserId))
                            {
                                List.SelectedItems.Add(antiSpam);
                            }
                        }

                        foreach (var item in members.Members)
                        {
                            if (item.MemberId is MessageSenderUser senderUser)
                            {
                                List.Items.Add(item);

                                if (userIds.Contains(senderUser.UserId))
                                {
                                    List.SelectedItems.Add(item);
                                }
                            }
                        }

                        if (List.SelectedItems.Count > 0)
                        {
                            FieldAllAdmins.IsChecked = null;
                        }
                        else
                        {
                            FieldAllAdmins.IsChecked = true;
                            List.SelectAll();
                        }
                    });
                }
            });

            //List.ItemsSource = new ChatMemberCollection(clientService, supergroupId, new SupergroupMembersFilterAdministrators());
            return this.ShowQueuedAsync();
        }

        private void Event_Toggled(object sender, RoutedEventArgs e)
        {
            if (EventFilters == null)
            {
                return;
            }

            var all = EventFilters.Children.OfType<CheckBox>().All(x => x.IsChecked == true);
            var none = EventFilters.Children.OfType<CheckBox>().All(x => x.IsChecked == false);

            FieldAllEvents.IsChecked = all ? true : none ? new bool?(false) : null;
            IsPrimaryButtonEnabled = none ? false : true;
        }

        private void AllEvents_Toggled(object sender, RoutedEventArgs e)
        {
            if (EventFilters == null)
            {
                return;
            }

            foreach (CheckBox check in EventFilters.Children)
            {
                check.IsChecked = FieldAllEvents.IsChecked == true;
            }

            IsPrimaryButtonEnabled = FieldAllEvents.IsChecked == true;
        }

        private void Admin_Toggled(object sender, SelectionChangedEventArgs e)
        {
            if (EventFilters == null)
            {
                return;
            }

            var all = List.Items.All(x => List.SelectedItems.Contains(x));
            var none = List.Items.All(x => !List.SelectedItems.Contains(x));

            FieldAllAdmins.IsChecked = all ? true : none ? new bool?(false) : null;
        }

        private void AllAdmins_Toggled(object sender, RoutedEventArgs e)
        {
            if (EventFilters == null)
            {
                return;
            }

            if (FieldAllAdmins.IsChecked == true)
            {
                List.SelectAll();
            }
            else
            {
                List.SelectedItems.Clear();
            }
        }

        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
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
                VideoChatChanges = VideoChatChanges.IsChecked == true,
                ForumChanges = ForumChanges.IsChecked == true
            };

            var areAllAdministratorsSelected = List.Items.All(x => List.SelectedItems.Contains(x));
            UserIds = areAllAdministratorsSelected ? new long[0] : List.SelectedItems.OfType<ChatMember>().Select(x => x.MemberId).OfType<MessageSenderUser>().Select(x => x.UserId).ToArray();
        }

        private void ContentDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
        }

        #region Recycle

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }
            else if (args.ItemContainer.ContentTemplateRoot is UserCell content)
            {
                content.UpdateSupergroupAdminFilter(_clientService, args, OnContainerContentChanging);
            }
        }

        #endregion
    }
}
