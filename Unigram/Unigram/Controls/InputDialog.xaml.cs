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
    public sealed partial class InputDialog : ContentDialog
    {
        public string Header { get; set; }

        public string Text { get; set; } = string.Empty;

        public string PlaceholderText { get; set; } = string.Empty;

        public InputScopeNameValue InputScope { get; set; }

        public InputDialog()
        {
            InitializeComponent();

            Label.Loaded += OnLoaded;
            Opened += OnOpened;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            Label.Focus(FocusState.Keyboard);
        }

        private void OnOpened(ContentDialog sender, ContentDialogOpenedEventArgs args)
        {
            Label.Header = Header;
            Label.PlaceholderText = PlaceholderText;
            Label.Text = Text;

            InputScope scope = new InputScope();
            InputScopeName name = new InputScopeName();

            name.NameValue = InputScope;
            scope.Names.Add(name);

            Label.InputScope = scope;
        }

        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            Text = Label.Text;
        }

        private void ContentDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
        }
    }
}
