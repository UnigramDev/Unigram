using Unigram.Controls;
using Windows.UI.Xaml.Controls;

namespace Unigram.Views.Popups
{
    public sealed partial class EditYourAboutPopup : ContentPopup
    {
        public EditYourAboutPopup(string bio)
        {
            this.InitializeComponent();

            FieldAbout.Text = bio;

            Title = Strings.Resources.UserBio;
            PrimaryButtonText = Strings.Resources.OK;
            SecondaryButtonText = Strings.Resources.Cancel;
        }

        public string About
        {
            get
            {
                return FieldAbout.Text;
            }
        }

        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
        }

        private void ContentDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
        }
    }
}
