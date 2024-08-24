//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Telegram.Services;
using Telegram.ViewModels.Settings;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Telegram.Views.Settings
{
    public sealed partial class SettingsThemePage : HostedPage
    {
        public SettingsThemeViewModel ViewModel => DataContext as SettingsThemeViewModel;

        public SettingsThemePage()
        {
            InitializeComponent();
            DataContext = TypeResolver.Current.Resolve<SettingsThemeViewModel>();

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
            if (XamlRoot.Content is Host.RootPage root)
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
