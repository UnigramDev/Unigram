//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Telegram.Common;
using Telegram.Controls;
using Telegram.Controls.Media;
using Telegram.Navigation;
using Telegram.Td.Api;
using Telegram.ViewModels.Settings;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;

namespace Telegram.Views.Settings
{
    public sealed partial class SettingsProxyPage : HostedPage, INavigablePage
    {
        public SettingsProxyViewModel ViewModel => DataContext as SettingsProxyViewModel;

        public SettingsProxyPage()
        {
            InitializeComponent();
            Title = Strings.ProxySettings;
        }

        public void OnBackRequested(BackRequestedRoutedEventArgs args)
        {
            if (ViewModel.SelectedItems.Count > 0)
            {
                ViewModel.SelectedItems.Clear();
                args.Handled = true;
            }
        }

        private void List_ItemClick(object sender, ItemClickEventArgs e)
        {
            ViewModel.Enable(e.ClickedItem as ProxyViewModel);
        }

        #region Context menu

        private void Proxy_ContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
            var flyout = new MenuFlyout();

            if (ViewModel.SelectedItems.Count > 1)
            {
                flyout.CreateFlyoutItem(ViewModel.DeleteSelected, Strings.DeleteSelected, Icons.Delete, destructive: true);
            }
            else
            {
                var proxy = ScrollingHost.ItemFromContainer(sender) as ProxyViewModel;
                if (proxy is null)
                {
                    return;
                }

                if (proxy.Type is ProxyTypeMtproto or ProxyTypeSocks5)
                {
                    flyout.CreateFlyoutItem(ViewModel.Share, proxy, Strings.ShareFile, Icons.Share);
                }

                flyout.CreateFlyoutItem(ViewModel.Edit, proxy, Strings.Edit, Icons.Edit);
                flyout.CreateFlyoutItem(ViewModel.Delete, proxy, Strings.Delete, Icons.Delete, destructive: true);

                flyout.CreateFlyoutSeparator();

                flyout.CreateFlyoutItem(ViewModel.Select, proxy, Strings.Select, Icons.CheckmarkCircle);
            }

            flyout.ShowAt(sender, args);
        }

        #endregion

        #region Recycle

        private void OnChoosingItemContainer(ListViewBase sender, ChoosingItemContainerEventArgs args)
        {
            if (args.ItemContainer == null)
            {
                args.ItemContainer = new TableListViewItem();
                args.ItemContainer.Style = sender.ItemContainerStyle;
                args.ItemContainer.ContentTemplate = sender.ItemTemplate;
                args.ItemContainer.ContextRequested += Proxy_ContextRequested;
            }

            args.IsContainerPrepared = true;
        }

        #endregion

    }
}
