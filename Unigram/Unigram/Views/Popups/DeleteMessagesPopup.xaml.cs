using System.Collections.Generic;
using System.Linq;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Services;
using Windows.UI.Xaml;

namespace Unigram.Views.Popups
{
    public sealed partial class DeleteMessagesPopup : ContentPopup
    {
        public DeleteMessagesPopup(IClientService clientService, IList<Message> messages)
        {
            InitializeComponent();

            Title = messages.Count == 1 ? Strings.Resources.DeleteSingleMessagesTitle : string.Format(Strings.Resources.DeleteMessagesTitle, Locale.Declension("messages", messages.Count));
            PrimaryButtonText = Strings.Resources.OK;
            SecondaryButtonText = Strings.Resources.Cancel;

            var first = messages.FirstOrDefault();
            if (first == null)
            {
                return;
            }

            var chat = clientService.GetChat(first.ChatId);
            if (chat == null)
            {
                return;
            }

            var user = clientService.GetUser(chat);

            var sameUser = messages.All(x => x.SenderId.AreTheSame(first.SenderId));
            if (sameUser && !first.IsOutgoing && clientService.TryGetSupergroup(chat, out Supergroup supergroup) && !supergroup.IsChannel)
            {
                RevokeCheck.Visibility = Visibility.Collapsed;
                ReportSpamCheck.Visibility = Visibility.Visible;
                DeleteAllCheck.Visibility = Visibility.Visible;

                BanUserCheck.Visibility = supergroup.CanRestrictMembers()
                    ? Visibility.Visible
                    : Visibility.Collapsed;

                var sender = clientService.GetMessageSender(first.SenderId);
                if (sender is User senderUser)
                {
                    DeleteAllCheck.Content = string.Format(Strings.Resources.DeleteAllFrom, senderUser.GetFullName());
                }
                else if (sender is Chat senderChat)
                {
                    DeleteAllCheck.Content = string.Format(Strings.Resources.DeleteAllFrom, senderChat.Title);
                }

                TextBlockHelper.SetMarkdown(Message, messages.Count == 1
                    ? Strings.Resources.AreYouSureDeleteSingleMessage
                    : Strings.Resources.AreYouSureDeleteFewMessages);
            }
            else
            {
                RevokeCheck.Visibility = Visibility.Collapsed;
                BanUserCheck.Visibility = Visibility.Collapsed;
                ReportSpamCheck.Visibility = Visibility.Collapsed;
                DeleteAllCheck.Visibility = Visibility.Collapsed;

                var canBeDeletedForAllUsers = messages.All(x => x.CanBeDeletedForAllUsers);
                var canBeDeletedOnlyForSelf = messages.All(x => x.CanBeDeletedOnlyForSelf);
                var anyCanBeDeletedForAllUsers = messages.Any(x => x.IsOutgoing && x.CanBeDeletedForAllUsers);

                if (chat.Type is ChatTypePrivate or ChatTypeBasicGroup)
                {
                    if (anyCanBeDeletedForAllUsers && !canBeDeletedForAllUsers)
                    {
                        TextBlockHelper.SetMarkdown(Message, chat.Type is ChatTypePrivate && user != null
                            ? string.Format(Strings.Resources.DeleteMessagesText, Locale.Declension("messages", messages.Count), user.FirstName)
                            : string.Format(Strings.Resources.DeleteMessagesTextGroup, Locale.Declension("messages", messages.Count)));

                        RevokeCheck.IsChecked = true;
                        RevokeCheck.Visibility = Visibility.Visible;
                        RevokeCheck.Content = Strings.Resources.DeleteMessagesOption;
                    }
                    else
                    {
                        TextBlockHelper.SetMarkdown(Message, messages.Count == 1
                            ? Strings.Resources.AreYouSureDeleteSingleMessage
                            : Strings.Resources.AreYouSureDeleteFewMessages);

                        if (canBeDeletedForAllUsers)
                        {
                            RevokeCheck.IsChecked = true;
                            RevokeCheck.Visibility = Visibility.Visible;
                            RevokeCheck.Content = chat.Type is ChatTypePrivate && user != null
                                ? string.Format(Strings.Resources.DeleteMessagesOptionAlso, user.FirstName)
                                : Strings.Resources.DeleteForAll;
                        }
                    }
                }
                else if (chat.Type is ChatTypeSupergroup super && !super.IsChannel)
                {
                    TextBlockHelper.SetMarkdown(Message, messages.Count == 1
                        ? Strings.Resources.AreYouSureDeleteSingleMessageMega
                        : Strings.Resources.AreYouSureDeleteFewMessagesMega);
                }
                else
                {
                    TextBlockHelper.SetMarkdown(Message, messages.Count == 1
                        ? Strings.Resources.AreYouSureDeleteSingleMessage
                        : Strings.Resources.AreYouSureDeleteFewMessages);
                }
            }
        }

        public bool Revoke
        {
            get => RevokeCheck.IsChecked ?? false;
            set => RevokeCheck.IsChecked = value;
        }

        public bool BanUser
        {
            get => BanUserCheck.IsChecked ?? false;
            set => BanUserCheck.IsChecked = value;
        }

        public bool ReportSpam
        {
            get => ReportSpamCheck.IsChecked ?? false;
            set => ReportSpamCheck.IsChecked = value;
        }

        public bool DeleteAll
        {
            get => DeleteAllCheck.IsChecked ?? false;
            set => DeleteAllCheck.IsChecked = value;
        }
    }
}
