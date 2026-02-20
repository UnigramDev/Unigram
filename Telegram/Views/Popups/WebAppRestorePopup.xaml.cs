//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System.Collections.Generic;
using System.Linq;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Converters;
using Telegram.Services;
using Windows.UI.Xaml.Controls;

namespace Telegram.Views.Popups
{
    public sealed partial class WebAppRestorePopup : ContentPopup
    {
        private readonly IClientService _clientService;

        public WebAppRestorePopup(IClientService clientService, User botUser, IList<WebAppStorageConfig> configs)
        {
            InitializeComponent();

            _clientService = clientService;

            Title = Strings.BotRestoreStorageTitle;
            TextBlockHelper.SetMarkdown(MessageLabel, string.Format(Strings.BotRestoreStorageText, botUser.FirstName));

            ScrollingHost.ItemsSource = configs;

            PurchaseCommand.IsEnabled = false;
        }

        public WebAppStorageConfig SelectedItem => ScrollingHost.SelectedItem as WebAppStorageConfig;

        private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            PurchaseCommand.IsEnabled = ScrollingHost.SelectedItems.Count > 0;

            if (ScrollingHost.SelectedItems.Count > 1 && e.AddedItems?.Count > 0)
            {
                foreach (var item in ScrollingHost.SelectedItems.ToList())
                {
                    if (item != e.AddedItems[0])
                    {
                        ScrollingHost.SelectedItems.Remove(item);
                    }
                }
            }
        }

        #region Recycle

        private void OnChoosingItemContainer(ListViewBase sender, ChoosingItemContainerEventArgs args)
        {
            if (args.ItemContainer == null)
            {
                args.ItemContainer = new TextListViewItem();
                args.ItemContainer.Style = sender.ItemContainerStyle;
                args.ItemContainer.ContentTemplate = sender.ItemTemplate;
            }

            args.IsContainerPrepared = true;
        }

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }
            else if (args.ItemContainer.ContentTemplateRoot is Grid content && args.Item is WebAppStorageConfig config)
            {
                var title = content.Children[0] as TextBlock;
                var subtitle = content.Children[1] as TextBlock;

                title.Text = config.UserName;
                subtitle.Text = string.Format(Strings.BotRestoreStorageCreatedAt, Formatter.DateAt((int)config.CreatedAt));
            }
        }

        #endregion

        private void Purchase_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            Hide(ContentDialogResult.Primary);
        }
    }
}
