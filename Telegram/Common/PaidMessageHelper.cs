//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System.Threading.Tasks;
using Telegram.Controls;
using Telegram.Controls.Media;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Td.Api;
using Windows.UI.Xaml.Controls;

namespace Telegram.Common
{
    public static class PaidMessageHelper
    {
        /// <summary>
        /// What each message sent to <paramref name="chat"/> costs. Unlike
        /// <see cref="IClientService.PaidMessageStarCount"/>, which reads the price a chat is
        /// set to, an administrator of a supergroup that charges sends for nothing.
        /// </summary>
        public static long OutgoingStarCount(IClientService clientService, Chat chat)
        {
            if (clientService.TryGetUserFull(chat, out UserFullInfo userFullInfo))
            {
                return userFullInfo.OutgoingPaidMessageStarCount;
            }
            else if (clientService.TryGetSupergroup(chat, out Supergroup supergroup))
            {
                if (supergroup.Status is not ChatMemberStatusAdministrator and not ChatMemberStatusCreator && !supergroup.IsAdministeredDirectMessagesGroup)
                {
                    return supergroup.PaidMessageStarCount;
                }
            }

            return 0;
        }

        /// <summary>
        /// Asks to pay for <paramref name="messageCount"/> messages at <paramref name="starCount"/>
        /// each, unless the chat is free or the user said not to ask again at this price. False
        /// when they decline.
        /// </summary>
        public static async Task<bool> ConfirmAsync(INavigationService navigationService, ISettingsService settings, Chat chat, int messageCount, long starCount)
        {
            settings.Chats.TryGet(chat.Id, null, ChatSetting.PaidMessageStarCount, out long savedMessageStarCount);

            if (starCount == 0 || starCount == savedMessageStarCount)
            {
                return true;
            }

            var message1 = Locale.Declension(Strings.R.MessageLockedStarsConfirmMessage1, starCount, chat.Title);

            string message2;
            if (messageCount > 1)
            {
                var message3 = Locale.Declension(Strings.R.MessageLockedStarsConfirmMessage2Many1, starCount * messageCount);
                var message4 = Locale.Declension(Strings.R.MessageLockedStarsConfirmMessage2Many2, messageCount);

                message2 = string.Format("{0} {1}", message3, message4);
            }
            else
            {
                message2 = Locale.Declension(Strings.R.MessageLockedStarsConfirmMessage2One, starCount);
            }

            var popup = new MessagePopup
            {
                Title = Strings.MessageLockedStarsConfirmTitle,
                Message = string.Format("{0} {1}", message1, message2),
                CheckBoxLabel = Strings.MessageLockedStarsConfirmMessageDontAsk,
                PrimaryButtonText = Icons.Premium16 + Icons.Spacing + (starCount * messageCount).ToString("N0"), //Locale.Declension(Strings.R.MessageLockedStarsConfirmMessagePay, messageCount),
                SecondaryButtonText = Strings.Cancel
            };

            var confirm = await navigationService.ShowPopupAsync(popup);
            if (confirm != ContentDialogResult.Primary)
            {
                return false;
            }

            if (popup.IsChecked is true)
            {
                settings.Chats[chat.Id, null, ChatSetting.PaidMessageStarCount] = starCount;
            }

            return true;
        }
    }
}
