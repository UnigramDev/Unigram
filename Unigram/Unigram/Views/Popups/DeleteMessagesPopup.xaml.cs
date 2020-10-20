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
        public DeleteMessagesPopup(ICacheService cacheService, IList<Message> messages)
        {
            this.InitializeComponent();

            Title = messages.Count == 1 ? Strings.Resources.DeleteSingleMessagesTitle : string.Format(Strings.Resources.DeleteMessagesTitle, Locale.Declension("messages", messages.Count));
            PrimaryButtonText = Strings.Resources.OK;
            SecondaryButtonText = Strings.Resources.Cancel;

            var first = messages.FirstOrDefault();
            if (first == null)
            {
                return;
            }

            var chat = cacheService.GetChat(first.ChatId);
            if (chat == null)
            {
                return;
            }

            var user = cacheService.GetUser(chat);

            var firstSender = first.Sender as MessageSenderUser;

            var sameUser = firstSender != null && messages.All(x => x.Sender is MessageSenderUser senderUser && senderUser.UserId == firstSender.UserId);
            if (sameUser && !first.IsOutgoing && chat.Type is ChatTypeSupergroup supergroup && !supergroup.IsChannel)
            {
                var sender = cacheService.GetUser(firstSender.UserId);

                RevokeCheck.Visibility = Visibility.Collapsed;
                BanUserCheck.Visibility = Visibility.Visible;
                ReportSpamCheck.Visibility = Visibility.Visible;
                DeleteAllCheck.Visibility = Visibility.Visible;
                DeleteAllCheck.Content = string.Format(Strings.Resources.DeleteAllFrom, sender.GetFullName());

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

                if (chat.Type is ChatTypePrivate || chat.Type is ChatTypeBasicGroup)
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
            get { return RevokeCheck.IsChecked ?? false; }
            set { RevokeCheck.IsChecked = value; }
        }

        public bool BanUser
        {
            get { return BanUserCheck.IsChecked ?? false; }
            set { BanUserCheck.IsChecked = value; }
        }

        public bool ReportSpam
        {
            get { return ReportSpamCheck.IsChecked ?? false; }
            set { ReportSpamCheck.IsChecked = value; }
        }

        public bool DeleteAll
        {
            get { return DeleteAllCheck.IsChecked ?? false; }
            set { DeleteAllCheck.IsChecked = value; }
        }
    }
}
