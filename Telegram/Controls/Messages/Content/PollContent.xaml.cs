//
// Copyright Fela Ameghino & Contributors 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using Telegram.Common;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;

namespace Telegram.Controls.Messages.Content
{
    public sealed class PollContent : ControlEx, IContent
    {
        private MessageViewModel _message;
        public MessageViewModel Message => _message;

        private DispatcherTimer _timeoutTimer;
        private bool _runningOut;

        public PollContent(MessageViewModel message)
        {
            _message = message;

            DefaultStyleKey = typeof(PollContent);

            Disconnected += OnUnloaded;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            _timeoutTimer?.Stop();
            _timeoutTimer = null;
        }

        #region InitializeComponent

        private RichTextBlock QuestionText;
        private Paragraph Question;
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
            QuestionText = GetTemplateChild(nameof(QuestionText)) as RichTextBlock;
            Question = GetTemplateChild(nameof(Question)) as Paragraph;
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

            CustomEmojiIcon.Add(QuestionText, Question.Inlines, message.ClientService, poll.Poll.Question);

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
                Submit.Visibility = !results && reg.AllowMultipleAnswers
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
                    var button = Options.Children[i] as PollOptionControl;
                    button.Click -= Option_Click;
                    button.Checked -= Option_Toggled;
                    button.Unchecked -= Option_Toggled;

                    if (i < poll.Poll.Options.Count)
                    {
                        button.UpdatePollOption(message.ClientService, poll.Poll, poll.Poll.Options[i]);

                        if (poll.Poll.Type is PollTypeRegular regular && regular.AllowMultipleAnswers)
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
                    var button = new PollOptionControl();
                    button.UpdatePollOption(message.ClientService, poll.Poll, poll.Poll.Options[i]);

                    if (poll.Poll.Type is PollTypeRegular regular && regular.AllowMultipleAnswers)
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
                destination.Clear();
                destination.AddRange(origin);
            }
        }

        private void RecentVoters_RecentUserHeadChanged(ProfilePicture photo, MessageSender sender)
        {
            if (_message.ClientService.TryGetUser(sender, out User user))
            {
                photo.SetUser(_message.ClientService, user, 20);
            }
            else if (_message.ClientService.TryGetChat(sender, out Chat chat))
            {
                photo.SetChat(_message.ClientService, chat, 20);
            }
            else
            {
                photo.Clear();
            }
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
            if (_message?.SchedulingState != null)
            {
                await MessagePopup.ShowAsync(XamlRoot, Strings.MessageScheduledVote, Strings.AppName, Strings.OK);
                return;
            }

            var button = sender as PollOptionControl;
            if (button.IsChecked == null)
            {
                return;
            }

            var option = button.Tag as PollOption;
            if (option == null)
            {
                return;
            }

            var poll = _message?.Content as MessagePoll;
            if (poll == null)
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

            foreach (PollOptionControl button in Options.Children)
            {
                if (button.IsChecked == true && button.Tag is PollOption)
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
                var button = Options.Children[i] as PollOptionControl;
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
            _message.Delegate.OpenMedia(_message, null);
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
