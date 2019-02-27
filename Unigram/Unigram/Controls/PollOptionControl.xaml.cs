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
    public sealed partial class PollOptionControl : Button
    {
        public PollOptionControl()
        {
            InitializeComponent();
        }

        public void UpdatePollOption(Poll poll, PollOption option)
        {
            var results = poll.IsClosed || poll.Options.Any(x => x.IsChosen);
            var percent = ((double)option.VoterCount / (double)poll.TotalVoterCount) * 100;
            percent = double.IsNaN(percent) ? 0 : percent;

            this.IsEnabled = !results;
            this.Tag = option;

            Ellipse.Opacity = results ? 0 : 1;

            Percentage.Visibility = results ? Visibility.Visible : Visibility.Collapsed;
            Percentage.Text = ((int)percent).ToString() + "%";

            ToolTipService.SetToolTip(Percentage, Locale.Declension("Vote", option.VoterCount));

            Text.Text = option.Text;

            Zero.Visibility = results ? Visibility.Visible : Visibility.Collapsed;

            //Votes.Visibility = results ? Visibility.Visible : Visibility.Collapsed;
            Votes.Maximum = 100;
            Votes.Value = results ? percent : 0;
        }
    }
}
