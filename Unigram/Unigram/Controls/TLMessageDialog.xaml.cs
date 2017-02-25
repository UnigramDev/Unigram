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

// The Content Dialog item template is documented at http://go.microsoft.com/fwlink/?LinkId=234238

namespace Unigram.Controls
{
    public sealed partial class TLMessageDialog : ContentDialog
    {
        public TLMessageDialog()
        {
            this.InitializeComponent();
        }

        public TLMessageDialog(string message)
            : this(message, null)
        {

        }

        public TLMessageDialog(string message, string title)
        {
            InitializeComponent();

            Message = message;
            Title = title;
            PrimaryButtonText = "OK";
        }

        public string Message
        {
            get
            {
                return MessageLabel.Text;
            }
            set
            {
                MessageLabel.Text = value;
            }
        }

        public string CheckBoxLabel
        {
            get
            {
                return CheckBox.Content.ToString();
            }
            set
            {
                CheckBox.Content = value;
                CheckBox.Visibility = string.IsNullOrWhiteSpace(value) ? Visibility.Collapsed : Visibility.Visible;
            }
        }

        public bool? IsChecked
        {
            get
            {
                return CheckBox.IsChecked;
            }
            set
            {
                CheckBox.IsChecked = value;
            }
        }

        public static IAsyncOperation<ContentDialogResult> ShowAsync(string message, string title = null, string primary = null, string secondary = null)
        {
            var dialog = new TLMessageDialog();
            dialog.Title = title;
            dialog.Message = message;
            dialog.PrimaryButtonText = primary ?? string.Empty;
            dialog.SecondaryButtonText = secondary ?? string.Empty;

            return dialog.ShowAsync();
        }
    }
}
