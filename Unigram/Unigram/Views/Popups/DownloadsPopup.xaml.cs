using Unigram.Common;
using Unigram.Controls;
using Unigram.Converters;
using Unigram.ViewModels;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;

namespace Unigram.Views.Popups
{
    public sealed partial class DownloadsPopup : ContentPopup
    {
        public DownloadsViewModel ViewModel => DataContext as DownloadsViewModel;

        public DownloadsPopup(int sessionId)
        {
            InitializeComponent();
            DataContext = TLContainer.Current.Resolve<DownloadsViewModel>(sessionId);
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

            flyout.CreateFlyoutItem(() => { }, Strings.Resources.PauseAll, new FontIcon { Glyph = Icons.Pause });
            flyout.CreateFlyoutItem(() => { }, Strings.Resources.Settings, new FontIcon { Glyph = Icons.Settings });
            flyout.CreateFlyoutItem(() => { }, Strings.Resources.DeleteAll, new FontIcon { Glyph = Icons.Delete });

            flyout.ShowAt(sender as FrameworkElement, new FlyoutShowOptions { Placement = FlyoutPlacementMode.BottomEdgeAlignedRight });
        }
    }
}
