using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Windows.Input;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.ViewModels.Settings;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace Unigram.Views.Settings
{
    public sealed partial class SettingsProxiesPage : Page
    {
        public SettingsProxiesViewModel ViewModel => DataContext as SettingsProxiesViewModel;

        public SettingsProxiesPage()
        {
            InitializeComponent();
            DataContext = TLContainer.Current.Resolve<SettingsProxiesViewModel>();
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
            var proxy = element.DataContext as ProxyViewModel;

            if (proxy.Type is ProxyTypeMtproto || proxy.Type is ProxyTypeSocks5)
            {
                flyout.CreateFlyoutItem(ViewModel.ShareCommand, proxy, Strings.Resources.ShareFile);
            }

            flyout.CreateFlyoutItem(ViewModel.EditCommand, proxy, Strings.Resources.Edit);
            flyout.CreateFlyoutItem(ViewModel.RemoveCommand, proxy, Strings.Resources.Delete);

            args.ShowAt(flyout, element);
        }

        #endregion

    }
}
