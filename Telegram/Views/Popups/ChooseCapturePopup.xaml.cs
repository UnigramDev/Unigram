//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Rg.DiffUtils;
using Telegram.Controls;
using Telegram.Controls.Cells;
using Telegram.Services;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation;
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

        // The list is only correct for as long as the user stays here: they usually leave to
        // arrange the window they want to share. Focus follows window activation, and unlike
        // WindowContext it needs no XamlRoot - which is null by the time the popup unloads, so
        // detaching from Activated there was itself a crash.
        private bool _lostFocus;

        protected override void OnLostFocus(RoutedEventArgs e)
        {
            base.OnLostFocus(e);
            _lostFocus = true;
        }

        protected override void OnGotFocus(RoutedEventArgs e)
        {
            base.OnGotFocus(e);

            // Only a pair counts. Enumerating every window on the system is not something to do
            // on any focus change, and focus arrives here once simply by opening the popup.
            if (_lostFocus)
            {
                _lostFocus = false;
                _items.ReplaceDiff(CaptureSessionService.FindAll());
            }
        }

        public CaptureSessionItem SelectedItem { get; private set; }

        public bool IsAudioCaptureEnabled { get; private set; }

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
                    AutomationProperties.SetName(args.ItemContainer, item.DisplayName);
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
