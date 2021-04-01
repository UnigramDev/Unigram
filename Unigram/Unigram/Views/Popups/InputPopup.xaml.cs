using LinqToVisualTree;
using System.Linq;
using Unigram.Common;
using Unigram.Controls;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation.Peers;
using Windows.UI.Xaml.Automation.Provider;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;

namespace Unigram.Views.Popups
{
    public sealed partial class InputPopup : ContentPopup
    {
        public string Header { get; set; }

        public string Text { get; set; } = string.Empty;

        public string PlaceholderText { get; set; } = string.Empty;

        public int MaxLength { get; set; } = int.MaxValue;

        public InputScopeNameValue InputScope { get; set; }

        public bool CanBeEmpty { get; set; }

        public InputPopup(bool password = false)
        {
            InitializeComponent();

            if (password)
            {
                FindName(nameof(Password));
                Password.Loaded += OnLoaded;
            }
            else
            {
                FindName(nameof(Label));
                Label.Loaded += OnLoaded;
            }

            Opened += OnOpened;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            Label?.Focus(FocusState.Keyboard);
            Password?.Focus(FocusState.Keyboard);
        }

        private void OnOpened(ContentDialog sender, ContentDialogOpenedEventArgs args)
        {
            if (Label != null)
            {
                Label.Header = Header;
                Label.PlaceholderText = PlaceholderText;
                Label.Text = Text;
                Label.MaxLength = MaxLength;

                var scope = new InputScope();
                var name = new InputScopeName();

                name.NameValue = InputScope;
                scope.Names.Add(name);

                Label.InputScope = scope;
            }
            else if (Password != null)
            {
                Password.Header = Header;
                Password.PlaceholderText = PlaceholderText;
                Password.Password = Text;
                Password.MaxLength = MaxLength;
            }
        }

        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            if (Label != null)
            {
                if (string.IsNullOrEmpty(Label.Text) && !CanBeEmpty)
                {
                    VisualUtilities.ShakeView(Label);
                    args.Cancel = true;
                    return;
                }

                Text = Label.Text;
            }
            else if (Password != null)
            {
                if (string.IsNullOrEmpty(Password.Password) && !CanBeEmpty)
                {
                    VisualUtilities.ShakeView(Password);
                    args.Cancel = true;
                    return;
                }

                Text = Password.Password;
            }
        }

        private void ContentDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
        }

        private void Label_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key != Windows.System.VirtualKey.Enter)
            {
                return;
            }

            var button = this.Descendants<Button>().FirstOrDefault(x => x is Button btn && string.Equals(btn.Name, "PrimaryButton"));
            if (button == null)
            {
                return;
            }

            var peer = ButtonAutomationPeer.CreatePeerForElement(button);
            var pattern = peer.GetPattern(PatternInterface.Invoke) as IInvokeProvider;

            pattern.Invoke();
        }

        private void Label_TextChanged(object sender, TextChangedEventArgs e)
        {
            IsPrimaryButtonEnabled = CanBeEmpty || !string.IsNullOrEmpty(Label.Text);
        }

        private void Label_PasswordChanged(object sender, RoutedEventArgs e)
        {
            IsPrimaryButtonEnabled = CanBeEmpty || !string.IsNullOrEmpty(Password.Password);
        }
    }
}
