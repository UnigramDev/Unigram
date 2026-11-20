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
using Telegram.ViewModels.Delegates;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

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
        private bool _explanationExpanded;

        private IMessageDelegate _mediaDelegate;
        private PollMedia _media;
        private PollMedia _explanationMedia;

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
        private Grid ExplanationRoot;
        private DashPath ExplanationDash;
        private GlyphButton ExplanationClose;
        private FormattedTextBlock ExplanationText;
        private Border ExplanationMedia;
        private StackPanel Options;
        private PollAddContent AddOption;
        private TextBlock Votes;
        private Button Submit;
        private Button Save;
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

            if (!recycled)
            {
                _explanationExpanded = false;
            }

            // Made once per message rather than with each medium's: those messages hold it weakly,
            // and the controls keep the messages they were given while the media stay the same.
            if (!recycled || _mediaDelegate == null)
            {
                _mediaDelegate = message.Delegate is DialogMessageDelegate dialog
                    ? new PollMediaDelegate(dialog, !message.CanBeSaved)
                    : message.Delegate;
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

            UpdateMedia(message, poll, recycled);

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

            var canExplain = (voted || showResults) && quiz != null && (quiz.Explanation.Text.Length > 0 || quiz.ExplanationMedia != null);
            var expanded = canExplain && _explanationExpanded;

            Explanation.Visibility = canExplain && !expanded
                ? Visibility.Visible
                : Visibility.Collapsed;

            UpdateExplanation(message, quiz, expanded, recycled);

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

            UpdateAddOption(message, poll);

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

        private void UpdateAddOption(MessageViewModel message, MessagePoll poll)
        {
            // Polls outside a chat (previews, instant view) have no delegate to add the option through.
            if (poll.CanAddOption && message.Delegate.IsDialog)
            {
                if (AddOption == null)
                {
                    AddOption = GetTemplateChild(nameof(AddOption)) as PollAddContent;
                    AddOption.IsEditingChanged += AddOption_IsEditingChanged;
                }

                AddOption.UpdateMessage(message);
                AddOption.Visibility = Visibility.Visible;
            }
            else if (AddOption != null)
            {
                AddOption.Visibility = Visibility.Collapsed;
            }
        }

        private void UpdateExplanation(MessageViewModel message, PollTypeQuiz quiz, bool expanded, bool recycled)
        {
            if (!expanded)
            {
                if (ExplanationRoot != null)
                {
                    ExplanationRoot.Visibility = Visibility.Collapsed;
                }

                return;
            }

            if (ExplanationRoot == null)
            {
                ExplanationRoot = GetTemplateChild(nameof(ExplanationRoot)) as Grid;
                ExplanationDash = GetTemplateChild(nameof(ExplanationDash)) as DashPath;
                ExplanationClose = GetTemplateChild(nameof(ExplanationClose)) as GlyphButton;
                ExplanationText = GetTemplateChild(nameof(ExplanationText)) as FormattedTextBlock;
                ExplanationMedia = GetTemplateChild(nameof(ExplanationMedia)) as Border;

                ExplanationClose.Click += ExplanationClose_Click;
                ExplanationText.TextEntityClick += QuestionText_TextEntityClick;
            }

            ExplanationRoot.Visibility = Visibility.Visible;

            UpdateExplanationColors(message);

            ExplanationText.SetText(message.ClientService, quiz.Explanation);
            ExplanationText.Visibility = quiz.Explanation.Text.Length > 0
                ? Visibility.Visible
                : Visibility.Collapsed;

            UpdateExplanationMedia(message, quiz, recycled);
        }

        private void UpdateExplanationColors(MessageViewModel message)
        {
            var outgoing = message.IsOutgoing && !message.IsChannelPost;
            var (accent, giftColors) = outgoing ? (null, null) : message.GetSender() switch
            {
                User user => (message.ClientService.GetAccentColor(user.AccentColorId), user.UpgradedGiftColors),
                Chat chat => (message.ClientService.GetAccentColor(chat.AccentColorId), chat.UpgradedGiftColors),
                _ => (null, null)
            };

            if (giftColors != null)
            {
                var colors = giftColors.ForTheme(ActualTheme, out _);
                BorderBrush = new SolidColorBrush(colors[0]);

                ExplanationDash.Stripe1 = colors.Count > 1 ? colors[1] : default;
                ExplanationDash.Stripe2 = colors.Count > 2 ? colors[2] : default;
            }
            else if (accent != null)
            {
                var colors = accent.ForTheme(ActualTheme);
                BorderBrush = new SolidColorBrush(colors[0]);

                ExplanationDash.Stripe1 = colors.Count > 1 ? colors[1] : default;
                ExplanationDash.Stripe2 = colors.Count > 2 ? colors[2] : default;
            }
            else
            {
                ClearValue(BorderBrushProperty);

                ExplanationDash.Stripe1 = default;
                ExplanationDash.Stripe2 = default;
            }
        }

        private void UpdateExplanationMedia(MessageViewModel message, PollTypeQuiz quiz, bool recycled)
        {
            var same = recycled && PollOptionContent.IsSameMedia(_explanationMedia, quiz.ExplanationMedia);

            _explanationMedia = quiz.ExplanationMedia;

            if (!same)
            {
                UpdateMedia(ExplanationMedia, message, _mediaDelegate, quiz.ExplanationMedia, quiz.Explanation);

                // Their padding insets them from a bubble's edge, and the block is inset already.
                if (ExplanationMedia.Child is AudioContent or DocumentContent)
                {
                    (ExplanationMedia.Child as Control).Padding = new Thickness(0);
                }
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
            if (AddOption is { Visibility: Visibility.Visible, IsEditing: true })
            {
                if (Save == null)
                {
                    Save = GetTemplateChild(nameof(Save)) as Button;
                    Save.Click += Save_Click;
                }

                Save.Visibility = Visibility.Visible;
                Submit.Visibility = Visibility.Collapsed;
                View.Visibility = Visibility.Collapsed;
                Votes.Visibility = Visibility.Collapsed;
                return;
            }
            else if (Save != null)
            {
                Save.Visibility = Visibility.Collapsed;
            }

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

        private void UpdateMedia(MessageViewModel message, MessagePoll poll, bool recycled)
        {
            // Every update delivers new objects for the same media, and the content controls follow
            // their files by themselves: handing them a new message would only reload it.
            var same = recycled && PollOptionContent.IsSameMedia(_media, poll.Media);

            _media = poll.Media;

            if (!same)
            {
                UpdateMedia(Media, message, _mediaDelegate, poll.Media, poll.Description);
            }
        }

        /// <summary>
        /// Hosts <paramref name="media"/> in the content control for its type, given a message of its
        /// own: the controls are written for the content of a message, and in a poll message they
        /// would only find the poll. The caption is what the gallery shows.
        /// </summary>
        private static void UpdateMedia(Border host, MessageViewModel message, IMessageDelegate delegato, PollMedia media, FormattedText caption)
        {
            MessageContent content = media switch
            {
                PollMediaAnimation animation => new MessageAnimation(animation.Animation, caption, false, false, false),
                PollMediaAudio audio => new MessageAudio(audio.Audio, caption),
                PollMediaDocument document => new MessageDocument(document.Document, caption),
                PollMediaLocation location => new MessageLocation(location.Location),
                PollMediaPhoto photo => new MessagePhoto(photo.Photo, photo.Video, caption, false, false, false),
                PollMediaVenue venue => new MessageVenue(venue.Venue),
                PollMediaVideo video => new MessageVideo(video.Video, video.AlternativeVideos, video.Storyboards, video.Cover, video.StartTimestamp, caption, false, false, false),
                _ => null
            };

            if (content == null)
            {
                if (host.Child is IContent previous)
                {
                    previous.Recycle();
                }

                host.Child = null;
                host.Visibility = Visibility.Collapsed;
                return;
            }

            // The poll's own id: an audio's playlist is searched for in the chat from it, and the
            // thumbnails tell one message's placeholder from another's by it.
            var viewModel = new MessageViewModel(message.ClientService, delegato, message.Chat, null, null, new Message { ChatId = message.ChatId, Id = message.Id, Content = content, SchedulingState = new MessageSchedulingStateSendWhenOnline() });

            host.Visibility = Visibility.Visible;

            if (host.Child is IContent current)
            {
                if (current.IsValid(content, true))
                {
                    current.UpdateMessage(viewModel);
                    return;
                }

                current.Recycle();
            }

            host.Child = content switch
            {
                MessageAnimation => new AnimationContent(viewModel),
                MessageAudio => new AudioContent(viewModel),
                MessageDocument => new DocumentContent(viewModel),
                MessageLocation => new LocationContent(viewModel),
                MessagePhoto => new PhotoContent(viewModel),
                MessageVenue => new VenueContent(viewModel),
                MessageVideo => new VideoContent(viewModel),
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
            else if (ExplanationRoot?.Visibility == Visibility.Visible && ExplanationMedia.Child is IContentWithPlayback explanation)
            {
                return explanation.GetPlaybackElement();
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

                    return new Rect(point.X, point.Y, (float)button.ActualWidth, (float)button.ActualHeight);
                }
            }

            return Rect.Empty;
        }

        public void Recycle()
        {
            _message = null;

            StopTimeoutTimer();
            AddOption?.Recycle();

            if (Media?.Child is IContent media)
            {
                media.Recycle();
            }

            if (ExplanationMedia?.Child is IContent explanation)
            {
                explanation.Recycle();
            }

            _media = null;
            _explanationMedia = null;
            _mediaDelegate = null;
        }

        public bool IsValid(MessageContent content, bool primary)
        {
            return content is MessagePoll;
        }

        private async void Option_Click(object sender, RoutedEventArgs e)
        {
            if (_message?.Content is not MessagePoll poll)
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

        private void AddOption_IsEditingChanged(object sender, EventArgs e)
        {
            if (_message?.Content is MessagePoll poll)
            {
                UpdateFooter(poll.Poll);
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            AddOption?.Submit();
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
            SetExplanationExpanded(true);
        }

        private void ExplanationClose_Click(object sender, RoutedEventArgs e)
        {
            SetExplanationExpanded(false);
        }

        public void ShowExplanation()
        {
            SetExplanationExpanded(true);
        }

        private void SetExplanationExpanded(bool expanded)
        {
            if (_message == null || _explanationExpanded == expanded)
            {
                return;
            }

            // The button pressed collapses with the focus, which would go wherever the window decides.
            var focused = expanded
                ? Explanation.FocusState != FocusState.Unfocused
                : ExplanationClose?.FocusState is not null and not FocusState.Unfocused;

            _explanationExpanded = expanded;
            UpdateMessage(_message);

            if (focused)
            {
                if (expanded)
                {
                    ExplanationClose?.Focus(FocusState.Programmatic);
                }
                else
                {
                    Explanation.Focus(FocusState.Programmatic);
                }
            }
        }
    }
}
