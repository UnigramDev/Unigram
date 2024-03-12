using System;
using System.Globalization;
using System.Linq;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Controls.Cells.Business;
using Telegram.Controls.Media;
using Telegram.Td.Api;
using Telegram.ViewModels.Business;
using Telegram.ViewModels.Delegates;
using Windows.ApplicationModel.DataTransfer;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;

namespace Telegram.Views.Business
{
    public sealed partial class BusinessRepliesPage : HostedPage, IBusinessRepliesDelegate
    {
        public BusinessRepliesViewModel ViewModel => DataContext as BusinessRepliesViewModel;

        public BusinessRepliesPage()
        {
            InitializeComponent();
            Title = Strings.BusinessReplies;
        }

        #region Binding

        private ImageSource ConvertLocation(bool valid, Location location)
        {
            if (valid)
            {
                var latitude = location.Latitude.ToString(CultureInfo.InvariantCulture);
                var longitude = location.Longitude.ToString(CultureInfo.InvariantCulture);

                return new BitmapImage(new Uri(string.Format("https://dev.virtualearth.net/REST/v1/Imagery/Map/Road/{0},{1}/{2}?mapSize={3}&key=FgqXCsfOQmAn9NRf4YJ2~61a_LaBcS6soQpuLCjgo3g~Ah_T2wZTc8WqNe9a_yzjeoa5X00x4VJeeKH48wAO1zWJMtWg6qN-u4Zn9cmrOPcL", latitude, longitude, 15, "320,200")));
            }

            return null;
        }

        private Visibility ConvertClear(string address, bool valid)
        {
            return string.IsNullOrEmpty(address) && !valid
                ? Visibility.Collapsed
                : Visibility.Visible;
        }

        #endregion

        private void OnChoosingItemContainer(ListViewBase sender, ChoosingItemContainerEventArgs args)
        {
            if (args.ItemContainer == null)
            {
                args.ItemContainer = new TableListViewItem();
                args.ItemContainer.Style = sender.ItemContainerStyle;
                args.ItemContainer.ContentTemplate = sender.ItemTemplate;
                args.ItemContainer.ContextRequested += OnContextRequested;
            }

            args.IsContainerPrepared = true;
        }

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }
            else if (args.ItemContainer.ContentTemplateRoot is BusinessRepliesCell cell && args.Item is QuickReplyShortcut replies)
            {
                cell.UpdateContent(ViewModel.ClientService, replies, false);
            }
        }

        private void OnContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
            var replies = ScrollingHost.ItemFromContainer(sender) as QuickReplyShortcut;
            if (replies is null)
            {
                return;
            }

            var flyout = new MenuFlyout();

            flyout.CreateFlyoutItem(ViewModel.Rename, replies, Strings.Edit, Icons.Edit);
            flyout.CreateFlyoutItem(ViewModel.Delete, replies, Strings.Delete, Icons.Delete, destructive: true);

            flyout.ShowAt(sender, args);
        }

        private void OnDragItemsStarting(object sender, DragItemsStartingEventArgs e)
        {
            if (e.Items.Count > 1)
            {
                ScrollingHost.CanReorderItems = false;
                e.Cancel = true;
            }
            else
            {
                var items = ViewModel?.Items;
                if (items == null || items.Count < 2)
                {
                    ScrollingHost.CanReorderItems = false;
                    e.Cancel = true;
                }
                else
                {
                    ScrollingHost.CanReorderItems = true;
                }
            }
        }

        private void OnDragItemsCompleted(ListViewBase sender, DragItemsCompletedEventArgs args)
        {
            sender.CanReorderItems = false;

            if (args.DropResult == DataPackageOperation.Move && args.Items.Count == 1 && args.Items[0] is QuickReplyShortcut shortcut)
            {
                var shortcuts = ViewModel.Items
                    .Select(x => x.Id)
                    .ToArray();

                ViewModel.ClientService.Send(new ReorderQuickReplyShortcuts(shortcuts));
            }
        }

        private void OnItemClick(object sender, ItemClickEventArgs e)
        {
            ViewModel.Open(e.ClickedItem as QuickReplyShortcut);
        }

        public void UpdateQuickReplyShortcut(QuickReplyShortcut shortcut)
        {
            var container = ScrollingHost.ContainerFromItem(shortcut) as SelectorItem;
            var content = container?.ContentTemplateRoot as BusinessRepliesCell;

            content?.UpdateContent(ViewModel.ClientService, shortcut, false);
        }
    }
}
