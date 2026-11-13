//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Telegram.Controls
{
    public partial class ChatFoldersNavView : TopNavView
    {
        private readonly DispatcherTimer _switchTimer;

        private object _switchToItem;

        // The container a drag is over: DragLeave can arrive while a deferred DragEnter still waits.
        private object _dragContainer;

        public ChatFoldersNavView()
        {
            _switchTimer = new DispatcherTimer();
            _switchTimer.Interval = TimeSpan.FromSeconds(2);
            _switchTimer.Tick += OnTick;

            DragItemsStarting += OnDragItemsStarting;
            DragItemsCompleted += OnDragItemsCompleted;
        }

        private void OnDragItemsStarting(object sender, DragItemsStartingEventArgs e)
        {
            if (ItemsPanelRoot is not Panel panel)
            {
                return;
            }

            foreach (var container in panel.Children)
            {
                container.AllowDrop = false;
            }
        }

        private void OnDragItemsCompleted(ListViewBase sender, DragItemsCompletedEventArgs args)
        {
            if (ItemsPanelRoot is not Panel panel)
            {
                return;
            }

            foreach (var container in panel.Children)
            {
                container.AllowDrop = true;
            }
        }

        private void OnTick(object sender, object e)
        {
            _switchTimer.Stop();

            if (_switchToItem != null)
            {
                SelectedItem = _switchToItem;
            }
        }

        protected override DependencyObject GetContainerForItemOverride()
        {
            var container = base.GetContainerForItemOverride() as TopNavViewItem;
            container.AllowDrop = true;
            container.DragEnter += Container_DragEnter;
            container.DragLeave += Container_DragLeave;
            container.Drop += Container_Drop;

            return container;
        }

        private async void Container_DragEnter(object sender, DragEventArgs e)
        {
            _switchTimer.Stop();
            _dragContainer = sender;

            // Reading the DataView is a synchronous call into the drag source, and inside a drag event XAML's
            // reentrancy guard fail-fasts on any message pumped while it waits, so leave the event first.
            var deferral = e.GetDeferral();
            await Task.Yield();

            try
            {
                if (_dragContainer != sender)
                {
                    return;
                }

                if (HasFormats(e.DataView))
                {
                    _switchToItem = ItemFromContainer(sender as DependencyObject);
                    _switchTimer.Start();
                    e.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Copy;
                }
                else
                {
                    _switchToItem = null;
                    e.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.None;
                }
            }
            finally
            {
                deferral.Complete();
            }
        }

        private static bool HasFormats(DataPackageView data)
        {
            try
            {
                return data.AvailableFormats.Count > 0;
            }
            catch
            {
                // A drag from some sources fails to list its formats with ERROR_NOT_FOUND.
                return false;
            }
        }

        private void Container_DragLeave(object sender, DragEventArgs e)
        {
            _switchTimer.Stop();

            if (_dragContainer == sender)
            {
                _dragContainer = null;
            }
        }

        private void Container_Drop(object sender, DragEventArgs e)
        {
            _switchTimer.Stop();

            if (_dragContainer == sender)
            {
                _dragContainer = null;
            }
        }
    }
}
