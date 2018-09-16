using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Windows.Input;
using Telegram.Td.Api;
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
                CreateFlyoutItem(ref flyout, ViewModel.ShareCommand, proxy, Strings.Resources.ShareFile);
            }

            CreateFlyoutItem(ref flyout, ViewModel.EditCommand, proxy, Strings.Resources.Edit);
            CreateFlyoutItem(ref flyout, ViewModel.RemoveCommand, proxy, Strings.Resources.Delete);

            if (flyout.Items.Count > 0 && args.TryGetPosition(sender, out Point point))
            {
                if (point.X < 0 || point.Y < 0)
                {
                    point = new Point(Math.Max(point.X, 0), Math.Max(point.Y, 0));
                }

                flyout.ShowAt(sender, point);
            }
        }

        private void CreateFlyoutItem(ref MenuFlyout flyout, ICommand command, object parameter, string text)
        {
            var flyoutItem = new MenuFlyoutItem();
            flyoutItem.IsEnabled = command != null;
            flyoutItem.Command = command;
            flyoutItem.CommandParameter = parameter;
            flyoutItem.Text = text;

            flyout.Items.Add(flyoutItem);
        }

        #endregion

    }
}
