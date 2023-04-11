using Telegram.Common;
using Telegram.Controls;
using Telegram.Controls.Cells;
using Telegram.Td.Api;
using Telegram.ViewModels.Folders;
using Windows.UI.Xaml.Controls;

namespace Telegram.Views.Folders
{
    public sealed partial class ShareFolderPage : HostedPage
    {
        public ShareFolderViewModel ViewModel => DataContext as ShareFolderViewModel;

        public ShareFolderPage()
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

    }
}
