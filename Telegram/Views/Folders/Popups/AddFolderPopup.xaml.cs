using System.Linq;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Controls.Cells;
using Telegram.Td.Api;
using Telegram.ViewModels.Folders;
using Windows.UI.Xaml.Controls;

namespace Telegram.Views.Folders.Popups
{
    public sealed partial class AddFolderPopup : ContentPopup
    {
        public AddFolderViewModel ViewModel => DataContext as AddFolderViewModel;

        private readonly TaskCompletionSource<object> _task;

        public AddFolderPopup(TaskCompletionSource<object> task)
        {
            InitializeComponent();

            _task = task;

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
                args.ItemContainer.IsEnabled = ViewModel.CanBeAdded(chat);

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

            return Locale.Declension(Strings.R.FolderLinkHeaderChatsJoin, count);
        }

        private string ConvertSelectAll(int count)
        {
            return count >= ViewModel.TotalCount ? Strings.DeselectAll : Strings.SelectAll;
        }

        #endregion


    }
}
