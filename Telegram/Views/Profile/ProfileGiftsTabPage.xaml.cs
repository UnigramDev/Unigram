//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.UI.Xaml.Controls;
using Telegram.Controls.Cells;
using Telegram.Td.Api;

namespace Telegram.Views.Profile
{
    public sealed partial class ProfileGiftsTabPage : ProfileTabPage
    {
        public ProfileGiftsTabPage()
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
            else if (args.ItemContainer.ContentTemplateRoot is UserGiftCell content && args.Item is UserGift gift)
            {
                content.UpdateUserGift(ViewModel.ClientService, gift);
            }

            args.Handled = true;
        }

        private void OnItemClick(object sender, ItemClickEventArgs e)
        {
            ViewModel.GiftsTab.OpenGift(e.ClickedItem as UserGift);
        }
    }
}
