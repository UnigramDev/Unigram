using Unigram.Services;
using Unigram.ViewModels.Settings;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Unigram.Views.Settings
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
            ViewModel.EditBrushCommand.Execute(e.ClickedItem);
        }
    }
}
