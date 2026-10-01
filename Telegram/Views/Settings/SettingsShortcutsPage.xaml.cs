//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Controls.Media;
using Telegram.Navigation;
using Telegram.Services;
using Telegram.ViewModels.Settings;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation.Peers;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;

namespace Telegram.Views.Settings
{
    public sealed partial class SettingsShortcutsPage : HostedPage, ISearchablePage
    {
        public SettingsShortcutsViewModel ViewModel => DataContext as SettingsShortcutsViewModel;

        private readonly DispatcherTimer _renew;
        private IShortcutRecording _recording;

        public SettingsShortcutsPage()
        {
            InitializeComponent();
            Title = Strings.ShortcutsTitle;

            _renew = new DispatcherTimer
            {
                // Comfortably inside the lease, so a UI thread busy for a moment costs a renewal
                // rather than the recording.
                Interval = TimeSpan.FromMilliseconds(ShortcutsService.RecordingLease / 3)
            };

            _renew.Tick += OnRenew;
            Unloaded += OnUnloaded;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            Unloaded -= OnUnloaded;
            _renew.Tick -= OnRenew;

            // The lease would lapse on its own, but not before ten seconds of a dead keyboard.
            StopRecording();
        }

        private void OnRenew(object sender, object e)
        {
            _recording?.Renew();
        }

        #region Recording

        private void OnItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is ShortcutInfo info)
            {
                StartRecording(info);
            }
        }

        private void StartRecording(ShortcutInfo info)
        {
            ViewModel.StartRecording(info);

            _recording ??= LifetimeService.Current.Shortcuts.Record();
            _renew.Start();

            // Keys route to whatever has focus once the global layer stands down, so the row has
            // to actually hold it - a click selects a row without focusing it.
            if (ScrollingHost.ContainerFromItem(info) is ListViewItem container)
            {
                container.Focus(FocusState.Programmatic);
            }

            Announce(Strings.ShortcutsEditHint);
        }

        private void StopRecording()
        {
            ViewModel.StopRecording();

            _renew.Stop();

            _recording?.Dispose();
            _recording = null;
        }

        private async void OnPreviewKeyDown(object sender, KeyRoutedEventArgs e)
        {
            var info = ViewModel.Recording;
            if (info == null)
            {
                return;
            }

            if (e.Key == VirtualKey.Escape)
            {
                e.Handled = true;
                StopRecording();
                return;
            }

            // Tab is the other way out, and it has somewhere to go, so it keeps its own meaning.
            if (e.Key == VirtualKey.Tab)
            {
                StopRecording();
                return;
            }

            // Everything else belongs to the recording: the list must not scroll and type-ahead
            // must not run, and a modifier on its own is only the first half of a chord.
            e.Handled = true;

            _recording?.Renew();

            if (!LifetimeService.Current.Shortcuts.TryGetShortcut(e, out Shortcut shortcut))
            {
                return;
            }

            if (await ViewModel.CommitAsync(shortcut))
            {
                StopRecording();
                Announce(ConvertName(info.Command, info.Shortcut));
            }
            else
            {
                // Still recording, so the refusal has to be spoken - the row stays where it is and
                // nothing else moves to carry it.
                Announce(shortcut + ". " + ConvertConflict(info.Conflict));
            }
        }

        private void OnLosingFocus(UIElement sender, LosingFocusEventArgs args)
        {
            // The event bubbles, so moving from one row to the next raises it on the list as well
            // - including the move that starts a recording. Only focus leaving the list ends one.
            for (var parent = args.NewFocusedElement as DependencyObject; parent != null; parent = VisualTreeHelper.GetParent(parent))
            {
                if (parent == ScrollingHost)
                {
                    return;
                }
            }

            StopRecording();
        }

        private void Announce(string text)
        {
            var peer = FrameworkElementAutomationPeer.CreatePeerForElement(ScrollingHost);
            peer?.RaiseNotificationEvent(AutomationNotificationKind.Other,
                AutomationNotificationProcessing.MostRecent, text, "ShortcutRecording");
        }

        // Both go through here rather than straight to the view model, so that every way out of a
        // recording releases the lease: the menu can be opened on the row that is recording.
        private void Unbind(ShortcutInfo info)
        {
            StopRecording();
            ViewModel.Unbind(info);
        }

        private void Reset(ShortcutInfo info)
        {
            StopRecording();
            ViewModel.Reset(info);
        }

        #endregion

        private void Import_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.Import();
        }

        private void Export_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.Export();
        }

        private void ResetAll_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.ResetAll();
        }

        #region Context menu

        private void OnChoosingItemContainer(ListViewBase sender, ChoosingItemContainerEventArgs args)
        {
            if (args.ItemContainer == null)
            {
                args.ItemContainer = new TableListViewItem();
                args.ItemContainer.Style = sender.ItemContainerStyle;
                args.ItemContainer.ContentTemplate = sender.ItemTemplate;
                args.ItemContainer.ContextRequested += OnContextRequested;
            }

            args.IsContainerPrepared = true;
        }

        private void OnContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
            // The container outlives the row it is showing, so the item comes from it rather than
            // from anything captured when the handler was attached.
            if (ScrollingHost.ItemFromContainer(sender) is not ShortcutInfo info)
            {
                return;
            }

            var flyout = new MenuFlyout();

            if (info.Shortcut != null)
            {
                flyout.CreateFlyoutItem(Unbind, info, Strings.ShortcutsRemove, Icons.Delete, destructive: true);
            }

            flyout.CreateFlyoutItem(Reset, info, Strings.ShortcutsRestoreDefault, Icons.ArrowReset);

            flyout.ShowAt(sender, args);
        }

        #endregion

        // Ctrl+F reaches the page through ShortcutCommand.Search, which ProcessAppCommands hands
        // to whatever ISearchablePage is on the frame.
        public void Search()
        {
            SearchField.StartBringIntoView();
            SearchField.Focus(FocusState.Keyboard);
        }

        private void SearchField_TextChanged(object sender, TextChangedEventArgs e)
        {
            var count = ViewModel.Search(SearchField.Text);

            // Rebuilding the list says nothing to a screen reader, so the row count is the only
            // feedback it has that the query did anything.
            var peer = FrameworkElementAutomationPeer.CreatePeerForElement(SearchField);
            peer?.RaiseNotificationEvent(AutomationNotificationKind.Other,
                AutomationNotificationProcessing.MostRecent, Locale.Declension(Strings.R.ShortcutsResults, count), "ShortcutsFiltered");
        }

        /// <summary>
        /// What a row announces to a screen reader.
        /// </summary>
        /// <remarks>
        /// The keycaps are <c>AccessibilityView="Raw"</c>, so the chord only reaches a screen
        /// reader if the row says it. It is composed here rather than on ShortcutInfo, which is
        /// the binding the service hands out and has no business knowing how a row reads.
        /// </remarks>
        public static string ConvertName(ShortcutCommand command, Shortcut shortcut)
        {
            return ShortcutsService.GetLabel(command) + ", " + (shortcut?.ToString() ?? Strings.ShortcutsUnbound);
        }

        public static Visibility ConvertUnbound(bool unbound, bool recording)
        {
            return unbound && !recording ? Visibility.Visible : Visibility.Collapsed;
        }

        public static Visibility ConvertRecording(bool recording)
        {
            return recording ? Visibility.Visible : Visibility.Collapsed;
        }

        public static Visibility ConvertNotRecording(bool recording)
        {
            return recording ? Visibility.Collapsed : Visibility.Visible;
        }

        public static Visibility ConvertConflictVisibility(ShortcutConflict conflict)
        {
            return conflict.Kind != ShortcutConflictKind.None ? Visibility.Visible : Visibility.Collapsed;
        }

        public static string ConvertConflict(ShortcutConflict conflict)
        {
            if (conflict.Kind == ShortcutConflictKind.None)
            {
                return string.Empty;
            }

            if (conflict.Kind == ShortcutConflictKind.Reserved)
            {
                return conflict.Owner switch
                {
                    ShortcutOwner.MessageEditor => Strings.ShortcutsReservedMessageEditor,
                    ShortcutOwner.TextEditing => Strings.ShortcutsReservedTextEditing,
                    _ => Strings.ShortcutsReservedNavigation
                };
            }

            return string.Format(Strings.ShortcutsConflict, ShortcutsService.GetLabel(conflict.Command));
        }
    }
}
