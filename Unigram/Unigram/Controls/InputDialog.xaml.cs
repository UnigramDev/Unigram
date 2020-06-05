using LinqToVisualTree;
using System.Linq;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation.Peers;
using Windows.UI.Xaml.Automation.Provider;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;

// The Content Dialog item template is documented at http://go.microsoft.com/fwlink/?LinkId=234238

namespace Unigram.Controls
{
    public sealed partial class InputDialog : ContentPopup
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
            if (string.IsNullOrEmpty(Label.Text))
            {
                args.Cancel = true;
                return;
            }

            Text = Label.Text;
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

            var peer = ButtonAutomationPeer.CreatePeerForElement(button as Button);
            var pattern = peer.GetPattern(PatternInterface.Invoke) as IInvokeProvider;

            pattern.Invoke();
        }

        private void Label_TextChanged(object sender, TextChangedEventArgs e)
        {
            IsPrimaryButtonEnabled = !string.IsNullOrEmpty(Label.Text);
        }
    }
}
