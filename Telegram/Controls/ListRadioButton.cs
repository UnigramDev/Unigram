//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Telegram.Common;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation.Peers;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Input;

namespace Telegram.Controls
{
    // A SettingsRadioButton meant to be the content root of a ListViewItem whose container
    // is not a tab stop: it reports the name and the set position that the container's own
    // peer would otherwise provide, so that focus can land here without losing either.
    public partial class ListRadioButton : SettingsRadioButton
    {
        protected override AutomationPeer OnCreateAutomationPeer()
        {
            return new ListRadioButtonAutomationPeer(this);
        }

        protected override void OnKeyDown(KeyRoutedEventArgs e)
        {
            // RadioButton handles up and down internally to move between group elements
            if (e.Key is not VirtualKey.Up and not VirtualKey.Down)
            {
                base.OnKeyDown(e);
            }
        }
    }

    public partial class ListRadioButtonAutomationPeer : RadioButtonAutomationPeer
    {
        private readonly ListRadioButton _owner;

        public ListRadioButtonAutomationPeer(ListRadioButton owner)
            : base(owner)
        {
            _owner = owner;
        }

        // Same concatenation TextListViewItemAutomationPeer applies to the container, so a row
        // that reads as one name today keeps reading as one name.
        protected override string GetNameCore()
        {
            if (_owner.Content is UIElement content)
            {
                return Automation.GetNameCore(content) ?? base.GetNameCore();
            }

            return base.GetNameCore();
        }

        // Computed on demand rather than stamped through AutomationProperties.PositionInSet:
        // a ListView raises no callback when an insertion or a removal shifts the index of an
        // already realized container, so a stamped value goes stale and there is nothing to
        // hook to refresh it.
        protected override int GetPositionInSetCore()
        {
            var index = IndexOfSet(out _);
            return index < 0
                ? base.GetPositionInSetCore()
                : index + 1;
        }

        protected override int GetSizeOfSetCore()
        {
            // The set is the list alone: radio buttons that share the group from outside of it,
            // as SettingsProxyPage's header ones do, are deliberately not counted.
            if (IndexOfSet(out var itemsControl) < 0)
            {
                return base.GetSizeOfSetCore();
            }

            return itemsControl.Items.Count;
        }

        private int IndexOfSet(out ItemsControl itemsControl)
        {
            var container = _owner.GetParent<SelectorItem>();

            itemsControl = container != null
                ? ItemsControl.ItemsControlFromItemContainer(container)
                : null;

            return itemsControl?.IndexFromContainer(container) ?? -1;
        }
    }
}
