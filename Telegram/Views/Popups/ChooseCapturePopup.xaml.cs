using Rg.DiffUtils;
using Telegram.Controls;
using Telegram.Controls.Cells;
using Telegram.Navigation;
using Telegram.Services;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Telegram.Views.Popups
{
    public sealed partial class ChooseCapturePopup : ContentPopup
    {
        private readonly DiffObservableCollection<CaptureSessionItem> _items;

        public ChooseCapturePopup(bool canShareAudio)
        {
            InitializeComponent();

            _items = new DiffObservableCollection<CaptureSessionItem>(new CaptureSessionItemDiffHandler(), new DiffOptions { AllowBatching = false, DetectMoves = false });
            _items.ReplaceDiff(CaptureSessionService.FindAll());

            ScrollingHost.ItemsSource = _items;
            ScrollingHost.SelectedIndex = 0;

            ShareSystemAudio.Visibility = canShareAudio
                ? Visibility.Visible
                : Visibility.Collapsed;

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        class CaptureSessionItemDiffHandler : IDiffHandler<CaptureSessionItem>
        {
            public bool CompareItems(CaptureSessionItem oldItem, CaptureSessionItem newItem)
            {
                if (oldItem is WindowCaptureSessionItem oldWindow && newItem is WindowCaptureSessionItem newWindow)
                {
                    return oldWindow.WindowId.Value == newWindow.WindowId.Value;
                }
                else if (oldItem is DisplayCaptureSessionItem oldDisplay && newItem is DisplayCaptureSessionItem newDisplay)
                {
                    return oldDisplay.DisplayId.Value == newDisplay.DisplayId.Value;
                }

                return false;
            }

            public void UpdateItem(CaptureSessionItem oldItem, CaptureSessionItem newItem)
            {

            }
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            WindowContext.ForXamlRoot(XamlRoot).Activated += OnActivated;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            WindowContext.ForXamlRoot(XamlRoot).Activated -= OnActivated;
        }

        private void OnActivated(object sender, WindowActivatedEventArgs e)
        {
            if (e.WindowActivationState != CoreWindowActivationState.Deactivated)
            {
                _items.ReplaceDiff(CaptureSessionService.FindAll());
            }
        }

        public CaptureSessionItem SelectedItem { get; private set; }

        public bool IsAudioCaptureEnabled { get; private set; }

        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
        }

        private void ContentDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
        }

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.ItemContainer.ContentTemplateRoot is CaptureSessionItemCell content)
            {
                if (args.InRecycleQueue)
                {
                    content.UpdateCell(null);
                }
                else if (args.Item is CaptureSessionItem item)
                {
                    content.UpdateCell(item);
                }
            }
        }

        private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Share.IsEnabled = ScrollingHost.SelectedItem is CaptureSessionItem;
        }

        private void Share_Click(object sender, RoutedEventArgs e)
        {
            SelectedItem = ScrollingHost.SelectedItem as CaptureSessionItem;
            IsAudioCaptureEnabled = ShareSystemAudio.IsChecked == true;

            Hide(ContentDialogResult.Primary);
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Hide(ContentDialogResult.Secondary);
        }
    }
}
