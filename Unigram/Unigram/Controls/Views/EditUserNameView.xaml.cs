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

namespace Unigram.Controls.Views
{
    public sealed partial class EditUserNameView : TLContentDialog
    {
        public EditUserNameView()
        {
            InitializeComponent();

            Title = Strings.Resources.EditName;
            PrimaryButtonText = Strings.Resources.OK;
            SecondaryButtonText = Strings.Resources.Cancel;
        }

        public EditUserNameView(string firstName, string lastName)
        {
            InitializeComponent();

            FirstName = firstName;
            LastName = lastName;

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
