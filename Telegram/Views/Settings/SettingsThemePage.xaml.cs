//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Telegram.Navigation;
using Telegram.Services;
using Telegram.ViewModels.Settings;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Telegram.Views.Settings
{
    public sealed partial class SettingsThemePage : HostedPage
    {
        public SettingsThemeViewModel ViewModel => DataContext as SettingsThemeViewModel;

        public SettingsThemePage()
        {
            InitializeComponent();
            DataContext = LifetimeService.Current.ActiveItem.Resolve<SettingsThemeViewModel>();

            WatchDog.TrackEvent("SettingsThemePage");
        }

        public void Load(ThemeCustomInfo theme)
        {
            ViewModel.Initialize(theme);
        }

        private void Done_Click(object sender, RoutedEventArgs e)
        {
            if (XamlRoot.Content is WindowControl { Content: Host.RootPage root })
            {
                root.HideEditor();
            }
        }

        private void List_ItemClick(object sender, ItemClickEventArgs e)
        {
            ViewModel.EditBrush(e.ClickedItem as ThemeBrush);
        }
    }
}
