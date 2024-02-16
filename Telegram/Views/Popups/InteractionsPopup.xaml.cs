//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Telegram.Controls;
using Telegram.Controls.Cells;
using Telegram.Streams;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Windows.UI.Xaml.Controls;

namespace Telegram.Views.Popups
{
    public sealed partial class InteractionsPopup : ContentPopup
    {
        public InteractionsViewModel ViewModel => DataContext as InteractionsViewModel;

        public InteractionsPopup()
        {
            InitializeComponent();

            //Title = Strings.Reactions;
            SecondaryButtonText = Strings.Close;
        }

        // 446.667,
        //  48.6667

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }
            else if (args.ItemContainer.ContentTemplateRoot is Grid content)
            {
                var cell = content.Children[0] as ProfileCell;
                var animated = content.Children[1] as CustomEmojiIcon;

                if (args.Item is AddedReaction addedReaction)
                {
                    cell.UpdateAddedReaction(ViewModel.ClientService, args, OnContainerContentChanging);
                    animated.Source = new ReactionFileSource(ViewModel.ClientService, addedReaction.Type);
                }
                else if (args.Item is MessageViewer messageViewer)
                {
                    cell.UpdateMessageViewer(ViewModel.ClientService, args, OnContainerContentChanging);
                    animated.Source = null;
                }

                args.Handled = true;
            }
        }

        private void OnItemClick(object sender, ItemClickEventArgs e)
        {
            ViewModel.OpenChat(e.ClickedItem);
            Hide();
        }
    }
}
