//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Telegram.Common;
using Telegram.Controls;
using Telegram.Controls.Cells;
using Telegram.Controls.Media;
using Telegram.Td.Api;
using Telegram.ViewModels.Delegates;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Input;

namespace Telegram.Views.Profile
{
    public sealed partial class ProfileSavedChatsTabPage : ProfileTabPage, ISavedMessagesChatsDelegate
    {
        public ProfileSavedChatsTabPage()
        {
            InitializeComponent();

            Connected += OnConnected;
            Disconnected += OnDisconnected;
        }

        private void OnConnected(object sender, RoutedEventArgs e)
        {
            if (ViewModel != null)
            {
                ViewModel.SavedChatsTab.Delegate = this;
            }
        }

        private void OnDisconnected(object sender, RoutedEventArgs e)
        {
            if (ViewModel != null)
            {
                ViewModel.SavedChatsTab.Delegate = null;
            }
        }

        private void ListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is SavedMessagesTopic topic)
            {
                ViewModel.OpenSavedMessagesTopic(topic);
            }
        }

        protected override void OnChoosingItemContainer(ListViewBase sender, ChoosingItemContainerEventArgs args)
        {
            if (args.ItemContainer == null)
            {
                args.ItemContainer = new TableListViewItem();
                args.ItemContainer.Style = ScrollingHost.ItemContainerStyle;
                args.ItemContainer.ContentTemplate = ScrollingHost.ItemTemplate;
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
            else if (args.ItemContainer.ContentTemplateRoot is Grid content && args.Item is SavedMessagesTopic savedMessagesTopic)
            {
                if (content.Children[0] is ChatCell cell)
                {
                    cell.UpdateSavedMessagesTopic(ViewModel.ClientService, savedMessagesTopic);
                }
            }
        }

        private void OnContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
            var topic = ScrollingHost.ItemFromContainer(sender) as SavedMessagesTopic;
            var flyout = new MenuFlyout();

            if (topic.IsPinned)
            {
                flyout.CreateFlyoutItem(ViewModel.SavedChatsTab.UnpinTopic, topic, Strings.UnpinFromTop, Icons.PinOff);
            }
            else
            {
                flyout.CreateFlyoutItem(ViewModel.SavedChatsTab.PinTopic, topic, Strings.PinToTop, Icons.Pin);
            }

            flyout.CreateFlyoutItem(ViewModel.SavedChatsTab.DeleteTopic, topic, Strings.Delete, Icons.Delete, destructive: true);

            flyout.ShowAt(sender, args);
        }

        public void UpdateSavedMessagesTopicLastMessage(SavedMessagesTopic topic)
        {
            this.BeginOnUIThread(() =>
            {
                var clientService = ViewModel?.ClientService;
                if (clientService == null)
                {
                    return;
                }

                var container = ScrollingHost.ContainerFromItem(topic) as SelectorItem;
                if (container?.ContentTemplateRoot is Grid content)
                {
                    if (content.Children[0] is ChatCell cell)
                    {
                        cell.UpdateSavedMessagesTopic(clientService, topic);
                    }
                }
            });
        }
    }
}
