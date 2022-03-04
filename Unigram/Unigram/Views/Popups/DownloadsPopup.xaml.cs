using Unigram.Common;
using Unigram.Controls;
using Unigram.Controls.Cells;
using Unigram.Converters;
using Unigram.Navigation.Services;
using Unigram.ViewModels;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Input;

namespace Unigram.Views.Popups
{
    public sealed partial class DownloadsPopup : ContentPopup
    {
        public DownloadsViewModel ViewModel => DataContext as DownloadsViewModel;

        public DownloadsPopup(int sessionId, INavigationService navigationService)
        {
            InitializeComponent();
            DataContext = TLContainer.Current.Resolve<DownloadsViewModel>(sessionId);

            PrimaryButtonText = Strings.Resources.Close;

            ViewModel.Dispatcher = navigationService.Dispatcher;
            ViewModel.NavigationService = navigationService;
            ViewModel.Hide = Hide;
        }

        private void OnOpened(ContentDialog sender, ContentDialogOpenedEventArgs args)
        {
            _ = ViewModel.OnNavigatedToAsync(null, default, null);
        }

        private void OnClosed(ContentDialog sender, ContentDialogClosedEventArgs args)
        {
            _ = ViewModel.OnNavigatedFromAsync(null, false);
        }

        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
        }

        private void ContentDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
        }

        private void Menu_ContextRequested(object sender, RoutedEventArgs e)
        {
            var viewModel = ViewModel;
            if (viewModel == null)
            {
                return;
            }

            var flyout = new MenuFlyout();

            if (viewModel.TotalActiveCount > 0)
            {
                flyout.CreateFlyoutItem(ViewModel.ToggleAllPausedCommand, Strings.Resources.PauseAll, new FontIcon { Glyph = Icons.Pause });
            }
            else if (viewModel.TotalPausedCount > 0)
            {
                flyout.CreateFlyoutItem(ViewModel.ToggleAllPausedCommand, Strings.Resources.ResumeAll, new FontIcon { Glyph = Icons.Play });
            }

            flyout.CreateFlyoutItem(ViewModel.SettingsCommand, Strings.Resources.Settings, new FontIcon { Glyph = Icons.Settings });

            if (viewModel.Items.Count > 0)
            {
                flyout.CreateFlyoutItem(ViewModel.RemoveAllCommand, Strings.Resources.DeleteAll, new FontIcon { Glyph = Icons.Delete });
            }

            flyout.ShowAt(sender as FrameworkElement, new FlyoutShowOptions { Placement = FlyoutPlacementMode.BottomEdgeAlignedRight });
        }

        private void OnChoosingItemContainer(ListViewBase sender, ChoosingItemContainerEventArgs args)
        {
            if (args.ItemContainer == null)
            {
                args.ItemContainer = new ListViewItem();
                args.ItemContainer.Style = sender.ItemContainerStyle;
                args.ItemContainer.ContentTemplate = sender.ItemTemplate;
                args.ItemContainer.ContextRequested += OnContextRequested;
            }

            args.IsContainerPrepared = true;
        }

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }

            var content = args.ItemContainer.ContentTemplateRoot as FileDownloadCell;
            var file = args.Item as FileDownloadViewModel;

            content.UpdateFileDownload(ViewModel, file);
            args.ItemContainer.Tag = file;
        }

        private void OnContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
            var element = sender as FrameworkElement;
            var fileDownload = element.Tag as FileDownloadViewModel;

            var flyout = new MenuFlyout();

            if (fileDownload.CompleteDate == 0)
            {
                flyout.CreateFlyoutItem(ViewModel.RemoveFileDownloadCommand, fileDownload, Strings.Resources.AccActionCancelDownload, new FontIcon { Glyph = Icons.Dismiss });
                flyout.CreateFlyoutItem(ViewModel.ViewFileDownloadCommand, fileDownload, Strings.Resources.ViewInChat, new FontIcon { Glyph = Icons.Comment });
            }
            else
            {
                flyout.CreateFlyoutItem(ViewModel.ViewFileDownloadCommand, fileDownload, Strings.Resources.ViewInChat, new FontIcon { Glyph = Icons.Comment });
                flyout.CreateFlyoutItem(ViewModel.ShowFileDownloadCommand, fileDownload, Strings.Resources.lng_context_show_in_folder, new FontIcon { Glyph = Icons.FolderOpen });

                flyout.CreateFlyoutItem(ViewModel.RemoveFileDownloadCommand, fileDownload, Strings.Resources.DeleteFromRecent, new FontIcon { Glyph = Icons.Delete });

                flyout.CreateFlyoutSeparator();
                flyout.CreateFlyoutItem(_ => { }, fileDownload, Strings.Resources.lng_context_select_msg, new FontIcon { Glyph = Icons.CheckmarkCircle });
            }

            args.ShowAt(flyout, sender as FrameworkElement);
        }
    }
}
