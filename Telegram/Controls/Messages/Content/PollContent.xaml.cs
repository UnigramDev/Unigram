//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Globalization;
using System.Linq;
using Telegram.Common;
using Telegram.Converters;
using Telegram.Native.Controls;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Telegram.Controls.Messages.Content
{
    public sealed partial class PollContent : ControlEx, IContent, IContentWithPlayback
    {
        private MessageViewModel _message;
        public MessageViewModel Message => _message;

        private DispatcherTimer _timeoutTimer;
        private bool _runningOut;

        private bool _canVote;
        private bool _canPreview;
        private bool _canViewVotes;
        private bool _showPreview;
        private bool _resultsHidden;
        private bool _awaitingVote;
        private bool _botChat;
        private bool _showClock;

        public PollContent(MessageViewModel message)
        {
            _message = message;

            DefaultStyleKey = typeof(PollContent);
        }

        protected override void OnLoaded()
        {
            if (_templateApplied && _message?.Content is MessagePoll poll)
            {
                UpdateTimeout(poll.Poll);
            }
        }

        protected override void OnUnloaded()
        {
            StopTimeoutTimer();
        }

        private void StopTimeoutTimer()
        {
            if (_timeoutTimer != null)
            {
                _timeoutTimer.Stop();
                _timeoutTimer.Tick -= TimeoutTimer_Tick;
                _timeoutTimer = null;
            }
        }

        #region InitializeComponent

        private Border Media;
        private FormattedTextBlock Description;
        private FormattedTextBlock QuestionText;
        private TextBlock Type;
        private RecentUserHeads RecentVoters;
        private StackPanel TimeoutLabel;
        private TextBlock Timeout;
        private TextBlock TimeoutGlyph;
        private GlyphButton Explanation;
        private StackPanel Options;
        private TextBlock Votes;
        private Button Submit;
        private Button View;
        private TextBlock ViewText;
        private TextBlock EndsIn;
        private bool _templateApplied;

        protected override void OnApplyTemplate()
        {
            Media = GetTemplateChild(nameof(Media)) as Border;
            Description = GetTemplateChild(nameof(Description)) as FormattedTextBlock;
            QuestionText = GetTemplateChild(nameof(QuestionText)) as FormattedTextBlock;
            Type = GetTemplateChild(nameof(Type)) as TextBlock;
            RecentVoters = GetTemplateChild(nameof(RecentVoters)) as RecentUserHeads;
            TimeoutLabel = GetTemplateChild(nameof(TimeoutLabel)) as StackPanel;
            Timeout = GetTemplateChild(nameof(Timeout)) as TextBlock;
            TimeoutGlyph = GetTemplateChild(nameof(TimeoutGlyph)) as TextBlock;
            Explanation = GetTemplateChild(nameof(Explanation)) as GlyphButton;
            Options = GetTemplateChild(nameof(Options)) as StackPanel;
            Votes = GetTemplateChild(nameof(Votes)) as TextBlock;
            Submit = GetTemplateChild(nameof(Submit)) as Button;
            View = GetTemplateChild(nameof(View)) as Button;
            ViewText = GetTemplateChild(nameof(ViewText)) as TextBlock;
            EndsIn = GetTemplateChild(nameof(EndsIn)) as TextBlock;

            Description.TextEntityClick += QuestionText_TextEntityClick;
            QuestionText.TextEntityClick += QuestionText_TextEntityClick;
            RecentVoters.RecentUserHeadChanged += RecentVoters_RecentUserHeadChanged;
            Explanation.Click += Explanation_Click;
            Submit.Click += Submit_Click;
            View.Click += View_Click;

            _templateApplied = true;

            if (_message != null)
            {
                UpdateMessage(_message);
            }
        }

        #endregion

        public void UpdateMessage(MessageViewModel message)
        {
            var recycled = _message?.Id == message.Id;

            _message = message;

            var poll = message.Content as MessagePoll;
            if (poll == null | !_templateApplied)
            {
                return;
            }

            var pollOptions = poll.Poll.Options;
            var voted = false;
            var beingChosen = false;
            var maximum = 0;

            for (int i = 0; i < pollOptions.Count; i++)
            {
                var option = pollOptions[i];
                voted |= option.IsChosen;
                beingChosen |= option.IsBeingChosen;
                maximum = Math.Max(maximum, option.VoterCount);
            }

            var closed = poll.Poll.IsClosed;
            var total = poll.Poll.TotalVoterCount;
            var restricted = poll.Poll.VoteRestrictionReason is PollVoteRestrictionReasonCountryRestricted or PollVoteRestrictionReasonMembershipRequired;

            _canVote = !voted && !closed && poll.Poll.VoteRestrictionReason == null;

            // Only the creator gets here: the rest either voted, are restricted, or can't see results.
            _canPreview = !voted && !closed && !restricted && poll.Poll.CanSeeResults && total > 0;
            _canViewVotes = poll.Poll.CanGetVoters && total > 0 && (voted || closed);

            if (!recycled || !_canPreview)
            {
                _showPreview = false;
            }

            // CanSeeResults is also true for the creator before voting and, whether or not the
            // poll hides its results, for a restricted user. A hidden poll sends that user a total
            // and no per-option counts, which is the only way to tell the two apart.
            var showResults = closed
                || _showPreview
                || (voted && poll.Poll.CanSeeResults)
                || (restricted && (maximum > 0 || total == 0));

            // Until the answer is saved a chosen option is still being chosen and CanSeeResults is
            // false on every poll, so this would otherwise be true for a moment after any vote.
            var resultsHidden = voted && !closed && !beingChosen && !poll.Poll.CanSeeResults;

            // The first render counts as recycled, because the constructor already set _message:
            // without this, a poll voted long ago would show the toast whenever it scrolls in.
            if (resultsHidden && _awaitingVote && recycled)
            {
                message.Delegate.NavigationService.ShowToast(Strings.PollResultsWillLater, ToastPopupIcon.Info);
            }

            _awaitingVote = !voted || beingChosen;
            _resultsHidden = resultsHidden;
            _botChat = message.ClientService.TryGetUser(message.Chat, out User user) && user.Type is UserTypeBot;
            _showClock = _botChat && poll.Poll.Type is PollTypeQuiz && !voted && !closed;

            UpdateTimeout(poll.Poll);

            UpdateMedia(message, poll);

            Description.SetText(message.ClientService, poll.Description);
            Description.Visibility = poll.Description.Text.Length > 0
                ? Visibility.Visible
                : Visibility.Collapsed;

            QuestionText.SetText(message.ClientService, poll.Poll.Question);

            Votes.Text = poll.Poll.TotalVoterCount > 0
                ? Locale.Declension(poll.Poll.Type is PollTypeQuiz ? Strings.R.Answer : Strings.R.Vote, poll.Poll.TotalVoterCount)
                : poll.Poll.Type is PollTypeQuiz
                ? Strings.NoVotesQuiz
                : Strings.NoVotes;

            var quiz = poll.Poll.Type as PollTypeQuiz;

            Type.Text = closed
                ? Strings.FinalResults
                : quiz != null
                ? poll.Poll.IsAnonymous ? Strings.AnonymousQuizPoll : Strings.QuizPoll
                : poll.Poll.IsAnonymous ? Strings.AnonymousPoll : Strings.PublicPoll;

            Explanation.Visibility = (voted || showResults) && !string.IsNullOrEmpty(quiz?.Explanation.Text)
                ? Visibility.Visible
                : Visibility.Collapsed;

            var order = poll.Poll.OptionOrder;
            var shuffled = order.Count == pollOptions.Count;

            var canVote = _canVote && !_showPreview;

            for (int i = 0; i < pollOptions.Count; i++)
            {
                PollOptionContent button;

                if (i < Options.Children.Count)
                {
                    button = Options.Children[i] as PollOptionContent;
                    DetachOption(button);
                }
                else
                {
                    button = new PollOptionContent();
                    Options.Children.Add(button);
                }

                button.UpdatePollOption(message, poll.Poll, pollOptions[shuffled ? order[i] : i], canVote, showResults, maximum);

                if (poll.Poll.AllowsMultipleAnswers && poll.Poll.VoteRestrictionReason == null)
                {
                    button.Checked += Option_Toggled;
                    button.Unchecked += Option_Toggled;
                }
                else
                {
                    button.Click += Option_Click;
                }
            }

            for (int i = Options.Children.Count - 1; i >= pollOptions.Count; i--)
            {
                DetachOption(Options.Children[i] as PollOptionContent);
                Options.Children.RemoveAt(i);
            }

            // Reads the options' checked state, so it has to follow them.
            UpdateFooter(poll.Poll);

            var destination = RecentVoters.Items;
            var origin = poll.Poll.RecentVoterIds;

            if (destination.Count > 0 && recycled)
            {
                destination.ReplaceDiff(origin);
            }
            else
            {
                destination.ReplaceWith(origin);
            }
        }

        private void DetachOption(PollOptionContent button)
        {
            button.Click -= Option_Click;
            button.Checked -= Option_Toggled;
            button.Unchecked -= Option_Toggled;
        }

        private void UpdateFooter(Poll poll)
        {
            var total = poll.TotalVoterCount;
            var submit = poll.AllowsMultipleAnswers && _canVote && !_showPreview;
            var hasChecked = false;

            if (submit)
            {
                for (int i = 0; i < Options.Children.Count; i++)
                {
                    if (Options.Children[i] is PollOptionContent { IsChecked: true })
                    {
                        hasChecked = true;
                        break;
                    }
                }
            }

            string view = null;

            if (_canPreview && !(submit && hasChecked))
            {
                submit = false;
                view = poll.CanGetVoters
                    ? string.Format(Strings.PollViewVotesX, total)
                    : _showPreview
                    ? Strings.PollViewBack
                    : Locale.Declension(Strings.R.PollViewVotesAsAdmin, total);
            }
            else if (!submit && _canViewVotes)
            {
                view = string.Format(Strings.PollViewVotesX, total);
            }

            if (view != null)
            {
                ViewText.Text = view;
            }

            View.Visibility = view != null
                ? Visibility.Visible
                : Visibility.Collapsed;
            Submit.Visibility = submit
                ? Visibility.Visible
                : Visibility.Collapsed;
            Submit.IsEnabled = hasChecked;
            Votes.Visibility = view == null && !submit
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private void UpdateTimeout(Poll poll)
        {
            var diff = poll.CloseDate > 0 && !poll.IsClosed
                ? poll.CloseDate - DateTime.Now.ToUnixTimeSeconds()
                : 0;

            var clock = diff > 0 && _showClock;
            var label = diff > 0 && !_botChat;

            TimeoutLabel.Visibility = clock
                ? Visibility.Visible
                : Visibility.Collapsed;
            EndsIn.Visibility = label
                ? Visibility.Visible
                : Visibility.Collapsed;

            if (clock)
            {
                Timeout.Text = string.Format("{0}:{1:00}", diff / 60, diff % 60);
            }
            else if (label)
            {
                var time = diff < 86400
                    ? Formatter.Duration(diff)
                    : Locale.Declension(Strings.R.Days, diff / 86400);

                EndsIn.Text = string.Format(_resultsHidden ? Strings.PollResultsIn : Strings.PollEndsIn, time);
            }

            var runningOut = clock && diff <= 5;
            if (runningOut != _runningOut)
            {
                _runningOut = runningOut;
                VisualStateManager.GoToState(this, runningOut ? "RunningOut" : "Default", false);
            }

            // A running timer keeps this control alive, so it only runs while the control is in
            // the tree. OnLoaded picks it up again. Above a day the text only changes once a day.
            if ((clock || label) && IsConnected)
            {
                var interval = TimeSpan.FromSeconds(diff >= 86400 && !clock ? diff % 86400 + 1 : 1);

                if (_timeoutTimer == null)
                {
                    _timeoutTimer = new DispatcherTimer();
                    _timeoutTimer.Tick += TimeoutTimer_Tick;
                }

                if (_timeoutTimer.Interval != interval)
                {
                    _timeoutTimer.Interval = interval;
                }

                if (!_timeoutTimer.IsEnabled)
                {
                    _timeoutTimer.Start();
                }
            }
            else
            {
                StopTimeoutTimer();
            }
        }

        public void UpdateMedia(MessageViewModel message, MessagePoll poll)
        {
            // Currently, can be only of the types messageAnimation, messageAudio, messageDocument, messageLocation, messagePhoto, messageVenue, or messageVideo without caption

            var content = poll.Media;

            if (Media.Child is IContent media)
            {
                // The content hosted here is not the primary one: the poll is.
                if (media.IsValid(message.Content, false))
                {
                    media.UpdateMessage(message);
                    return;
                }
                else
                {
                    media.Recycle();
                }
            }

            //if (Media.Child is StickerContent or VideoNoteContent)
            //{
            //    UpdateAttach(message);
            //}

            Media.Child = content switch
            {
                PollMediaAnimation => new AnimationContent(message),
                PollMediaAudio => new AudioContent(message),
                PollMediaDocument => new DocumentContent(message),
                PollMediaLocation => new LocationContent(message),
                PollMediaPhoto => new PhotoContent(message),
                PollMediaVenue => new VenueContent(message),
                PollMediaVideo => new VideoContent(message),
                _ => null
            };
        }

        public IPlayerView GetPlaybackElement()
        {
            if (Media?.Child is IContentWithPlayback content)
            {
                return content.GetPlaybackElement();
            }
            else if (Media?.Child is IPlayerView playback)
            {
                return playback;
            }

            return null;
        }

        private void QuestionText_TextEntityClick(object sender, TextEntityClickEventArgs e)
        {
            MessageBubble.TextEntityClick(_message, e);
        }

        private void RecentVoters_RecentUserHeadChanged(ProfilePicture photo, MessageSender sender)
        {
            photo.Source = ProfilePictureSource.MessageSender(_message.ClientService, sender);
        }

        private void TimeoutTimer_Tick(object sender, object e)
        {
            if (_message?.Content is MessagePoll poll)
            {
                UpdateTimeout(poll.Poll);
            }
            else
            {
                StopTimeoutTimer();
            }
        }

        public Rect Highlight(MessageBubbleHighlightOptions options)
        {
            if (Options == null)
            {
                return Rect.Empty;
            }

            foreach (var child in Options.Children)
            {
                if (child is PollOptionContent button
                    && button.Option.Id == options.PollOptionId)
                {
                    var transform = child.TransformToVisual(this);
                    var point = transform.TransformPoint(new Point());

                    return new Rect(point.X, point.Y, button.ActualWidth, button.ActualHeight);
                }
            }

            return Rect.Empty;
        }

        public void Recycle()
        {
            _message = null;

            StopTimeoutTimer();

            if (Media?.Child is IContent media)
            {
                media.Recycle();
            }
        }

        public bool IsValid(MessageContent content, bool primary)
        {
            return content is MessagePoll;
        }

        private async void Option_Click(object sender, RoutedEventArgs e)
        {
            if (_message.Content is not MessagePoll poll)
            {
                return;
            }

            if (poll.Poll.VoteRestrictionReason is PollVoteRestrictionReasonScheduled)
            {
                await MessagePopup.ShowAsync(XamlRoot, Strings.MessageScheduledVote, Strings.AppName, Strings.OK);
                return;
            }
            else if (poll.Poll.VoteRestrictionReason is PollVoteRestrictionReasonCountryRestricted)
            {
                if (poll.Poll.CountryCodes.Count > 2)
                {
                    _message.Delegate.NavigationService.ShowToast(string.Format(Strings.PollV2ToastOnlyUsersFromCountriesCanVoteOther, string.Join(", ", poll.Poll.CountryCodes.Take(poll.Poll.CountryCodes.Count - 1).Select(x => CountryName(x))), CountryName(poll.Poll.CountryCodes[^1])), ToastPopupIcon.Ban);
                }
                else if (poll.Poll.CountryCodes.Count > 1)
                {
                    _message.Delegate.NavigationService.ShowToast(string.Format(Strings.PollV2ToastOnlyUsersFromCountriesCanVoteOther, CountryName(poll.Poll.CountryCodes[0]), CountryName(poll.Poll.CountryCodes[1])), ToastPopupIcon.Ban);
                }
                else
                {
                    _message.Delegate.NavigationService.ShowToast(string.Format(Strings.PollV2ToastOnlyUsersFromCountriesCanVoteOne, CountryName(poll.Poll.CountryCodes[0])), ToastPopupIcon.Ban);
                }

                return;

                static string CountryName(string name)
                {
                    try
                    {
                        var info = new RegionInfo(name);
                        return info.DisplayName;
                    }
                    catch
                    {
                        return name;
                    }
                }
            }
            else if (poll.Poll.VoteRestrictionReason is PollVoteRestrictionReasonMembershipRequired membershipRequired && _message.ClientService.TryGetChat(membershipRequired.ChatId, out Chat restrictedChat))
            {
                if (_message.ClientService.TryGetSupergroup(restrictedChat, out Supergroup supergroup) && supergroup.Status is ChatMemberStatusLeft)
                {
                    _message.Delegate.NavigationService.ShowToast(string.Format(Strings.PollV2ToastOnlySubscribersCanVote, restrictedChat.Title), ToastPopupIcon.Ban);
                }
                else if (_message.ClientService.TryGetBasicGroup(restrictedChat, out BasicGroup basicGroup) && basicGroup.Status is ChatMemberStatusLeft)
                {
                    _message.Delegate.NavigationService.ShowToast(string.Format(Strings.PollV2ToastOnlySubscribersCanVote, restrictedChat.Title), ToastPopupIcon.Ban);
                }
                else
                {
                    _message.Delegate.NavigationService.ShowToast(string.Format(Strings.PollV2ToastOnlySubscribersJoined24hCanVote, restrictedChat.Title), ToastPopupIcon.Ban);
                }

                return;
            }

            if (poll.Poll.VoteRestrictionReason != null)
            {
                return;
            }

            var button = sender as PollOptionContent;
            if (button.IsChecked == null)
            {
                return;
            }

            var option = button.Option as PollOption;
            if (option == null)
            {
                return;
            }

            var optionId = poll.Poll.Options.IndexOf(option);
            if (optionId != -1)
            {
                _message.Delegate.VotePoll(_message, new[] { optionId });
            }
        }

        private void Option_Toggled(object sender, RoutedEventArgs e)
        {
            if (_message?.Content is MessagePoll poll)
            {
                UpdateFooter(poll.Poll);
            }
        }

        private void Submit_Click(object sender, RoutedEventArgs e)
        {
            var poll = _message?.Content as MessagePoll;
            if (poll == null)
            {
                return;
            }

            MutableVector<int> options = null;

            for (int i = 0; i < Options.Children.Count; i++)
            {
                // The children follow the display order, the API takes indexes into Poll.Options.
                var button = Options.Children[i] as PollOptionContent;
                if (button != null && button.IsChecked == true)
                {
                    var optionId = poll.Poll.Options.IndexOf(button.Option);
                    if (optionId != -1)
                    {
                        options ??= new();
                        options.Add(optionId);
                    }
                }
            }

            _message.Delegate.VotePoll(_message, options);
        }

        private void View_Click(object sender, RoutedEventArgs e)
        {
            if (_message?.Content is not MessagePoll poll)
            {
                return;
            }

            if (poll.Poll.CanGetVoters)
            {
                _message.Delegate.OpenPoll(_message);
            }
            else
            {
                _showPreview = !_showPreview;
                UpdateMessage(_message);
            }
        }

        private void Explanation_Click(object sender, RoutedEventArgs e)
        {
            var poll = _message?.Content as MessagePoll;
            if (poll == null)
            {
                return;
            }

            var quiz = poll.Poll.Type as PollTypeQuiz;
            if (string.IsNullOrEmpty(quiz?.Explanation.Text))
            {
                return;
            }

            ToastPopup.Show(Explanation, quiz.Explanation, ToastPlacementMode.TopLeft);
        }

        public void ShowExplanation()
        {
            Explanation_Click(null, null);
        }
    }
}
