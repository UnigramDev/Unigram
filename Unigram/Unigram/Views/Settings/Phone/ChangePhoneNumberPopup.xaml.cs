using Unigram.Controls;

namespace Unigram.Views.Settings
{
    public sealed partial class ChangePhoneNumberPopup : ContentPopup
    {
        public ChangePhoneNumberPopup()
        {
            InitializeComponent();

            Title = Strings.Resources.ChangePhoneNumber;
            PrimaryButtonText = Strings.Resources.Change;
            SecondaryButtonText = Strings.Resources.Cancel;
        }
    }
}
