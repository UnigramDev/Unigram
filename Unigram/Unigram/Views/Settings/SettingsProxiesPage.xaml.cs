//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Converters;
using Unigram.ViewModels.Settings;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;

namespace Unigram.Views.Settings
{
    public sealed partial class SettingsProxiesPage : HostedPage
    {
        public SettingsProxiesViewModel ViewModel => DataContext as SettingsProxiesViewModel;

        public SettingsProxiesPage()
        {
            InitializeComponent();
            Title = Strings.Resources.ProxySettings;
        }

        private void List_ItemClick(object sender, ItemClickEventArgs e)
        {
            ViewModel.EnableCommand.Execute(e.ClickedItem);
        }

        #region Context menu

        private void Proxy_ContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
            var flyout = new MenuFlyout();
            var element = sender as FrameworkElement;

            var proxy = List.ItemFromContainer(element) as ProxyViewModel;
            if (proxy is null or SystemProxyViewModel)
            {
                return;
            }

            if (proxy.Type is ProxyTypeMtproto or ProxyTypeSocks5)
            {
                flyout.CreateFlyoutItem(ViewModel.ShareCommand, proxy, Strings.Resources.ShareFile, new FontIcon { Glyph = Icons.Share });
            }

            flyout.CreateFlyoutItem(ViewModel.EditCommand, proxy, Strings.Resources.Edit, new FontIcon { Glyph = Icons.Edit });
            flyout.CreateFlyoutItem(ViewModel.RemoveCommand, proxy, Strings.Resources.Delete, new FontIcon { Glyph = Icons.Delete });

            args.ShowAt(flyout, element);
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
