//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Telegram.Common;
using Telegram.Controls.Cells;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;

namespace Telegram.Controls
{
    public partial class TableListView : SelectListView
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
            return;

            if (selector.ContentTemplateRoot is Grid grid)
            {
                if (SelectionMode == ListViewSelectionMode.Multiple)
                {
                    grid.Margin = new Thickness(-28, 0, 0, 0);
                    grid.Padding = new Thickness(grid.Padding.Right + 28, grid.Padding.Top, grid.Padding.Right, grid.Padding.Bottom);
                }
                else
                {
                    grid.Margin = new Thickness(0);
                    grid.Padding = new Thickness(grid.Padding.Right, grid.Padding.Top, grid.Padding.Right, grid.Padding.Bottom);
                }
            }
            else if (selector.ContentTemplateRoot is ProfileCell profileCell)
            {
                if (SelectionMode == ListViewSelectionMode.Multiple)
                {
                    profileCell.Margin = new Thickness(-28, 0, 0, 0);
                    profileCell.Padding = new Thickness(profileCell.Padding.Right + 28, profileCell.Padding.Top, profileCell.Padding.Right, profileCell.Padding.Bottom);
                }
                else
                {
                    profileCell.Margin = new Thickness(0);
                    profileCell.Padding = new Thickness(profileCell.Padding.Right, profileCell.Padding.Top, profileCell.Padding.Right, profileCell.Padding.Bottom);
                }
            }
        }

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            return;

            if (args.InRecycleQueue)
            {
                return;
            }

            if (ItemContainerCornerRadius.TopLeft > 0)
            {
                if (args.ItemContainer.ContentTemplateRoot is Grid grid)
                {
                    // TODO: some day would be great to get rid of this
                    grid.CornerRadius = new CornerRadius(4);
                    grid.BorderThickness = new Thickness(1);
                    //content.Background = null;
                }
                else if (args.ItemContainer.ContentTemplateRoot is ProfileCell profileCell)
                {
                    // TODO: some day would be great to get rid of this
                    profileCell.CornerRadius = new CornerRadius(4);
                    profileCell.BorderThickness = new Thickness(1);
                    //content.Background = null;
                }
            }
        }

        protected override DependencyObject GetContainerForItemOverride()
        {
            return new TableListViewItem();
        }

        #region ItemContainerCornerRadius

        public CornerRadius ItemContainerCornerRadius
        {
            get { return (CornerRadius)GetValue(ItemContainerCornerRadiusProperty); }
            set { SetValue(ItemContainerCornerRadiusProperty, value); }
        }

        public static readonly DependencyProperty ItemContainerCornerRadiusProperty =
            DependencyProperty.Register("ItemContainerCornerRadius", typeof(CornerRadius), typeof(TableListView), new PropertyMetadata(default(CornerRadius)));

        #endregion
    }

    public partial class TableListViewItem : TextListViewItem
    {
        public TableListViewItem()
        {
            DefaultStyleKey = typeof(TableListViewItem);
        }
    }

    public partial class TableListViewItemVisualStateManager : VisualStateManager
    {
        protected override bool GoToStateCore(Control control, FrameworkElement templateRoot, string stateName, VisualStateGroup group, VisualState state, bool useTransitions)
        {
            if (stateName == "MultiSelectDisabled" && group.CurrentState == null)
            {
                return false;
            }

            return base.GoToStateCore(control, templateRoot, stateName, group, state, useTransitions);
        }
    }
}
