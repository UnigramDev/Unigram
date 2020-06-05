using Unigram.Controls;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Unigram.Views.Popups
{
    public sealed partial class CallRatingPopup : ContentPopup
    {
        public CallRatingPopup()
        {
            this.InitializeComponent();

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
            return rating >= 0 && rating <= 3 ? Visibility.Visible : Visibility.Collapsed;
        }

        private string ConvertPlaceholder(int rating)
        {
            return rating < 3 ? Strings.Resources.CallReportIncludeLogs : Strings.Resources.VoipFeedbackCommentHint;
        }

        public int Rating
        {
            get
            {
                return RatingBar.Value;
            }
        }

        public string Comment
        {
            get
            {
                return CommentField.Text;
            }
        }

        public bool IncludeDebugLogs
        {
            get
            {
                return DebugLogs.IsChecked == true;
            }
        }

        private void RatingBar_ValueChanged(object sender, RatingBarValueChangedEventArgs e)
        {
            CommentField.PlaceholderText = e.NewValue < 4 ? Strings.Resources.CallReportHint : Strings.Resources.VoipFeedbackCommentHint;
            CommentField.Visibility = e.NewValue < 5 ? Visibility.Visible : Visibility.Collapsed;

            Debug.Visibility = e.NewValue < 4 ? Visibility.Visible : Visibility.Collapsed;

            IsPrimaryButtonEnabled = e.NewValue >= 1;
        }
    }
}
