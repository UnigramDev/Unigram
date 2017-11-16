using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Unigram.Common;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The Content Dialog item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace Unigram.Controls.Views
{
    public sealed partial class EditUserNameView : ContentDialog
    {
        private EditUserNameView()
        {
            InitializeComponent();

            Title = Strings.Android.EditName;
            PrimaryButtonText = Strings.Android.OK;
            SecondaryButtonText = Strings.Android.Cancel;
        }

        public string FirstName
        {
            get
            {
                if (string.IsNullOrWhiteSpace(TextFirstName.Text))
                {
                    return TextLastName.Text;
                }

                return TextFirstName.Text;
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
                    return null;
                }

                return TextLastName.Text;
            }
            private set
            {
                TextLastName.Text = value ?? string.Empty;
            }
        }

        private static EditUserNameView _current;
        public static EditUserNameView Current
        {
            get
            {
                if (_current == null)
                    _current = new EditUserNameView();

                return _current;
            }
        }

        public Task<ContentDialogResult> ShowAsync(string firstName, string lastName)
        {
            FirstName = firstName;
            LastName = lastName;

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
