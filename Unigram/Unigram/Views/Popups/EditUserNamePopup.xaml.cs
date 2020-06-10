using System.Threading.Tasks;
using Unigram.Common;
using Unigram.Controls;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Unigram.Views.Popups
{
    public sealed partial class EditUserNamePopup : ContentPopup
    {
        public EditUserNamePopup()
        {
            InitializeComponent();

            Title = Strings.Resources.EditName;
            PrimaryButtonText = Strings.Resources.OK;
            SecondaryButtonText = Strings.Resources.Cancel;
        }

        public EditUserNamePopup(string firstName, string lastName, bool showShare = false)
        {
            InitializeComponent();

            FirstName = firstName;
            LastName = lastName;

            SharePhoneCheck.Content = string.Format(Strings.Resources.SharePhoneNumberWith, firstName);
            SharePhoneCheck.Visibility = showShare ? Visibility.Visible : Visibility.Collapsed;

            Title = Strings.Resources.EditName;
            PrimaryButtonText = Strings.Resources.OK;
            SecondaryButtonText = Strings.Resources.Cancel;
        }

        public string FirstName
        {
            get
            {
                if (string.IsNullOrWhiteSpace(TextFirstName.Text))
                {
                    return TextLastName.Text ?? string.Empty;
                }

                return TextFirstName.Text ?? string.Empty;
            }
            private set
            {
                TextFirstName.Text = value ?? string.Empty;
            }
        }

        public string LastName
        {
            get
            {
                if (string.IsNullOrWhiteSpace(TextFirstName.Text))
                {
                    return string.Empty;
                }

                return TextLastName.Text ?? string.Empty;
            }
            private set
            {
                TextLastName.Text = value ?? string.Empty;
            }
        }

        public bool SharePhoneNumber
        {
            get => SharePhoneCheck.IsChecked == true;
            set => SharePhoneCheck.IsChecked = value;
        }

        public Task<ContentDialogResult> ShowAsync(string firstName, string lastName, bool showShare = false)
        {
            FirstName = firstName;
            LastName = lastName;

            SharePhoneCheck.Content = string.Format(Strings.Resources.SharePhoneNumberWith, firstName);
            SharePhoneCheck.Visibility = showShare ? Visibility.Visible : Visibility.Collapsed;

            return this.ShowQueuedAsync();
        }

        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
        }

        private void ContentDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
        }

        private void OnTextChanged(object sender, TextChangedEventArgs e)
        {
            IsPrimaryButtonEnabled = !string.IsNullOrEmpty(FirstName);
        }
    }
}
