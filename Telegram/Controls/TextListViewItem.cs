//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Telegram.Common;
using Telegram.Controls.Cells;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation.Peers;
using Windows.UI.Xaml.Controls;

namespace Telegram.Controls
{
    public partial class TextListView : ListView
    {
        protected override DependencyObject GetContainerForItemOverride()
        {
            return new TextListViewItem();
        }
    }

    public partial class TextListViewItem : ListViewItemEx
    {
        protected override AutomationPeer OnCreateAutomationPeer()
        {
            return new TextListViewItemAutomationPeer(this);
        }
    }

    public partial class TextListViewItemAutomationPeer : ListViewItemAutomationPeer
    {
        private readonly ListViewItem _owner;

        public TextListViewItemAutomationPeer(ListViewItem owner)
            : base(owner)
        {
            _owner = owner;
        }

        protected override string GetNameCore()
        {
            if (_owner.ContentTemplateRoot is ChatCell cell)
            {
                return cell.GetAutomationName() ?? base.GetNameCore();
            }

            return Automation.GetNameCore(_owner.ContentTemplateRoot ?? _owner);
        }
    }



    public partial class TextGridViewItem : GridViewItem
    {
        protected override AutomationPeer OnCreateAutomationPeer()
        {
            return new TextGridViewItemAutomationPeer(this);
        }
    }

    public partial class TextGridViewItemAutomationPeer : GridViewItemAutomationPeer
    {
        private readonly GridViewItem _owner;

        public TextGridViewItemAutomationPeer(GridViewItem owner)
            : base(owner)
        {
            _owner = owner;
        }

        protected override string GetNameCore()
        {
            return Automation.GetNameCore(_owner.ContentTemplateRoot ?? _owner);
        }
    }
}
