//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Controls.Cells;
using Telegram.Td;
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

        public override void OnNavigatedTo(object parameter)
        {
            UpdateSubtitle(ViewModel.Subtitle);

            ViewModel.PropertyChanged += OnPropertyChanged;
        }

        public override void OnNavigatedFrom()
        {
            ViewModel.PropertyChanged -= OnPropertyChanged;
        }

        private void OnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ViewModel.Subtitle))
            {
                UpdateSubtitle(ViewModel.Subtitle);
            }
        }

        private void UpdateSubtitle(ChatFolderName text)
        {
            var formatted = ClientEx.ParseMarkdown(text.Text);
            var name = new ChatFolderName(formatted, text.AnimateCustomEmoji);

            CustomEmojiIcon.Add(SubtitleText, SubtitleParagraph.Inlines, ViewModel.ClientService, name);
        }

        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            _task.SetResult(ViewModel.SelectedItems.Select(x => x.Id).ToList());
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

                args.Handled = true;
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
