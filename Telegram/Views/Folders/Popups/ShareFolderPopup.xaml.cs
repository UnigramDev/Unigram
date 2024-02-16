//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System.Linq;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Controls.Cells;
using Telegram.Td.Api;
using Telegram.ViewModels.Folders;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Telegram.Views.Folders.Popups
{
    public sealed partial class ShareFolderPopup : ContentPopup
    {
        public ShareFolderViewModel ViewModel => DataContext as ShareFolderViewModel;

        private readonly TaskCompletionSource<object> _task;

        public ShareFolderPopup(TaskCompletionSource<object> task)
        {
            InitializeComponent();

            _task = task;

            Title = Strings.FilterShare;

            PrimaryButtonText = Strings.Save;
            SecondaryButtonText = Strings.Cancel;
        }

        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            _task.SetResult(ViewModel.SelectedItems.Select(x => x.Id).ToList());
        }

        private void ContentDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
        }

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
                args.ItemContainer.IsEnabled = ViewModel.CanBeShared(chat);

                content.UpdateState(args.ItemContainer.IsSelected, false, true);
                content.UpdateSharedChat(ViewModel.ClientService, args, OnContainerContentChanging);
            }
        }

        #endregion

        #region Binding

        private bool ConvertInviteLinkLoad(string link)
        {
            return !string.IsNullOrEmpty(link);
        }

        private string ConvertHeadline(string title, int count)
        {
            if (count == 0)
            {
                return Strings.FilterInviteHeaderNo;
            }

            return Locale.Declension(Strings.R.FilterInviteHeader, count, title);
        }

        private string ConvertSelected(int count, string link)
        {
            if (string.IsNullOrEmpty(link))
            {
                return Strings.FilterInviteHeaderChatsNo;
            }
            else if (count == 0)
            {
                return Strings.FilterInviteHeaderChatsEmpty;
            }

            return Locale.Declension(Strings.R.FilterInviteHeaderChats, count);
        }

        private string ConvertSelectAll(int count)
        {
            return count >= ViewModel.TotalCount ? Strings.DeselectAll : Strings.SelectAll;
        }

        #endregion

        private void Share_Click(object sender, RoutedEventArgs e)
        {
            Hide();
            ViewModel.Share();
        }
    }
}
