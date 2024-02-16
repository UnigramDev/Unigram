//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System.Collections.Generic;
using System.Linq;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Converters;
using Telegram.Services;
using Telegram.Td.Api;
using Windows.UI.Xaml;

namespace Telegram.Views.Popups
{
    public sealed partial class DeleteMessagesPopup : ContentPopup
    {
        public DeleteMessagesPopup(IClientService clientService, long savedMessagesTopicId, IList<Message> messages)
        {
            InitializeComponent();

            Title = messages.Count == 1
                ? savedMessagesTopicId == 0 ? Strings.DeleteSingleMessagesTitle : Strings.UnsaveSingleMessagesTitle
                : string.Format(savedMessagesTopicId == 0 ? Strings.DeleteMessagesTitle : Strings.UnsaveMessagesTitle, Locale.Declension(Strings.R.messages, messages.Count));
            PrimaryButtonText = savedMessagesTopicId == 0 ? Strings.Delete : Strings.Remove;
            SecondaryButtonText = Strings.Cancel;

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
            if (sameUser && savedMessagesTopicId == 0 && !first.IsOutgoing && clientService.TryGetSupergroup(chat, out Supergroup supergroup) && !supergroup.IsChannel)
            {
                RevokeCheck.Visibility = Visibility.Collapsed;
                ReportSpamCheck.Visibility = Visibility.Visible;
                DeleteAllCheck.Visibility = Visibility.Visible;

                BanUserCheck.Visibility = supergroup.CanRestrictMembers()
                    ? Visibility.Visible
                    : Visibility.Collapsed;

                var sender = clientService.GetMessageSender(first.SenderId);
                var deleteAllText = string.Empty;

                if (sender is User senderUser)
                {
                    deleteAllText = string.Format(Strings.DeleteAllFrom, senderUser.FullName());
                }
                else if (sender is Chat senderChat)
                {
                    deleteAllText = string.Format(Strings.DeleteAllFrom, senderChat.Title);
                }

                DeleteAllCheck.Content = deleteAllText;
                TextBlockHelper.SetMarkdown(Message, messages.Count == 1
                    ? Strings.AreYouSureDeleteSingleMessage
                    : Strings.AreYouSureDeleteFewMessages);

                // TODO: I don't like the UI moving around when the text appears
                // also, the CheckBox looks misaligned on two lines, one more reason not to have this.
                //var queue = Windows.System.DispatcherQueue.GetForCurrentThread();
                //clientService.Send(new SearchChatMessages(chat.Id, string.Empty, first.SenderId, 0, 0, 1, null, 0), result =>
                //{
                //    if (result is Messages messages)
                //    {
                //        queue.TryEnqueue(() =>
                //        {
                //            if (DeleteAllCheck.IsLoaded)
                //            {
                //                DeleteAllCheck.Content = deleteAllText + string.Format(" ({0})", Locale.Declension("messages", messages.TotalCount));
                //            }
                //        });
                //    }
                //});
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
                        TextBlockHelper.SetMarkdown(Message, chat.Type is ChatTypePrivate && user != null
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
                            RevokeCheck.Content = chat.Type is ChatTypePrivate && user != null
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
