using Unigram.Controls;
using Unigram.Navigation;
using Unigram.Services;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

// The Content Dialog item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace Unigram.Views.Popups
{
    public sealed partial class EditShortcutPopup : ContentPopup
    {
        private readonly IShortcutsService _shortcutsService;

        public EditShortcutPopup(IShortcutsService shortcutsService, ShortcutInfo info)
        {
            InitializeComponent();

            _shortcutsService = shortcutsService;

            Title = info.Command;
            TextField.Text = info.Shortcut.ToString();

            PrimaryButtonText = Strings.Resources.OK;
            SecondaryButtonText = Strings.Resources.Cancel;
        }

        public Shortcut Shortcut { get; private set; }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            WindowContext.GetForCurrentView().AcceleratorKeyActivated += OnAcceleratorKeyActivated;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            WindowContext.GetForCurrentView().AcceleratorKeyActivated -= OnAcceleratorKeyActivated;
        }

        private void OnAcceleratorKeyActivated(CoreDispatcher sender, AcceleratorKeyEventArgs args)
        {
            if (TextField.FocusState != FocusState.Unfocused && _shortcutsService.TryGetShortcut(args, out Shortcut shortcut))
            {
                args.Handled = true;

                Shortcut = shortcut;
                TextField.Text = shortcut.ToString();
            }
        }

        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            args.Cancel = Shortcut == null;
        }

        private void ContentDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
        }

        private void TextField_Loaded(object sender, RoutedEventArgs e)
        {
            TextField.Focus(FocusState.Keyboard);
        }
    }
}
