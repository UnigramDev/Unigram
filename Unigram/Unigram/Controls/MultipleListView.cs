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
        private bool _selected;

        public MultipleListViewItem()
        {
            DefaultStyleKey = typeof(MultipleListViewItem);
        }

        public void UpdateState(bool selected)
        {
            if (_selected == selected)
            {
                return;
            }

            if (ContentTemplateRoot is IMultipleElement test)
            {
                test.UpdateState(selected, true);
            }

            _selected = selected;
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
                selector.UpdateState(_multi && selector.IsSelected);
            }
            else if (_multi && stateName.EndsWith("Selected"))
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
