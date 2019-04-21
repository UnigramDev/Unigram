using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace Unigram.Controls.Views
{
    public sealed partial class CallRatingView : ContentDialog
    {
        public CallRatingView()
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
