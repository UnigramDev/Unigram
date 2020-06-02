using System;
using System.Linq;
using Telegram.Td.Api;
using Unigram.Common;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;

namespace Unigram.Controls
{
    public sealed partial class PollOptionControl : ToggleButton
    {
        private bool _allowToggle;

        public PollOptionControl()
        {
            InitializeComponent();
        }

        public void UpdatePollOption(Poll poll, PollOption option)
        {
            var results = poll.IsClosed || poll.Options.Any(x => x.IsChosen);
            var correct = poll.Type is PollTypeQuiz quiz && quiz.CorrectOptionId == poll.Options.IndexOf(option);

            this.IsThreeState = results;
            this.IsChecked = results ? null : new bool?(false);
            this.Tag = option;

            _allowToggle = poll.Type is PollTypeRegular regular && regular.AllowMultipleAnswers && !results;

            Ellipse.Opacity = results || option.IsBeingChosen ? 0 : 1;

            Percentage.Visibility = results ? Visibility.Visible : Visibility.Collapsed;
            Percentage.Text = $"{option.VotePercentage}%";

            ToolTipService.SetToolTip(Percentage, results ? Locale.Declension(poll.Type is PollTypeQuiz ? "Answer" : "Vote", option.VoterCount) : null);

            Text.Text = option.Text;

            Zero.Visibility = results ? Visibility.Visible : Visibility.Collapsed;

            Votes.Maximum = results ? Math.Max(poll.Options.Max(x => x.VoterCount), 1) : 1;
            Votes.Value = results ? option.VoterCount : 0;

            Loading.IsActive = option.IsBeingChosen;

            Tick.Visibility = (results && correct) || option.IsChosen ? Visibility.Visible : Visibility.Collapsed;

            if (option.IsChosen && poll.Type is PollTypeQuiz && !correct)
            {
                VisualStateManager.GoToState(LayoutRoot, "Wrong", false);
            }
            else
            {
                VisualStateManager.GoToState(LayoutRoot, "Normal", false);
            }

            AutomationProperties.SetName(this, option.Text);
        }

        protected override void OnToggle()
        {
            if (!_allowToggle)
            {
                return;
            }

            base.OnToggle();
        }

        private Visibility ConvertCheckMark(bool? check)
        {
            return check == true ? Visibility.Visible : Visibility.Collapsed;
        }
    }
}
