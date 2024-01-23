//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Telegram.Common;
using Telegram.Controls;
using Telegram.Controls.Cells;
using Telegram.Td.Api;
using Windows.UI.Xaml.Controls;

namespace Telegram.Views.Profile
{
    public sealed partial class ProfileChannelsTabPage : ProfileTabPage
    {
        public ProfileChannelsTabPage()
        {
            InitializeComponent();
        }

        private void ListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is Chat chat)
            {
                ViewModel.OpenSimilarChat(chat);
            }
        }

        protected override void OnChoosingItemContainer(ListViewBase sender, ChoosingItemContainerEventArgs args)
        {
            if (args.ItemContainer == null)
            {
                args.ItemContainer = new TableListViewItem();
                args.ItemContainer.Style = ScrollingHost.ItemContainerStyle;
                args.ItemContainer.ContentTemplate = ScrollingHost.ItemTemplate;
            }

            args.IsContainerPrepared = true;
        }

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }
            else if (args.ItemContainer.ContentTemplateRoot is ProfileCell content)
            {
                content.UpdateSimilarChannel(ViewModel.ClientService, args, OnContainerContentChanging);
            }
        }

        private FormattedText ConvertMoreSimilar(int totalCount)
        {
            var text = string.Format(Strings.MoreSimilarText, "**100**");
            return Extensions.ReplacePremiumLink(text, new PremiumFeatureIncreasedLimits());
        }
    }
}
