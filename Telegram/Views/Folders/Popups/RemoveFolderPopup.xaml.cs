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
using Telegram.Controls.Cells;
using Telegram.Td.Api;
using Telegram.ViewModels.Folders;
using Windows.UI.Xaml.Controls;

namespace Telegram.Views.Folders.Popups
{
    public sealed partial class RemoveFolderPopup : ContentPopup
    {
        public RemoveFolderViewModel ViewModel => DataContext as RemoveFolderViewModel;

        public RemoveFolderPopup()
        {
            InitializeComponent();

            Title = Strings.FolderLinkTitleRemove;
            SecondaryButtonText = Strings.Cancel;
        }

        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            SelectedItems = ViewModel.SelectedItems.Select(x => x.Id).ToList();
        }

        public List<long> SelectedItems { get; private set; } = new();

        #region Recycle

        private void OnChoosingItemContainer(ListViewBase sender, ChoosingItemContainerEventArgs args)
        {
            if (args.ItemContainer == null)
            {
                args.ItemContainer = new MultipleListViewItem(sender, false);
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
            else if (args.ItemContainer.ContentTemplateRoot is ChatShareCell content && args.Item is Chat chat)
            {
                content.UpdateState(args.ItemContainer.IsSelected, false, true);
                content.UpdateSharedChat(ViewModel.ClientService, args, OnContainerContentChanging);
            }
        }

        #endregion

        #region Binding

        private bool ConvertTotalCount(int count)
        {
            return count > 0;
        }

        private string ConvertSelected(int count)
        {
            if (count == 0)
            {
                return Strings.FilterInviteHeaderChatsEmpty;
            }

            return Locale.Declension(Strings.R.FolderLinkHeaderChatsQuit, count);
        }

        private string ConvertSelectAll(int count)
        {
            return count >= ViewModel.TotalCount ? Strings.DeselectAll : Strings.SelectAll;
        }

        #endregion

    }
}
