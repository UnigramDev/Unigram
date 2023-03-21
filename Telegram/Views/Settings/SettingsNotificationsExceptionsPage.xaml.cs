//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Telegram.Common;
using Telegram.Controls;
using Telegram.Controls.Cells;
using Telegram.Converters;
using Telegram.Td.Api;
using Telegram.ViewModels.Settings;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;

namespace Telegram.Views.Settings
{
    public sealed partial class SettingsNotificationsExceptionsPage : HostedPage
    {
        public SettingsNotificationsExceptionsViewModel ViewModel => DataContext as SettingsNotificationsExceptionsViewModel;

        public SettingsNotificationsExceptionsPage()
        {
            InitializeComponent();
        }

        private void ListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is Chat chat)
            {
                ViewModel.NavigationService.NavigateToChat(chat);
            }
        }

        #region Recycle

        private void OnChoosingItemContainer(ListViewBase sender, ChoosingItemContainerEventArgs args)
        {
            if (args.ItemContainer == null)
            {
                args.ItemContainer = new TableListViewItem();
                args.ItemContainer.Style = sender.ItemContainerStyle;
                args.ItemContainer.ContentTemplate = sender.ItemTemplate;
                args.ItemContainer.ContextRequested += Exception_ContextRequested;
            }

            args.IsContainerPrepared = true;
        }

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }
            else if (args.ItemContainer.ContentTemplateRoot is UserCell content)
            {
                content.UpdateNotificationException(ViewModel.ClientService, args, OnContainerContentChanging);
            }
        }

        #endregion

        #region Context menu

        private void Exception_ContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
            var flyout = new MenuFlyout();
            var element = sender as FrameworkElement;

            var exception = List.ItemFromContainer(element) as Chat;
            if (exception is null)
            {
                return;
            }

            flyout.CreateFlyoutItem(ViewModel.RemoveCommand, exception, Strings.Delete, new FontIcon { Glyph = Icons.Delete });

            args.ShowAt(flyout, element);
        }

        #endregion
    }
}
