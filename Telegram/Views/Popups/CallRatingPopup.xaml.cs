//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Telegram.Controls;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Telegram.Views.Popups
{
    public sealed partial class CallRatingPopup : ContentPopup
    {
        public CallRatingPopup()
        {
            InitializeComponent();

            Title = Strings.AppName;
            PrimaryButtonText = Strings.OK;
            SecondaryButtonText = Strings.Cancel;
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
            return rating < 3 ? Strings.CallReportIncludeLogs : Strings.VoipFeedbackCommentHint;
        }

        public int Rating => RatingBar.Value;

        public string Comment => CommentField.Text;

        public bool IncludeDebugLogs => DebugLogs.IsChecked == true;

        private void RatingBar_ValueChanged(object sender, RatingBarValueChangedEventArgs e)
        {
            CommentField.PlaceholderText = e.NewValue < 4 ? Strings.CallReportHint : Strings.VoipFeedbackCommentHint;
            CommentField.Visibility = e.NewValue < 5 ? Visibility.Visible : Visibility.Collapsed;

            Debug.Visibility = e.NewValue < 4 ? Visibility.Visible : Visibility.Collapsed;

            IsPrimaryButtonEnabled = e.NewValue >= 1;
        }
    }
}
