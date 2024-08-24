//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using LinqToVisualTree;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using System.Text;
using Telegram.Controls.Cells;

namespace Telegram.Controls
{
    public class TextListView : ListView
    {
        protected override DependencyObject GetContainerForItemOverride()
        {
            return new TextListViewItem();
        }
    }

    public class TextListViewItem : ListViewItem
    {
        protected override AutomationPeer OnCreateAutomationPeer()
        {
            return new TextListViewItemAutomationPeer(this);
        }
    }

    public class TextListViewItemAutomationPeer : ListViewItemAutomationPeer
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

            var builder = new StringBuilder();
            var descendants = (_owner.ContentTemplateRoot ?? _owner).DescendantsAndSelf<TextBlock>();

            foreach (TextBlock child in descendants)
            {
                var view = AutomationProperties.GetAccessibilityView(child);
                if (view == AccessibilityView.Raw)
                {
                    continue;
                }

                if (builder.Length > 0)
                {
                    builder.Append(", ");
                }

                builder.Append(child.Text);
            }

            return builder.ToString();
        }
    }



    public class TextGridViewItem : GridViewItem
    {
        protected override AutomationPeer OnCreateAutomationPeer()
        {
            return new TextGridViewItemAutomationPeer(this);
        }
    }

    public class TextGridViewItemAutomationPeer : GridViewItemAutomationPeer
    {
        private readonly GridViewItem _owner;

        public TextGridViewItemAutomationPeer(GridViewItem owner)
            : base(owner)
        {
            _owner = owner;
        }

        protected override string GetNameCore()
        {
            var builder = new StringBuilder();
            var descendants = (_owner.ContentTemplateRoot ?? _owner).DescendantsAndSelf<TextBlock>();

            foreach (TextBlock child in descendants)
            {
                var view = AutomationProperties.GetAccessibilityView(child);
                if (view == AccessibilityView.Raw)
                {
                    continue;
                }

                if (builder.Length > 0)
                {
                    builder.Append(", ");
                }

                builder.Append(child.Text);
            }

            return builder.ToString();
        }
    }
}
