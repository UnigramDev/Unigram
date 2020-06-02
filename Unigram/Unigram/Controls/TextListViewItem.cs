using LinqToVisualTree;
using System.Text;
using Unigram.Controls.Cells;
using Windows.UI.Xaml.Automation.Peers;
using Windows.UI.Xaml.Controls;

namespace Unigram.Controls
{
    public class TextListViewItem : ListViewItem
    {
        protected override AutomationPeer OnCreateAutomationPeer()
        {
            return new TextListViewItemAutomationPeer(this);
        }
    }

    public class TextListViewItemAutomationPeer : ListViewItemAutomationPeer
    {
        private ListViewItem _owner;

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
        private GridViewItem _owner;

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
