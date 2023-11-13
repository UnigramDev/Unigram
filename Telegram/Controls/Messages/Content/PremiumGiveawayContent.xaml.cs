//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System.Text;
using Telegram.Common;
using Telegram.Converters;
using Telegram.Entities;
using Telegram.Streams;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;

namespace Telegram.Controls.Messages.Content
{
    public sealed class PremiumGiveawayContent : Control, IContent
    {
        private MessageViewModel _message;
        public MessageViewModel Message => _message;

        public PremiumGiveawayContent(MessageViewModel message)
        {
            _message = message;

            DefaultStyleKey = typeof(PremiumGiveawayContent);
        }

        #region InitializeComponent

        private AnimatedImage Animation;
        private BadgeControl Count;
        private TextBlock PrizesLabel;
        private TextBlock ParticipantsLabel;
        private WrapPanel ParticipantsPanel;
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
            ParticipantsPanel = GetTemplateChild(nameof(ParticipantsPanel)) as WrapPanel;
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

            var giveaway = message.Content as MessagePremiumGiveaway;
            if (giveaway == null || !_templateApplied)
            {
                return;
            }

            Animation.Source = new DelayedFileSource(message.ClientService, giveaway.Sticker.StickerValue);

            if (giveaway.Sticker.StickerValue.Local.IsDownloadingCompleted is false)
            {
                CompositionPathParser.ParseThumbnail(giveaway.Sticker, out ShapeVisual visual, false);
                ElementCompositionPreview.SetElementChildVisual(Animation, visual);
            }

            Count.Text = $"X{giveaway.WinnerCount}";

            var months = Locale.Declension(Strings.R.Months, giveaway.MonthCount, false);
            var duration = string.Format(Strings.BoostingGiveawayMsgInfo, giveaway.WinnerCount.ToString("N0"), string.Format(months, $"**{giveaway.MonthCount}**"));

            TextBlockHelper.SetMarkdown(PrizesLabel, duration);

            ParticipantsLabel.Text = giveaway.Parameters.OnlyNewMembers
                ? Locale.Declension(Strings.R.BoostingGiveawayMsgNewSubsPlural, 1 + giveaway.Parameters.AdditionalChatIds.Count, false)
                : Locale.Declension(Strings.R.BoostingGiveawayMsgAllSubsPlural, 1 + giveaway.Parameters.AdditionalChatIds.Count, false);

            ParticipantsPanel.Children.Clear();

            if (message.ClientService.TryGetChat(giveaway.Parameters.BoostedChatId, out Chat boostedChat))
            {
                var button = new ChatPill();
                button.SetChat(message.ClientService, boostedChat);
                button.Click += Chat_Click;
                button.Margin = new Thickness(0, 2, 2, 0);

                ParticipantsPanel.Children.Add(button);
            }

            foreach (var chat in message.ClientService.GetChats(giveaway.Parameters.AdditionalChatIds))
            {
                var button = new ChatPill();
                button.SetChat(message.ClientService, chat);
                button.Click += Chat_Click;
                button.Margin = new Thickness(0, 2, 2, 0);

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
            return content is MessagePremiumGiveaway;
        }

        private async void Button_Click(object sender, RoutedEventArgs e)
        {
            var giveaway = _message.Content as MessagePremiumGiveaway;
            if (giveaway == null)
            {
                return;
            }

            Button.ShowSkeleton();

            // TODO: how des it work?
            var response = await _message.ClientService.SendAsync(new GetPremiumGiveawayInfo(_message.ChatId, _message.Id));
            if (response is not PremiumGiveawayInfoOngoing and not PremiumGiveawayInfoCompleted)
            {
                // TODO
                return;
            }

            Button.HideSkeleton();

            var boostedChat = _message.ClientService.GetChat(giveaway.Parameters.BoostedChatId);

            var creationTimeStamp = response switch
            {
                PremiumGiveawayInfoOngoing ongoing => ongoing.CreationDate,
                PremiumGiveawayInfoCompleted completed1 => completed1.CreationDate,
                _ => _message.Date
            };

            var selectionTimeStamp = response switch
            {
                PremiumGiveawayInfoCompleted completed2 => completed2.ActualWinnersSelectionDate,
                _ => giveaway.Parameters.WinnersSelectionDate
            };

            var winnerCount = response switch
            {
                PremiumGiveawayInfoCompleted completed6 => completed6.WinnerCount.ToString("N0"),
                _ => giveaway.WinnerCount.ToString("N0")
            };

            var message1 = Locale.Declension(Strings.R.BoostingGiveawayHowItWorksText, giveaway.WinnerCount, false);
            message1 = string.Format(message1, string.Empty, boostedChat.Title, winnerCount, Locale.Declension(Strings.R.BoldMonths, giveaway.MonthCount));

            var selectionDate = Formatter.DayMonthFull.Format(Formatter.ToLocalTime(selectionTimeStamp));

            string message2;
            if (giveaway.Parameters.OnlyNewMembers)
            {
                var creationTime = Formatter.ShortTime.Format(Formatter.ToLocalTime(creationTimeStamp));
                var creationDate = Formatter.DayMonthFull.Format(Formatter.ToLocalTime(creationTimeStamp));

                if (giveaway.Parameters.AdditionalChatIds.Count > 0)
                {
                    var several = Locale.Declension(Strings.R.BoostingGiveawayHowItWorksSubTextDateSeveral2, giveaway.Parameters.AdditionalChatIds.Count, false);
                    var key = response is PremiumGiveawayInfoCompleted
                        ? Strings.R.BoostingGiveawayHowItWorksSubTextDateSeveralEnd1
                        : Strings.R.BoostingGiveawayHowItWorksSubTextDateSeveral1;

                    several = string.Format(several, giveaway.Parameters.AdditionalChatIds.Count, creationTime, creationDate);

                    message2 = Locale.Declension(key, giveaway.WinnerCount, false);
                    message2 = string.Format(message2, string.Empty, selectionDate, winnerCount, boostedChat.Title, several);
                }
                else
                {
                    var key = response is PremiumGiveawayInfoCompleted
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
                    var key = response is PremiumGiveawayInfoCompleted
                        ? Strings.R.BoostingGiveawayHowItWorksSubTextSeveralEnd1
                        : Strings.R.BoostingGiveawayHowItWorksSubTextSeveral1;

                    message2 = Locale.Declension(key, giveaway.WinnerCount, false);
                    message2 = string.Format(message2, string.Empty, selectionDate, winnerCount, boostedChat.Title, several);
                }
                else
                {
                    var key = response is PremiumGiveawayInfoCompleted
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

            if (response is PremiumGiveawayInfoCompleted completed)
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
            else if (response is PremiumGiveawayInfoOngoing ongoing)
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
                    PremiumGiveawayParticipantStatusEligible => Eligible(),
                    PremiumGiveawayParticipantStatusParticipating => Participating(),
                    PremiumGiveawayParticipantStatusAdministrator administrator => string.Format(Strings.BoostingGiveawayNotEligibleAdmin, _message.ClientService.GetTitle(administrator.ChatId)),
                    PremiumGiveawayParticipantStatusAlreadyWasMember already => string.Format(Strings.BoostingGiveawayNotEligible, Formatter.DateAt(already.JoinedChatDate)),
                    PremiumGiveawayParticipantStatusDisallowedCountry => Strings.BoostingGiveawayNotEligibleCountry,
                    _ => string.Empty
                };

                primary = Strings.Close;
                secondary = string.Empty;
            }
            else
            {
                return;
            }

            var confirm = await MessagePopup.ShowAsync(message1 + "\n\n" + message2 + "\n\n" + message3, title, primary, secondary);
            if (confirm == ContentDialogResult.Primary && response is PremiumGiveawayInfoCompleted completed3 && completed3.GiftCode.Length > 0)
            {
                MessageHelper.OpenTelegramUrl(_message.ClientService, _message.Delegate.NavigationService, new InternalLinkTypePremiumGiftCode(completed3.GiftCode));
            }
        }
    }
}
