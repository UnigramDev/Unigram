//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Telegram.Common;
using Telegram.Controls.Cells;
using Telegram.ViewModels;
using Windows.UI.Xaml.Automation;
using Windows.UI.Xaml.Controls;

namespace Telegram.Views.Chats
{
    public sealed partial class ChatSharedFilesPage : ChatSharedMediaPageBase
    {
        public ChatSharedFilesPage()
        {
            InitializeComponent();
        }

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }

            args.ItemContainer.Tag = args.Item;

            var message = args.Item as MessageWithOwner;
            if (message == null)
            {
                return;
            }

            AutomationProperties.SetName(args.ItemContainer,
                Automation.GetSummary(message, true));

            if (args.ItemContainer.ContentTemplateRoot is SharedFileCell fileCell)
            {
                fileCell.UpdateMessage(ViewModel.MessageDelegate, message);
                fileCell.Tag = message;
            }
        }

        private void List_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }

    }
}
