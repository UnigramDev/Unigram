//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.UI.Xaml.Controls;
using Unigram.Controls;

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

        private void Label_KeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            if (e.Key != Windows.System.VirtualKey.Enter)
            {
                return;
            }

            Hide(ContentDialogResult.Primary);
        }
    }
}
