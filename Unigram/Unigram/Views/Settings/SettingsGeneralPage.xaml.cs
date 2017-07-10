using Unigram.ViewModels.Settings;
using Windows.UI.Xaml.Controls;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace Unigram.Views.Settings
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class SettingsGeneralPage : Page
    {
        public SettingsGeneralViewModel ViewModel => DataContext as SettingsGeneralViewModel;

        public SettingsGeneralPage()
        {
            InitializeComponent();
            DataContext = UnigramContainer.Current.ResolveType<SettingsGeneralViewModel>();
        }
    }
}
