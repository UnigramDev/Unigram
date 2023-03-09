//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Linq;
using Telegram.Common;
using Telegram.Td.Api;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;

namespace Telegram.Controls
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

            var votes = Locale.Declension(poll.Type is PollTypeQuiz ? "Answer" : "Vote", option.VoterCount);

            IsThreeState = results;
            IsChecked = results ? null : new bool?(false);
            Tag = option;

            _allowToggle = poll.Type is PollTypeRegular regular && regular.AllowMultipleAnswers && !results;

            Ellipse.Opacity = results || option.IsBeingChosen ? 0 : 1;

            Percentage.Visibility = results ? Visibility.Visible : Visibility.Collapsed;
            Percentage.Text = $"{option.VotePercentage}%";

            ToolTipService.SetToolTip(Percentage, results ? votes : null);

            Text.Text = option.Text;

            Zero.Visibility = results ? Visibility.Visible : Visibility.Collapsed;

            Votes.Maximum = results ? Math.Max(poll.Options.Max(x => x.VoterCount), 1) : 1;
            Votes.Value = results ? option.VoterCount : 0;

            Loading.IsActive = option.IsBeingChosen;

            Tick.Visibility = (results && correct) || option.IsChosen ? Visibility.Visible : Visibility.Collapsed;

            if (option.IsChosen && poll.Type is PollTypeQuiz)
            {
                VisualStateManager.GoToState(LayoutRoot, correct ? "Correct" : "Wrong", false);
            }
            else
            {
                VisualStateManager.GoToState(LayoutRoot, "Normal", false);
            }

            if (results)
            {
                AutomationProperties.SetName(this, $"{option.Text}, {votes}, {option.VotePercentage}%");
            }
            else
            {
                AutomationProperties.SetName(this, option.Text);
            }
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
