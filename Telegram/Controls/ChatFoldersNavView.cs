//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Telegram.Controls
{
    public partial class ChatFoldersNavView : TopNavView
    {
        private readonly DispatcherTimer _switchTimer;

        private object _switchToItem;

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

        private void Container_DragEnter(object sender, DragEventArgs e)
        {
            _switchTimer.Stop();

            if (e.DataView.AvailableFormats.Count > 0)
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

        private void Container_DragLeave(object sender, DragEventArgs e)
        {
            _switchTimer.Stop();
        }

        private void Container_Drop(object sender, DragEventArgs e)
        {
            _switchTimer.Stop();
        }
    }
}
