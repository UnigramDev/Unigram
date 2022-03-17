using Unigram.Controls;
using Windows.UI.Xaml.Controls;

namespace Unigram.Views.Settings.Popups
{
    public sealed partial class SettingsPasscodePopup : ContentPopup
    {
        public SettingsPasscodePopup()
        {
            this.InitializeComponent();

            Title = Strings.Resources.Passcode;
            PrimaryButtonText = Strings.Resources.Enable;
            SecondaryButtonText = Strings.Resources.Cancel;
        }

        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
        }

        private void ContentDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
        }
    }
}
