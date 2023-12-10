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

namespace Telegram.Views.Profile
{
    public sealed partial class ProfileMusicTabPage : ProfileTabPage
    {
        public ProfileMusicTabPage()
        {
            InitializeComponent();
        }

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }
            else if (args.ItemContainer.ContentTemplateRoot is SharedAudioCell audioCell && args.Item is MessageWithOwner message)
            {
                AutomationProperties.SetName(args.ItemContainer, Automation.GetSummary(message, true));

                audioCell.UpdateMessage(ViewModel.PlaybackService, message);
                args.Handled = true;
            }
        }
    }
}
