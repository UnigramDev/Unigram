//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
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
            DataContext = TLContainer.Current.Resolve<SettingsThemeViewModel>();

#if !DEBUG
            Microsoft.AppCenter.Analytics.Analytics.TrackEvent("SettingsThemePage");
#endif
        }

        public void Load(ThemeCustomInfo theme)
        {
            ViewModel.Initialize(theme);
        }

        private void Done_Click(object sender, RoutedEventArgs e)
        {
            if (Window.Current.Content is Host.RootPage root)
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
