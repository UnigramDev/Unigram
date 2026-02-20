//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Telegram.Controls;
using Telegram.Controls.Cells;
using Telegram.Services;
using Telegram.Streams;
using Telegram.Td.Api;
using Telegram.ViewModels.Settings;
using Telegram.Views.Profile;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Telegram.Views.Settings
{
    public sealed partial class SettingsProfileColorNameTabPage : ProfileTabPage
    {
        public new SettingsProfileColorNameViewModel ViewModel => DataContext as SettingsProfileColorNameViewModel;

        public SettingsProfileColorNameTabPage()
        {
            InitializeComponent();
        }

        private new void OnChoosingItemContainer(ListViewBase sender, ChoosingItemContainerEventArgs args)
        {
            if (args.ItemContainer == null)
            {
                args.ItemContainer = new GridViewItem();
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
            else if (args.ItemContainer.ContentTemplateRoot is ReceivedGiftCell content)
            {
                if (args.Item is UpgradedGift upgraded)
                {
                    content.UpdateGift(ViewModel.ClientService, new SentGiftUpgraded(upgraded));
                }
                else if (args.Item is GiftForResale giftForResale)
                {
                    content.UpdateGift(ViewModel.ClientService, giftForResale);
                }
            }

            args.Handled = true;
        }

        public void Initialize(IClientService clientService, MessageSender messageSender)
        {
            View.Initialize(clientService, messageSender);
        }

        private void Navigation_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }
            else if (args.ItemContainer.ContentTemplateRoot is Grid content && args.Item is SettingsProfileColorTabViewModelBase.AvailableGiftsCollection collection)
            {
                var animated = content.Children[0] as CustomEmojiIcon;
                var textBlock = content.Children[1] as TextBlock;

                if (collection.Gift != null)
                {
                    animated.Visibility = Visibility.Visible;
                    animated.Source = DelayedFileSource.FromSticker(ViewModel.ClientService, collection.Gift.Gift.Sticker);
                    textBlock.Text = collection.Gift.Title;
                }
                else
                {
                    animated.Visibility = Visibility.Collapsed;
                    animated.Source = null;
                    textBlock.Text = Strings.Gift2TabMine;
                }
            }
        }
    }
}
