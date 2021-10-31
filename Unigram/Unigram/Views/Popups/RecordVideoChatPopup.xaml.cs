using Unigram.Controls;
using Windows.UI.Xaml.Controls;

namespace Unigram.Views.Popups
{
    public sealed partial class RecordVideoChatPopup : ContentPopup
    {
        public RecordVideoChatPopup(string title)
        {
            InitializeComponent();

            Title = Strings.Resources.VoipGroupStartRecordingTitle;
            MessageLabel.Text = Strings.Resources.VoipGroupStartRecordingText;
            PrimaryButtonText = Strings.Resources.Start;
            SecondaryButtonText = Strings.Resources.Cancel;
            Label.PlaceholderText = Strings.Resources.VoipGroupSaveFileHint;
            Label.Text = title;
        }

        public string FileName => Label.Text;

        public bool RecordVideo => RecordVideoCheck.IsChecked == true || UseLandscapeOrientation.IsChecked == true;

        public bool UsePortraitOrientation => RecordVideoCheck.IsChecked == true && UseLandscapeOrientation.IsChecked == false;

        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
        }

        private void ContentDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
        }

        private void Label_KeyDown(object sender, Windows.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            if (e.Key != Windows.System.VirtualKey.Enter)
            {
                return;
            }

            Hide(ContentDialogResult.Primary);
        }
    }
}
