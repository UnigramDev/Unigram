using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.ViewModels;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.UI.Xaml.Shapes;

namespace Unigram.Controls.Messages.Content
{
    public sealed partial class PollContent : StackPanel, IContent
    {
        private MessageViewModel _message;
        public MessageViewModel Message => _message;

        public PollContent(MessageViewModel message)
        {
            InitializeComponent();
            UpdateMessage(message);
        }

        public void UpdateMessage(MessageViewModel message)
        {
            _message = message;

            var poll = message.Content as MessagePoll;
            if (poll == null)
            {
                return;
            }

            Question.Text = poll.Poll.Question;
            Type.Text = poll.Poll.IsClosed ? Strings.Resources.FinalResults : Strings.Resources.AnonymousPoll; // No public polls for now
            Votes.Text = poll.Poll.TotalVoterCount > 0 ? Locale.Declension("Vote", poll.Poll.TotalVoterCount) : Strings.Resources.NoVotes;

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

                    if (i < poll.Poll.Options.Count)
                    {
                        button.UpdatePollOption(poll.Poll, poll.Poll.Options[i]);
                    }
                    else
                    {
                        Options.Children.Remove(button);
                    }
                }
                else
                {
                    var button = new PollOptionControl();
                    button.Click += Option_Click;
                    button.UpdatePollOption(poll.Poll, poll.Poll.Options[i]);

                    Options.Children.Add(button);
                }
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
                await TLMessageDialog.ShowAsync(Strings.Resources.MessageScheduledVote, Strings.Resources.AppName, Strings.Resources.OK);
                return;
            }

            var button = sender as PollOptionControl;
            if (button.IsChecked == true)
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

            _message.Delegate.VotePoll(_message, option);
        }
    }
}
