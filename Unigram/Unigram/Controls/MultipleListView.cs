using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Unigram.Controls
{
    public class MultipleListView : ListView
    {
        protected override DependencyObject GetContainerForItemOverride()
        {
            return new MultipleListViewItem();
        }
    }

    public class MultipleListViewItem : TextListViewItem
    {
        private readonly bool _multi;
        private bool _selected;

        public MultipleListViewItem(bool multi = true)
        {
            _multi = multi;
            DefaultStyleKey = typeof(MultipleListViewItem);
        }

        public bool IsSingle => !_multi;

        public void UpdateState(bool selected)
        {
            if (_selected == selected)
            {
                return;
            }

            if (ContentTemplateRoot is IMultipleElement test)
            {
                _selected = selected;
                test.UpdateState(selected, true);
            }
        }
    }

    public interface IMultipleElement
    {
        void UpdateState(bool selected, bool animate);
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
