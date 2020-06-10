using Telegram.Td.Api;
using Unigram.Controls.Cells;
using Unigram.ViewModels.Settings;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Unigram.Views.Settings
{
    public sealed partial class SettingsWebSessionsPage : HostedPage
    {
        public SettingsWebSessionsViewModel ViewModel => DataContext as SettingsWebSessionsViewModel;

        public SettingsWebSessionsPage()
        {
            InitializeComponent();
            DataContext = TLContainer.Current.Resolve<SettingsWebSessionsViewModel>();
        }

        private void ListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            ViewModel.TerminateCommand.Execute(e.ClickedItem);
        }

        private void TerminateOthers_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.TerminateOthersCommand.Execute(null);
        }

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }

            if (args.ItemContainer.ContentTemplateRoot is WebSessionCell cell)
            {
                cell.UpdateConnectedWebsite(ViewModel.ProtoService, args.Item as ConnectedWebsite);
            }
        }
    }
}
