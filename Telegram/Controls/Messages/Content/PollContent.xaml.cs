//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Telegram.Common;
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

        public PollContent(MessageViewModel message)
        {
            _message = message;

            DefaultStyleKey = typeof(PollContent);
        }

        protected override void OnUnloaded()
        {
            _timeoutTimer?.Stop();
            _timeoutTimer = null;
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

            var results = poll.Poll.IsClosed || poll.Poll.Options.Any(x => x.IsChosen);

            if (poll.Poll.Type is PollTypeQuiz && poll.Poll.CloseDate != 0 && !results)
            {
                var now = DateTime.Now.ToTimestamp();

                var diff = poll.Poll.CloseDate - now;
                if (diff > 0)
                {
                    TimeoutLabel.Visibility = Visibility.Visible;
                    Timeout.Text = TimeSpan.FromSeconds(diff).ToString("m\\:ss");

                    if (_timeoutTimer == null)
                    {
                        _timeoutTimer = new DispatcherTimer();
                        _timeoutTimer.Interval = TimeSpan.FromSeconds(1);
                        _timeoutTimer.Tick += TimeoutTimer_Tick;
                    }

                    _timeoutTimer.Stop();
                    _timeoutTimer.Start();
                }
                else
                {
                    _timeoutTimer?.Stop();
                    TimeoutLabel.Visibility = Visibility.Collapsed;
                }
            }
            else
            {
                _timeoutTimer?.Stop();
                TimeoutLabel.Visibility = Visibility.Collapsed;
            }

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

            if (poll.Poll.Type is PollTypeRegular reg)
            {
                Type.Text = poll.Poll.IsClosed ? Strings.FinalResults : poll.Poll.IsAnonymous ? Strings.AnonymousPoll : Strings.PublicPoll;
                View.Visibility = results && poll.Poll.TotalVoterCount > 0 && !poll.Poll.IsAnonymous
                    ? Visibility.Visible
                    : Visibility.Collapsed;
                Submit.Visibility = !results && poll.Poll.AllowsMultipleAnswers
                    ? Visibility.Visible
                    : Visibility.Collapsed;
                Explanation.Visibility = Visibility.Collapsed;
            }
            else if (poll.Poll.Type is PollTypeQuiz quiz)
            {
                Type.Text = poll.Poll.IsClosed ? Strings.FinalResults : poll.Poll.IsAnonymous ? Strings.AnonymousQuizPoll : Strings.QuizPoll;
                View.Visibility = results && poll.Poll.TotalVoterCount > 0 && !poll.Poll.IsAnonymous
                    ? Visibility.Visible
                    : Visibility.Collapsed;
                Submit.Visibility = Visibility.Collapsed;
                Explanation.Visibility = results && !string.IsNullOrEmpty(quiz.Explanation?.Text)
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }

            Votes.Visibility = View.Visibility == Visibility.Collapsed
                && Submit.Visibility == Visibility.Collapsed
                    ? Visibility.Visible
                    : Visibility.Collapsed;

            Submit.IsEnabled = false;
            //Options.Children.Clear();

            //foreach (var option in poll.Poll.Options)
            //{
            //    var button = new PollOptionControl();
            //    button.Click += Option_Click;
            //    button.UpdatePollOption(poll.Poll, option);

            //    Options.Children.Add(button);
            //}

            for (int i = 0; i < Math.Max(poll.Poll.Options.Count, Options.Children.Count); i++)
            {
                if (i < Options.Children.Count)
                {
                    var button = Options.Children[i] as PollOptionContent;
                    button.Click -= Option_Click;
                    button.Checked -= Option_Toggled;
                    button.Unchecked -= Option_Toggled;

                    if (i < poll.Poll.Options.Count)
                    {
                        button.UpdatePollOption(message, poll.Poll, poll.Poll.Options[i]);

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
                    else
                    {
                        Options.Children.Remove(button);
                    }
                }
                else
                {
                    var button = new PollOptionContent();
                    button.UpdatePollOption(message, poll.Poll, poll.Poll.Options[i]);

                    if (poll.Poll.AllowsMultipleAnswers && poll.Poll.VoteRestrictionReason == null)
                    {
                        button.Checked += Option_Toggled;
                        button.Unchecked += Option_Toggled;
                    }
                    else
                    {
                        button.Click += Option_Click;
                    }

                    Options.Children.Add(button);
                }
            }

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

        public void UpdateMedia(MessageViewModel message, MessagePoll poll)
        {
            // Currently, can be only of the types messageAnimation, messageAudio, messageDocument, messageLocation, messagePhoto, messageVenue, or messageVideo without caption

            var content = poll.Media;

            if (Media.Child is IContent media)
            {
                if (media.IsValid(message.Content, true))
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

            // Media.Margin = new Thickness(10, 4, 10, 8);
            Media.Margin = content switch
            {
                //MessageAnimation => new AnimationContent(message),
                PollMediaAudio => new Thickness(10, 8, 10, 4),
                PollMediaDocument => new Thickness(10, 8, 10, 4),
                //MessageLocation => new LocationContent(message),
                //MessagePhoto => new PhotoContent(message),
                //MessageVenue => new VenueContent(message),
                //MessageVideo => new VideoContent(message),
                _ => new Thickness(0)
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
            MessageBubble.TextEntityClick(_message, sender as FormattedTextBlock, e);
        }

        private void RecentVoters_RecentUserHeadChanged(ProfilePicture photo, MessageSender sender)
        {
            photo.Source = ProfilePictureSource.MessageSender(_message.ClientService, sender);
        }

        private void TimeoutTimer_Tick(object sender, object e)
        {
            var poll = _message?.Content as MessagePoll;
            if (poll == null)
            {
                _timeoutTimer?.Stop();
                return;
            }

            var now = DateTime.Now.ToTimestamp();

            var diff = poll.Poll.CloseDate - now;
            if (diff > 0)
            {
                Timeout.Text = TimeSpan.FromSeconds(diff).ToString("m\\:ss");

                if (diff <= 5 && !_runningOut)
                {
                    _runningOut = true;
                    VisualStateManager.GoToState(this, "RunningOut", false);
                }
                else if (diff > 5 && _runningOut)
                {
                    _runningOut = false;
                    VisualStateManager.GoToState(this, "Default", false);
                }
            }
            else
            {
                _timeoutTimer?.Stop();
                TimeoutLabel.Visibility = Visibility.Collapsed;
            }
        }

        public Rect Highlight(MessageBubbleHighlightOptions options)
        {
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
            Submit.IsEnabled = false;

            foreach (PollOptionContent button in Options.Children)
            {
                if (button.IsChecked == true && button.Option is PollOption)
                {
                    Submit.IsEnabled = true;
                }
            }
        }

        private void Submit_Click(object sender, RoutedEventArgs e)
        {
            List<int> options = null;

            for (int i = 0; i < Options.Children.Count; i++)
            {
                var button = Options.Children[i] as PollOptionContent;
                if (button != null && button.IsChecked == true)
                {
                    options ??= new();
                    options.Add(i);
                }
            }

            var poll = _message?.Content as MessagePoll;
            if (poll == null)
            {
                return;
            }

            _message.Delegate.VotePoll(_message, options);
        }

        private void View_Click(object sender, RoutedEventArgs e)
        {
            _message.Delegate.OpenPoll(_message);
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

            ToastPopup.Show(Explanation, quiz.Explanation, TeachingTipPlacementMode.TopLeft);
        }

        public void ShowExplanation()
        {
            Explanation_Click(null, null);
        }
    }
}
