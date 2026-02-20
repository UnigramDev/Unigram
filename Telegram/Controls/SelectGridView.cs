//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;

namespace Telegram.Controls
{
    public partial class SelectGridView : GridView
    {
        public SelectGridView()
        {
            ContainerContentChanging += OnContainerContentChanging;
            RegisterPropertyChangedCallback(SelectionModeProperty, OnSelectionModeChanged);
        }

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            var content = args.ItemContainer.ContentTemplateRoot;
            content?.IsHitTestVisible = SelectionMode == ListViewSelectionMode.None;
        }

        private void OnSelectionModeChanged(DependencyObject sender, DependencyProperty dp)
        {
            var panel = ItemsPanelRoot as ItemsWrapGrid;
            if (panel == null)
            {
                return;
            }

            foreach (SelectorItem container in panel.Children)
            {
                var content = container.ContentTemplateRoot;
                content?.IsHitTestVisible = SelectionMode != ListViewSelectionMode.Multiple;
            }
        }
    }
}
