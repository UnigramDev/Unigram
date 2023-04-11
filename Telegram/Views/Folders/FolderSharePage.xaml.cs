using Telegram.Common;
using Telegram.Controls;
using Telegram.Controls.Cells;
using Telegram.Td.Api;
using Telegram.ViewModels.Folders;
using Windows.UI.Xaml.Controls;

namespace Telegram.Views.Folders
{
    public sealed partial class FolderSharePage : HostedPage
    {
        public FolderShareViewModel ViewModel => DataContext as FolderShareViewModel;

        public FolderSharePage()
        {
            InitializeComponent();
            Title = Strings.FilterShare;
        }

        #region Recycle

        private void OnChoosingItemContainer(ListViewBase sender, ChoosingItemContainerEventArgs args)
        {
            if (args.ItemContainer == null)
            {
                args.ItemContainer = new MultipleListViewItem(false);
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

                content.UpdateState(args.ItemContainer.IsSelected, false);
                content.UpdateSharedChat(ViewModel.ClientService, args, OnContainerContentChanging);
            }
        }

        #endregion

        #region Binding

        private string ConvertHeadline(string title, int count)
        {
            return Locale.Declension(Strings.R.FilterInviteHeader, count, title);
        }

        private string ConvertSelected(int count)
        {
            if (count == 0)
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

    }
}
