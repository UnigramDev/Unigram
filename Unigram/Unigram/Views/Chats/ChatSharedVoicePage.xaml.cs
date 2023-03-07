//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Unigram.Common;
using Unigram.Controls.Cells;
using Unigram.ViewModels;

namespace Unigram.Views.Chats
{
    public sealed partial class ChatSharedVoicePage : ChatSharedMediaPageBase
    {
        public ChatSharedVoicePage()
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

            if (args.ItemContainer.ContentTemplateRoot is SharedVoiceCell voiceCell)
            {
                voiceCell.UpdateMessage(ViewModel.PlaybackService, message);
                voiceCell.Tag = message;
            }
        }

        private void List_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }
    }
}
