//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Telegram.Controls
{
    public class MultipleListView : ListView
    {
        protected override DependencyObject GetContainerForItemOverride()
        {
            return new MultipleListViewItem(this);
        }
    }

    public class MultipleListViewItem : TableListViewItem
    {
        private readonly ListViewBase _owner;
        private readonly bool _multi;
        private bool _enabled;
        private bool _selected;

        public MultipleListViewItem(ListViewBase owner, bool multi = true)
        {
            _owner = owner;
            _multi = multi;
            _enabled = owner.SelectionMode == ListViewSelectionMode.Multiple;
            DefaultStyleKey = typeof(MultipleListViewItem);
        }

        public bool IsSingle => !_multi;

        public void UpdateState(bool selected)
        {
            var enabled = _owner.SelectionMode == ListViewSelectionMode.Multiple;
            if (enabled == _enabled && _selected == selected)
            {
                return;
            }

            if (ContentTemplateRoot is IMultipleElement test)
            {
                _selected = selected;
                _enabled = enabled;
                test.UpdateState(selected, true, enabled);
            }
        }
    }

    public interface IMultipleElement
    {
        void UpdateState(bool selected, bool animate, bool multiple);
    }

    public class MultipleVisualStateManager : VisualStateManager
    {
        private bool _multi;

        protected override bool GoToStateCore(Control control, FrameworkElement templateRoot, string stateName, VisualStateGroup group, VisualState state, bool useTransitions)
        {
            var selector = control as MultipleListViewItem;
            if (selector == null)
            {
                return false;
            }

            if (group.Name == "MultiSelectStates")
            {
                _multi = stateName == "MultiSelectEnabled";
                selector.UpdateState((_multi || selector.IsSingle) && selector.IsSelected);
            }
            else if ((_multi || selector.IsSingle) && stateName.EndsWith("Selected"))
            {
                stateName = stateName.Replace("Selected", string.Empty);

                if (string.IsNullOrEmpty(stateName))
                {
                    stateName = "Normal";
                }
            }

            return base.GoToStateCore(control, templateRoot, stateName, group, state, useTransitions);
        }
    }
}
