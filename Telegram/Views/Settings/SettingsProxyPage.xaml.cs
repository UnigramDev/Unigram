//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Telegram.Common;
using Telegram.Controls;
using Telegram.Controls.Media;
using Telegram.Td.Api;
using Telegram.ViewModels.Settings;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;

namespace Telegram.Views.Settings
{
    public sealed partial class SettingsProxyPage : HostedPage
    {
        public SettingsProxyViewModel ViewModel => DataContext as SettingsProxyViewModel;

        public SettingsProxyPage()
        {
            InitializeComponent();
            Title = Strings.ProxySettings;
        }

        private void List_ItemClick(object sender, ItemClickEventArgs e)
        {
            ViewModel.Enable(e.ClickedItem as ConnectionViewModel);
        }

        #region Context menu

        private void Proxy_ContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
            var flyout = new MenuFlyout();
            var element = sender as FrameworkElement;

            var proxy = ScrollingHost.ItemFromContainer(element) as ProxyViewModel;
            if (proxy is null or SystemProxyViewModel)
            {
                return;
            }

            if (proxy.Type is ProxyTypeMtproto or ProxyTypeSocks5)
            {
                flyout.CreateFlyoutItem(ViewModel.Share, proxy, Strings.ShareFile, Icons.Share);
            }

            flyout.CreateFlyoutItem(ViewModel.Edit, proxy, Strings.Edit, Icons.Edit);
            flyout.CreateFlyoutItem(ViewModel.Remove, proxy, Strings.Delete, Icons.Delete, destructive: true);

            flyout.CreateFlyoutSeparator();

            flyout.CreateFlyoutItem(ViewModel.Select, proxy, Strings.Select, Icons.CheckmarkCircle);

            args.ShowAt(flyout, element);
        }

        #endregion

        #region Recycle

        private void OnChoosingItemContainer(ListViewBase sender, ChoosingItemContainerEventArgs args)
        {
            if (args.ItemContainer == null)
            {
                args.ItemContainer = new MultipleListViewItem(sender, false);
                args.ItemContainer.Style = sender.ItemContainerStyle;
                args.ItemContainer.ContentTemplate = sender.ItemTemplate;
                args.ItemContainer.ContextRequested += Proxy_ContextRequested;
            }

            args.IsContainerPrepared = true;
        }

        #endregion

    }
}
