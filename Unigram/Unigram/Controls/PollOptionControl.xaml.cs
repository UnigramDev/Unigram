using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Telegram.Td.Api;
using Unigram.Common;
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
    public sealed partial class PollOptionControl : ToggleButton
    {
        public PollOptionControl()
        {
            InitializeComponent();
        }

        public void UpdatePollOption(Poll poll, PollOption option)
        {
            var results = poll.IsClosed || poll.Options.Any(x => x.IsChosen);

            this.IsChecked = results;
            this.Tag = option;

            Ellipse.Opacity = results || option.IsBeingChosen ? 0 : 1;

            Percentage.Visibility = results ? Visibility.Visible : Visibility.Collapsed;
            Percentage.Text = $"{option.VotePercentage}%";

            ToolTipService.SetToolTip(Percentage, results ? Locale.Declension("Vote", option.VoterCount) : null);

            Text.Text = option.Text;

            Zero.Visibility = results ? Visibility.Visible : Visibility.Collapsed;

            Votes.Maximum = results ? poll.Options.Max(x => x.VoterCount) : 1;
            Votes.Value = results ? option.VoterCount : 0;
            
            Loading.IsActive = option.IsBeingChosen;
        }

        protected override void OnToggle() { }
    }
}
