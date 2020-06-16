using Telegram.Td.Api;
using Unigram.Common;
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

            var proxy = List.ItemFromContainer(element) as ProxyViewModel;
            if (proxy == null)
            {
                return;
            }

            if (proxy.Type is ProxyTypeMtproto || proxy.Type is ProxyTypeSocks5)
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
                args.ItemContainer = new ListViewItem();
                args.ItemContainer.Style = sender.ItemContainerStyle;
                args.ItemContainer.ContextRequested += Proxy_ContextRequested;
            }

            args.ItemContainer.ContentTemplate = sender.ItemTemplateSelector.SelectTemplate(args.Item, args.ItemContainer);
            args.IsContainerPrepared = true;
        }

        #endregion

    }
}
