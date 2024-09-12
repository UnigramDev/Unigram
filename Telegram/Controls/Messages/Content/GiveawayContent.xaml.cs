//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Text;
using Telegram.Common;
using Telegram.Converters;
using Telegram.Entities;
using Telegram.Streams;
using Telegram.Td.Api;
using Telegram.ViewModels;

namespace Telegram.Controls.Messages.Content
{
    public sealed partial class GiveawayContent : Control, IContent
    {
        private MessageViewModel _message;
        public MessageViewModel Message => _message;

        public GiveawayContent(MessageViewModel message)
        {
            _message = message;

            DefaultStyleKey = typeof(GiveawayContent);
        }

        #region InitializeComponent

        private AnimatedImage Animation;
        private BadgeControl Count;
        private TextBlock PrizesLabel;
        private TextBlock ParticipantsLabel;
        private StackPanel ParticipantsPanel;
        private TextBlock FromLabel;
        private TextBlock WinnersLabel;
        private BadgeButton Button;
        private bool _templateApplied;

        protected override void OnApplyTemplate()
        {
            Animation = GetTemplateChild(nameof(Animation)) as AnimatedImage;
            Count = GetTemplateChild(nameof(Count)) as BadgeControl;
            PrizesLabel = GetTemplateChild(nameof(PrizesLabel)) as TextBlock;
            ParticipantsLabel = GetTemplateChild(nameof(ParticipantsLabel)) as TextBlock;
            ParticipantsPanel = GetTemplateChild(nameof(ParticipantsPanel)) as StackPanel;
            FromLabel = GetTemplateChild(nameof(FromLabel)) as TextBlock;
            WinnersLabel = GetTemplateChild(nameof(WinnersLabel)) as TextBlock;
            Button = GetTemplateChild(nameof(Button)) as BadgeButton;

            Button.Click += Button_Click;

            _templateApplied = true;

            if (_message != null)
            {
                UpdateMessage(_message);
            }
        }

        #endregion

        public void UpdateMessage(MessageViewModel message)
        {
            _message = message;

            if (!_templateApplied)
            {
                return;
            }

            if (message.Content is MessageGiveaway giveaway)
            {
                UpdateMessageGiveaway(message, giveaway);
            }
            else if (message.Content is MessageGiveawayWinners giveawayWinners)
            {
                UpdateMessageGiveawayWinners(message, giveawayWinners);
            }
        }

        private void UpdateMessageGiveaway(MessageViewModel message, MessageGiveaway giveaway)
        { 
            Animation.Source = giveaway.Sticker != null
                ? new DelayedFileSource(message.ClientService, giveaway.Sticker)
                : null;

            Count.Text = $"X{giveaway.WinnerCount}";

            if (giveaway.Prize is GiveawayPrizePremium prizePremium)
            {
                var months = Locale.Declension(Strings.R.Months, prizePremium.MonthCount, false);
                var duration = string.Format(Strings.BoostingGiveawayMsgInfo, giveaway.WinnerCount.ToString("N0"), string.Format(months, $"**{prizePremium.MonthCount}**"));

                TextBlockHelper.SetMarkdown(PrizesLabel, duration);
            }
            else if (giveaway.Prize is GiveawayPrizeStars prizeStars)
            {
                var stars = Locale.Declension(Strings.R.BoostingStarsGiveawayMsgInfoPlural1, prizeStars.StarCount);
                var winners = Locale.Declension(Strings.R.BoostingStarsGiveawayMsgInfoPlural2, giveaway.WinnerCount);

                TextBlockHelper.SetMarkdown(PrizesLabel, string.Format("{0} {1}", stars, winners));
            }

            ParticipantsLabel.Text = giveaway.Parameters.OnlyNewMembers
                ? Locale.Declension(Strings.R.BoostingGiveawayMsgNewSubsPlural, 1 + giveaway.Parameters.AdditionalChatIds.Count, false)
                : Locale.Declension(Strings.R.BoostingGiveawayMsgAllSubsPlural, 1 + giveaway.Parameters.AdditionalChatIds.Count, false);

            ParticipantsPanel.Children.Clear();

            if (message.ClientService.TryGetChat(giveaway.Parameters.BoostedChatId, out Chat boostedChat))
            {
                var button = new ChatPill();
                button.SetChat(message.ClientService, boostedChat);
                button.Click += Chat_Click;
                button.Margin = new Thickness(0, 4, 0, 0);
                button.HorizontalAlignment = HorizontalAlignment.Center;

                ParticipantsPanel.Children.Add(button);
            }

            foreach (var chat in message.ClientService.GetChats(giveaway.Parameters.AdditionalChatIds))
            {
                var button = new ChatPill();
                button.SetChat(message.ClientService, chat);
                button.Click += Chat_Click;
                button.Margin = new Thickness(0, 4, 0, 0);
                button.HorizontalAlignment = HorizontalAlignment.Center;

                ParticipantsPanel.Children.Add(button);
            }

            if (giveaway.Parameters.CountryCodes.Count > 0)
            {
                var builder = new StringBuilder();

                foreach (var code in giveaway.Parameters.CountryCodes)
                {
                    if (Country.Codes.TryGetValue(code, out var value))
                    {
                        if (builder.Length > 0)
                        {
                            builder.Append(", ");
                        }

                        builder.AppendFormat("{0} {1}", value.Emoji, value.DisplayName);
                    }
                }

                FromLabel.Text = string.Format(Strings.BoostingGiveAwayFromCountries, builder.ToString());
                FromLabel.Visibility = Visibility.Visible;
            }
            else
            {
                FromLabel.Visibility = Visibility.Collapsed;
            }

            WinnersLabel.Text = Formatter.DateAt(giveaway.Parameters.WinnersSelectionDate);
        }

        private void UpdateMessageGiveawayWinners(MessageViewModel message, MessageGiveawayWinners giveaway)
        {
            // TODO:
            Animation.Source = new AnimatedEmojiFileSource(message.ClientService, "\U0001F389");

            Count.Text = $"X{giveaway.WinnerCount}";

            if (giveaway.Prize is GiveawayPrizePremium prizePremium)
            {
                var months = Locale.Declension(Strings.R.Months, prizePremium.MonthCount, false);
                var duration = string.Format(Strings.BoostingGiveawayMsgInfo, giveaway.WinnerCount.ToString("N0"), string.Format(months, $"**{prizePremium.MonthCount}**"));

                TextBlockHelper.SetMarkdown(PrizesLabel, duration);
            }
            else if (giveaway.Prize is GiveawayPrizeStars prizeStars)
            {
                var stars = Locale.Declension(Strings.R.BoostingStarsGiveawayMsgInfoPlural1, prizeStars.StarCount);
                var winners = Locale.Declension(Strings.R.BoostingStarsGiveawayMsgInfoPlural2, giveaway.WinnerCount);

                TextBlockHelper.SetMarkdown(PrizesLabel, string.Format("{0} {1}", stars, winners));
            }

            //ParticipantsLabel.Text = giveaway.Parameters.OnlyNewMembers
            //    ? Locale.Declension(Strings.R.BoostingGiveawayMsgNewSubsPlural, 1 + giveaway.Parameters.AdditionalChatIds.Count, false)
            //    : Locale.Declension(Strings.R.BoostingGiveawayMsgAllSubsPlural, 1 + giveaway.Parameters.AdditionalChatIds.Count, false);

            ParticipantsPanel.Children.Clear();

            //if (message.ClientService.TryGetChat(giveaway.Parameters.BoostedChatId, out Chat boostedChat))
            //{
            //    var button = new ChatPill();
            //    button.SetChat(message.ClientService, boostedChat);
            //    button.Click += Chat_Click;
            //    button.Margin = new Thickness(0, 4, 0, 0);
            //    button.HorizontalAlignment = HorizontalAlignment.Center;

            //    ParticipantsPanel.Children.Add(button);
            //}

            foreach (var user in message.ClientService.GetUsers(giveaway.WinnerUserIds))
            {
                var button = new ChatPill();
                button.SetUser(message.ClientService, user);
                button.Click += Chat_Click;
                button.Margin = new Thickness(0, 4, 0, 0);
                button.HorizontalAlignment = HorizontalAlignment.Center;

                ParticipantsPanel.Children.Add(button);
            }

            FromLabel.Visibility = Visibility.Collapsed;

            //WinnersLabel.Text = Formatter.DateAt(giveaway.Parameters.WinnersSelectionDate);
        }

        private void Chat_Click(object sender, RoutedEventArgs e)
        {
            if (sender is ChatPill pill)
            {
                _message.Delegate.OpenChat(pill.ChatId);
            }
        }

        public void Recycle()
        {
            _message = null;
        }

        public bool IsValid(MessageContent content, bool primary)
        {
            return content is MessageGiveaway or MessageGiveawayWinners;
        }

        private async void Button_Click(object sender, RoutedEventArgs e)
        {
            var giveaway = _message.Content as MessageGiveaway;
            if (giveaway == null)
            {
                return;
            }

            Button.ShowSkeleton();

            // TODO: how des it work?
            var response = await _message.ClientService.SendAsync(new GetGiveawayInfo(_message.ChatId, _message.Id));
            if (response is not GiveawayInfoOngoing and not GiveawayInfoCompleted)
            {
                // TODO
                return;
            }

            Button.HideSkeleton();

            var boostedChat = _message.ClientService.GetChat(giveaway.Parameters.BoostedChatId);
            var isChannel = boostedChat.Type is ChatTypeSupergroup { IsChannel: true };

            var creationTimeStamp = response switch
            {
                GiveawayInfoOngoing ongoing => ongoing.CreationDate,
                GiveawayInfoCompleted completed1 => completed1.CreationDate,
                _ => _message.Date
            };

            var selectionTimeStamp = response switch
            {
                GiveawayInfoCompleted completed2 => completed2.ActualWinnersSelectionDate,
                _ => giveaway.Parameters.WinnersSelectionDate
            };

            var winnerCount = response switch
            {
                GiveawayInfoCompleted completed6 => completed6.WinnerCount.ToString("N0"),
                _ => giveaway.WinnerCount.ToString("N0")
            };

            string message1;
            if (giveaway.Prize is GiveawayPrizePremium prizePremium)
            {
                if (response is GiveawayInfoCompleted)
                {
                    message1 = Locale.Declension(isChannel
                        ? Strings.R.BoostingGiveawayHowItWorksTextEnd
                        : Strings.R.BoostingGiveawayHowItWorksTextEndGroup, giveaway.WinnerCount, false);
                    message1 = string.Format(message1, string.Empty, boostedChat.Title, winnerCount, Locale.Declension(Strings.R.BoldMonths, prizePremium.MonthCount));
                }
                else
                {
                    message1 = Locale.Declension(isChannel
                        ? Strings.R.BoostingGiveawayHowItWorksText
                        : Strings.R.BoostingGiveawayHowItWorksTextGroup, giveaway.WinnerCount, false);
                    message1 = string.Format(message1, string.Empty, boostedChat.Title, winnerCount, Locale.Declension(Strings.R.BoldMonths, prizePremium.MonthCount));
                }
            }
            else if (giveaway.Prize is GiveawayPrizeStars prizeStars)
            {
                if (response is GiveawayInfoCompleted)
                {
                    message1 = Locale.Declension(isChannel
                        ? Strings.R.BoostingStarsGiveawayHowItWorksTextEnd
                        : Strings.R.BoostingStarsGiveawayHowItWorksTextEndGroup, prizeStars.StarCount, boostedChat.Title);
                }
                else
                {
                    message1 = Locale.Declension(isChannel
                        ? Strings.R.BoostingStarsGiveawayHowItWorksText
                        : Strings.R.BoostingStarsGiveawayHowItWorksTextGroup, prizeStars.StarCount, boostedChat.Title);
                }
            }
            else
            {
                return;
            }

            if (giveaway.Parameters.PrizeDescription.Length > 0)
            {
                var additional = Locale.Declension(Strings.R.BoostingGiveawayHowItWorksIncludeText, giveaway.WinnerCount, false);
                message1 += "\n\n" + string.Format(additional, giveaway.WinnerCount, boostedChat.Title, giveaway.Parameters.PrizeDescription);
            }

            var selectionDate = Formatter.DayMonthFull.Format(Formatter.ToLocalTime(selectionTimeStamp));

            string message2;
            if (giveaway.Parameters.OnlyNewMembers)
            {
                var creationTime = Formatter.Time(creationTimeStamp);
                var creationDate = Formatter.DayMonthFull.Format(Formatter.ToLocalTime(creationTimeStamp));

                if (giveaway.Parameters.AdditionalChatIds.Count > 0)
                {
                    var several = Locale.Declension(Strings.R.BoostingGiveawayHowItWorksSubTextDateSeveral2, giveaway.Parameters.AdditionalChatIds.Count, false);
                    var key = response is GiveawayInfoCompleted
                        ? Strings.R.BoostingGiveawayHowItWorksSubTextDateSeveralEnd1
                        : Strings.R.BoostingGiveawayHowItWorksSubTextDateSeveral1;

                    several = string.Format(several, giveaway.Parameters.AdditionalChatIds.Count, creationTime, creationDate);

                    message2 = Locale.Declension(key, giveaway.WinnerCount, false);
                    message2 = string.Format(message2, string.Empty, selectionDate, winnerCount, boostedChat.Title, several);
                }
                else
                {
                    var key = response is GiveawayInfoCompleted
                        ? Strings.R.BoostingGiveawayHowItWorksSubTextDateEnd
                        : Strings.R.BoostingGiveawayHowItWorksSubTextDate;

                    message2 = Locale.Declension(key, giveaway.WinnerCount, false);
                    message2 = string.Format(message2, string.Empty, selectionDate, winnerCount, boostedChat.Title, creationTime, creationDate);
                }
            }
            else
            {
                if (giveaway.Parameters.AdditionalChatIds.Count > 0)
                {
                    var several = Locale.Declension(Strings.R.BoostingGiveawayHowItWorksSubTextSeveral2, giveaway.Parameters.AdditionalChatIds.Count);
                    var key = response is GiveawayInfoCompleted
                        ? Strings.R.BoostingGiveawayHowItWorksSubTextSeveralEnd1
                        : Strings.R.BoostingGiveawayHowItWorksSubTextSeveral1;

                    message2 = Locale.Declension(key, giveaway.WinnerCount, false);
                    message2 = string.Format(message2, string.Empty, selectionDate, winnerCount, boostedChat.Title, several);
                }
                else
                {
                    var key = response is GiveawayInfoCompleted
                        ? Strings.R.BoostingGiveawayHowItWorksSubTextEnd
                        : Strings.R.BoostingGiveawayHowItWorksSubText;

                    message2 = Locale.Declension(key, giveaway.WinnerCount, false);
                    message2 = string.Format(message2, string.Empty, selectionDate, winnerCount, boostedChat.Title);
                }
            }

            string title;
            string message3;
            string primary;
            string secondary;

            if (response is GiveawayInfoCompleted completed)
            {
                if (completed.ActivationCount > 0)
                {
                    message2 += " " + Locale.Declension(Strings.R.BoostingGiveawayUsedLinksPlural, completed.ActivationCount);
                }

                title = Strings.BoostingGiveawayEnd;
                message3 = completed.WasRefunded
                    ? Strings.BoostingGiveawayCanceledByPayment
                    : completed.GiftCode.Length > 0
                    ? Strings.BoostingGiveawayYouWon
                    : Strings.BoostingGiveawayYouNotWon;

                primary = completed.GiftCode.Length > 0
                    ? Strings.BoostingGiveawayViewPrize
                    : Strings.Close;

                secondary = completed.GiftCode.Length > 0
                    ? Strings.Close
                    : string.Empty;
            }
            else if (response is GiveawayInfoOngoing ongoing)
            {
                string Participating()
                {
                    if (giveaway.Parameters.AdditionalChatIds.Count > 0)
                    {
                        return Locale.Declension(Strings.R.BoostingGiveawayParticipantMultiPlural, giveaway.Parameters.AdditionalChatIds.Count, boostedChat.Title);
                    }

                    return string.Format(Strings.BoostingGiveawayParticipant, boostedChat.Title);
                }

                string Eligible()
                {
                    string title = null;
                    int count = 0;

                    if (_message.ClientService.TryGetSupergroup(boostedChat, out Supergroup boostedSupergroup))
                    {
                        if (!boostedSupergroup.IsMember())
                        {
                            title = boostedChat.Title;
                        }
                    }

                    foreach (var chat in _message.ClientService.GetChats(giveaway.Parameters.AdditionalChatIds))
                    {
                        if (_message.ClientService.TryGetSupergroup(chat, out Supergroup supergroup))
                        {
                            if (!supergroup.IsMember())
                            {
                                if (title == null)
                                {
                                    title = chat.Title;
                                }
                                else
                                {
                                    count++;
                                }
                            }
                        }
                    }

                    if (count > 0)
                    {
                        return Locale.Declension(Strings.R.BoostingGiveawayTakePartMultiPlural, count, title, selectionDate);
                    }

                    return string.Format(Strings.BoostingGiveawayTakePart, title, selectionDate);

                    // Simplified version
                    if (giveaway.Parameters.AdditionalChatIds.Count > 0)
                    {
                        return Locale.Declension(Strings.R.BoostingGiveawayTakePartMultiPlural, giveaway.Parameters.AdditionalChatIds.Count, boostedChat.Title);
                    }

                    return string.Format(Strings.BoostingGiveawayTakePart, boostedChat.Title);
                }

                title = Strings.BoostingGiveAwayAbout;
                message3 = ongoing.Status switch
                {
                    GiveawayParticipantStatusEligible => Eligible(),
                    GiveawayParticipantStatusParticipating => Participating(),
                    GiveawayParticipantStatusAdministrator administrator => string.Format(Strings.BoostingGiveawayNotEligibleAdmin, _message.ClientService.GetTitle(administrator.ChatId)),
                    GiveawayParticipantStatusAlreadyWasMember already => string.Format(Strings.BoostingGiveawayNotEligible, Formatter.DateAt(already.JoinedChatDate)),
                    GiveawayParticipantStatusDisallowedCountry => Strings.BoostingGiveawayNotEligibleCountry,
                    _ => string.Empty
                };

                primary = Strings.Close;
                secondary = string.Empty;
            }
            else
            {
                return;
            }

            var confirm = await MessagePopup.ShowAsync(XamlRoot, message1 + "\n\n" + message2 + "\n\n" + message3, title, primary, secondary);
            if (confirm == ContentDialogResult.Primary && response is GiveawayInfoCompleted completed3 && completed3.GiftCode.Length > 0)
            {
                MessageHelper.OpenTelegramUrl(_message.ClientService, _message.Delegate.NavigationService, new InternalLinkTypePremiumGiftCode(completed3.GiftCode));
            }
        }
    }
}
