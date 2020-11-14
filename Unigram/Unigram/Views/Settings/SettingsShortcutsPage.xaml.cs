using Microsoft.UI.Xaml.Controls;
using Unigram.Controls;
using Unigram.Services;
using Unigram.ViewModels.Settings;

namespace Unigram.Views.Settings
{
    public sealed partial class SettingsShortcutsPage : HostedPage
    {
        public SettingsShortcutsViewModel ViewModel => DataContext as SettingsShortcutsViewModel;

        public SettingsShortcutsPage()
        {
            InitializeComponent();
            DataContext = TLContainer.Current.Resolve<SettingsShortcutsViewModel>();
        }

        private void OnElementPrepared(ItemsRepeater sender, ItemsRepeaterElementPreparedEventArgs args)
        {
            var button = args.Element as BadgeButton;
            var info = sender.ItemsSourceView.GetAt(args.Index) as ShortcutInfo;
            //var info = button.DataContext as ShortcutInfo;

            button.Content = info.Command;
            //button.Badge = info.Shortcut;
            button.Command = ViewModel.EditCommand;
            button.CommandParameter = info;
        }
    }
}
