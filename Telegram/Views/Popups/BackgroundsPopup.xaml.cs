//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Controls.Chats;
using Telegram.Controls.Media;
using Telegram.Td.Api;
using Telegram.ViewModels.Settings;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;

namespace Telegram.Views.Popups
{
    public sealed partial class BackgroundsPopup : ContentPopup
    {
        public SettingsBackgroundsViewModel ViewModel => DataContext as SettingsBackgroundsViewModel;

        private readonly TaskCompletionSource<object> _task;
        private bool _ignoreClosing;

        public BackgroundsPopup(TaskCompletionSource<object> task)
        {
            InitializeComponent();

            _task = task;

            Title = Strings.ChatBackground;
            SecondaryButtonText = Strings.Cancel;
        }

        private void OnChoosingItemContainer(ListViewBase sender, ChoosingItemContainerEventArgs args)
        {
            if (args.ItemContainer == null)
            {
                args.ItemContainer = new GridViewItem();
                args.ItemContainer.Style = sender.ItemContainerStyle;
                args.ItemContainer.ContentTemplate = sender.ItemTemplate;
                args.ItemContainer.ContextRequested += OnContextRequested;
            }

            args.IsContainerPrepared = true;
        }

        private void OnContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
            var element = sender as FrameworkElement;
            var background = ScrollingHost.ItemFromContainer(sender) as Background;

            if (background == null || background.Id == Constants.WallpaperLocalId)
            {
                return;
            }

            var flyout = new MenuFlyout();

            flyout.CreateFlyoutItem(ViewModel.Share, background, Strings.ShareFile, Icons.Share);
            flyout.CreateFlyoutItem(ViewModel.Delete, background, Strings.Delete, Icons.Delete, dangerous: true);

            args.ShowAt(flyout, element);
        }

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }

            var wallpaper = args.Item as Background;
            var root = args.ItemContainer.ContentTemplateRoot as Grid;

            var preview = root.Children[0] as ChatBackgroundPresenter;
            var check = root.Children[1];

            preview.UpdateSource(ViewModel.ClientService, wallpaper, true);
            check.Visibility = wallpaper == ViewModel.SelectedItem ? Visibility.Visible : Visibility.Collapsed;
        }

        private void List_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is Background background)
            {
                Change(ViewModel.ChangeAsync(background, false));
            }
        }

        private void ChangeToLocal_Click(object sender, RoutedEventArgs e)
        {
            Change(ViewModel.ChangeToLocalAsync(false));
        }

        private void ChangeToColor_Click(object sender, RoutedEventArgs e)
        {
            Change(ViewModel.ChangeToColorAsync(false));
        }

        private async void Change(Task<ContentDialogResult> task)
        {
            _ignoreClosing = true;
            Hide();

            _ignoreClosing = false;

            var confirm = await task;
            if (confirm != ContentDialogResult.Primary)
            {
                await this.ShowQueuedAsync();
            }
            else
            {
                _task.SetResult(true);
            }
        }

        private void OnClosing(ContentDialog sender, ContentDialogClosingEventArgs args)
        {
            if (_ignoreClosing)
            {
                return;
            }

            _task.TrySetResult(false);
        }
    }
}
