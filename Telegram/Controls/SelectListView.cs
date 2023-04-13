//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;

namespace Telegram.Controls
{
    public class SelectListView : ListView
    {
        public SelectListView()
        {
            ContainerContentChanging += OnContainerContentChanging;
            RegisterPropertyChangedCallback(SelectionModeProperty, OnSelectionModeChanged);
        }

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }

            var content = args.ItemContainer.ContentTemplateRoot;
            if (content != null)
            {
                content.IsHitTestVisible = SelectionMode != ListViewSelectionMode.Multiple;
            }
        }

        private void OnSelectionModeChanged(DependencyObject sender, DependencyProperty dp)
        {
            var panel = ItemsPanelRoot as ItemsStackPanel;
            if (panel == null)
            {
                return;
            }

            foreach (SelectorItem container in panel.Children)
            {
                var content = container.ContentTemplateRoot;
                if (content != null)
                {
                    content.IsHitTestVisible = SelectionMode != ListViewSelectionMode.Multiple;
                }
            }
        }

        protected override void PrepareContainerForItemOverride(DependencyObject element, object item)
        {
            base.PrepareContainerForItemOverride(element, item);

            var container = element as ListViewItem;
            if (container == null)
            {
                return;
            }

            var content = container.ContentTemplateRoot;
            if (content != null)
            {
                content.IsHitTestVisible = SelectionMode != ListViewSelectionMode.Multiple;
            }
        }
    }
}
