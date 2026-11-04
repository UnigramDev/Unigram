//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System.Collections.Generic;
using Telegram.Collections;
using Telegram.Controls;
using Telegram.Controls.Cells;
using Telegram.Services;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;

namespace Telegram.Views.Popups
{
    public sealed partial class ChooseCapturePopup : ContentPopup
    {
        private readonly DiffObservableCollection<CaptureSessionItem> _items;
        private readonly ThumbnailQueue _thumbnails = new();
        private readonly WindowId _excluded;

        public ChooseCapturePopup(bool canShareAudio, WindowId excluded)
        {
            InitializeComponent();

            _excluded = excluded;

            _items = new DiffObservableCollection<CaptureSessionItem>(new CaptureSessionItemDiffHandler(), detectMoves: false);
            _items.ReplaceDiff(CaptureSessionService.FindAll(excluded));

            ScrollingHost.ItemsSource = _items;
            ScrollingHost.SelectedIndex = 0;

            ShareSystemAudio.Visibility = canShareAudio
                ? Visibility.Visible
                : Visibility.Collapsed;

            Closed += OnClosed;
        }

        // The stills are released with the popup rather than whenever it is collected.
        private void OnClosed(ContentDialog sender, ContentDialogClosedEventArgs args)
        {
            Closed -= OnClosed;

            foreach (var cell in RealizedCells())
            {
                cell.UpdateCell(null, null);
            }
        }

        private IEnumerable<CaptureSessionItemCell> RealizedCells()
        {
            if (ScrollingHost.ItemsPanelRoot is not Panel panel)
            {
                yield break;
            }

            foreach (var child in panel.Children)
            {
                if (child is SelectorItem { ContentTemplateRoot: CaptureSessionItemCell cell })
                {
                    yield return cell;
                }
            }
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
                _items.ReplaceDiff(CaptureSessionService.FindAll(_excluded));

                // Cells the diff kept are not bound again, and what they show is as old as the
                // moment the user left.
                foreach (var cell in RealizedCells())
                {
                    cell.RefreshThumbnail();
                }
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
                    content.UpdateCell(null, null);
                }
                else if (args.Item is CaptureSessionItem item)
                {
                    content.UpdateCell(item, _thumbnails);
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
