//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System.Collections.Generic;
using System.Linq;
using Telegram.Controls;
using Telegram.Controls.Cells;
using Telegram.ViewModels.Chats;
using Telegram.ViewModels.Stories;
using Windows.UI.Xaml.Controls;

namespace Telegram.Views.Popups
{
    public sealed partial class ChooseStoriesPopup : ContentPopup
    {
        public ChooseStoriesPopup(ChatStoriesViewModel viewModel)
        {
            InitializeComponent();

            Title = Strings.StoriesAlbumMenuAddStories;
            PrimaryButtonText = Strings.StoriesAlbumMenuAddStories;
            SecondaryButtonText = Strings.Cancel;

            ScrollingHost.ItemsSource = viewModel.Items;
        }

        public IEnumerable<StoryViewModel> SelectedItems => ScrollingHost.SelectedItems
            .Cast<StoryViewModel>()
            .OrderByDescending(x => x.Date);

        private void OnChoosingItemContainer(ListViewBase sender, ChoosingItemContainerEventArgs args)
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
            else if (args.ItemContainer.ContentTemplateRoot is StoryCell cell && args.Item is StoryViewModel story)
            {
                cell.Update(story);
                args.Handled = true;
            }
        }
    }
}
