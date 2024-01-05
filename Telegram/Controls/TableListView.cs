//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Telegram.Common;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;

namespace Telegram.Controls
{
    public class TableListView : SelectListView
    {
        public TableListView()
        {
            DefaultStyleKey = typeof(TableListView);

            ContainerContentChanging += OnContainerContentChanging;
            RegisterPropertyChangedCallback(SelectionModeProperty, OnSelectionModeChanged);
        }

        private void OnSelectionModeChanged(DependencyObject sender, DependencyProperty dp)
        {
            this.ForEach(OnSelectionModeChanged);
        }

        private void OnSelectionModeChanged(SelectorItem selector)
        {
            if (selector.ContentTemplateRoot is Grid content)
            {
                if (SelectionMode == ListViewSelectionMode.Multiple)
                {
                    content.Margin = new Thickness(-28, 0, 0, 0);
                    content.Padding = new Thickness(content.Padding.Right + 28, content.Padding.Top, content.Padding.Right, content.Padding.Bottom);
                }
                else
                {
                    content.Margin = new Thickness(0);
                    content.Padding = new Thickness(content.Padding.Right, content.Padding.Top, content.Padding.Right, content.Padding.Bottom);
                }
            }
        }

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }

            if (args.ItemContainer.ContentTemplateRoot is Grid content)
            {
                // TODO: some day would be great to get rid of this
                content.CornerRadius = new CornerRadius(4);
                content.BorderThickness = new Thickness(1);
                //content.Background = null;
            }
        }

        protected override DependencyObject GetContainerForItemOverride()
        {
            return new TableListViewItem();
        }
    }

    public class TableListViewItem : TextListViewItem
    {
        public TableListViewItem()
        {
            DefaultStyleKey = typeof(TableListViewItem);
        }
    }
}
