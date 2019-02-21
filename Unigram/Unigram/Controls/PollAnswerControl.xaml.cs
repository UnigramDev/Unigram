using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Telegram.Td.Api;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace Unigram.Controls
{
    public sealed partial class PollAnswerControl : Button
    {
        public PollAnswerControl()
        {
            InitializeComponent();
        }

        public void UpdatePollAnswer(Poll poll, PollAnswer answer)
        {
            var results = poll.IsClosed || poll.Answers.Any(x => x.IsChosen);
            var percent = ((double)answer.VoterCount / (double)poll.TotalVoterCount) * 100;

            this.IsEnabled = !results;
            this.Tag = answer;

            Ellipse.Opacity = results ? 0 : 1;

            Percentage.Visibility = results ? Visibility.Visible : Visibility.Collapsed;
            Percentage.Text = ((int)percent).ToString() + "%";

            Text.Text = answer.Text;

            Zero.Visibility = results ? Visibility.Visible : Visibility.Collapsed;

            Votes.Visibility = results ? Visibility.Visible : Visibility.Collapsed;
            Votes.Maximum = poll.TotalVoterCount;
            Votes.Value = answer.VoterCount;
        }
    }
}
