using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.ViewModels;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Unigram.Controls.Messages.Content
{
    public sealed partial class PollContent : StackPanel, IContent
    {
        private MessageViewModel _message;
        public MessageViewModel Message => _message;

        private DispatcherTimer _timeoutTimer;

        public PollContent(MessageViewModel message)
        {
            InitializeComponent();
            UpdateMessage(message);
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {

        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            _timeoutTimer?.Stop();
            _timeoutTimer = null;
        }

        public void UpdateMessage(MessageViewModel message)
        {
            _message = message;

            var poll = message.Content as MessagePoll;
            if (poll == null)
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

            Question.Text = poll.Poll.Question;
            Votes.Text = poll.Poll.TotalVoterCount > 0
                ? Locale.Declension(poll.Poll.Type is PollTypeQuiz ? "Answer" : "Vote", poll.Poll.TotalVoterCount)
                : poll.Poll.Type is PollTypeQuiz
                ? Strings.Resources.NoVotesQuiz
                : Strings.Resources.NoVotes;

            if (poll.Poll.Type is PollTypeRegular reg)
            {
                Type.Text = poll.Poll.IsClosed ? Strings.Resources.FinalResults : poll.Poll.IsAnonymous ? Strings.Resources.AnonymousPoll : Strings.Resources.PublicPoll;
                View.Visibility = results && poll.Poll.TotalVoterCount > 0 && !poll.Poll.IsAnonymous ? Visibility.Visible : Visibility.Collapsed;
                Submit.Visibility = !results && reg.AllowMultipleAnswers ? Visibility.Visible : Visibility.Collapsed;
                Explanation.Visibility = Visibility.Collapsed;
            }
            else if (poll.Poll.Type is PollTypeQuiz quiz)
            {
                Type.Text = poll.Poll.IsClosed ? Strings.Resources.FinalResults : poll.Poll.IsAnonymous ? Strings.Resources.AnonymousQuizPoll : Strings.Resources.QuizPoll;
                View.Visibility = results && poll.Poll.TotalVoterCount > 0 && !poll.Poll.IsAnonymous ? Visibility.Visible : Visibility.Collapsed;
                Submit.Visibility = Visibility.Collapsed;
                Explanation.Visibility = results && !string.IsNullOrEmpty(quiz.Explanation?.Text) ? Visibility.Visible : Visibility.Collapsed;
            }

            Votes.Visibility = View.Visibility == Visibility.Collapsed && Submit.Visibility == Visibility.Collapsed ? Visibility.Visible : Visibility.Collapsed;

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
                        button.UpdatePollOption(poll.Poll, poll.Poll.Options[i]);

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
                    button.UpdatePollOption(poll.Poll, poll.Poll.Options[i]);

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

            RecentVoters.Children.Clear();

            foreach (var id in poll.Poll.RecentVoterUserIds)
            {
                var user = message.ProtoService.GetUser(id);
                if (user == null)
                {
                    continue;
                }

                var picture = new ProfilePicture();
                picture.Source = PlaceholderHelper.GetUser(message.ProtoService, user, 16);
                picture.Width = 16;
                picture.Height = 16;

                if (RecentVoters.Children.Count > 0)
                {
                    picture.Margin = new Thickness(-6, -1, 0, 0);
                }
                else
                {
                    picture.Margin = new Thickness(0, -1, 0, 0);
                }

                RecentVoters.Children.Add(picture);
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
            }
            else
            {
                _timeoutTimer?.Stop();
                TimeoutLabel.Visibility = Visibility.Collapsed;
            }
        }

        public bool IsValid(MessageContent content, bool primary)
        {
            return content is MessagePoll;
        }

        private async void Option_Click(object sender, RoutedEventArgs e)
        {
            if (_message?.SchedulingState != null)
            {
                await MessagePopup.ShowAsync(Strings.Resources.MessageScheduledVote, Strings.Resources.AppName, Strings.Resources.OK);
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

            _message.Delegate.VotePoll(_message, new[] { option });
        }

        private void Option_Toggled(object sender, RoutedEventArgs e)
        {
            Submit.IsEnabled = false;

            foreach (PollOptionControl button in Options.Children)
            {
                if (button.IsChecked == true && button.Tag is PollOption option)
                {
                    Submit.IsEnabled = true;
                }
            }
        }

        private void Submit_Click(object sender, RoutedEventArgs e)
        {
            var options = new List<PollOption>();

            foreach (PollOptionControl button in Options.Children)
            {
                if (button.IsChecked == true && button.Tag is PollOption option)
                {
                    options.Add(option);
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
            if (quiz == null)
            {
                return;
            }

            Window.Current.ShowTeachingTip(Explanation, quiz.Explanation, TeachingTipPlacementMode.TopLeft);
        }
    }
}
