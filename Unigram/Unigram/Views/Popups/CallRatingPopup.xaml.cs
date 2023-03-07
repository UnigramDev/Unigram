//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Unigram.Controls;

namespace Unigram.Views.Popups
{
    public sealed partial class CallRatingPopup : ContentPopup
    {
        public CallRatingPopup()
        {
            InitializeComponent();

            Title = Strings.Resources.AppName;
            PrimaryButtonText = Strings.Resources.OK;
            SecondaryButtonText = Strings.Resources.Cancel;
        }

        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
        }

        private void ContentDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
        }

        private Visibility ConvertVisibility(int rating)
        {
            return rating is >= 0 and <= 3 ? Visibility.Visible : Visibility.Collapsed;
        }

        private string ConvertPlaceholder(int rating)
        {
            return rating < 3 ? Strings.Resources.CallReportIncludeLogs : Strings.Resources.VoipFeedbackCommentHint;
        }

        public int Rating => RatingBar.Value;

        public string Comment => CommentField.Text;

        public bool IncludeDebugLogs => DebugLogs.IsChecked == true;

        private void RatingBar_ValueChanged(object sender, RatingBarValueChangedEventArgs e)
        {
            CommentField.PlaceholderText = e.NewValue < 4 ? Strings.Resources.CallReportHint : Strings.Resources.VoipFeedbackCommentHint;
            CommentField.Visibility = e.NewValue < 5 ? Visibility.Visible : Visibility.Collapsed;

            Debug.Visibility = e.NewValue < 4 ? Visibility.Visible : Visibility.Collapsed;

            IsPrimaryButtonEnabled = e.NewValue >= 1;
        }
    }
}
