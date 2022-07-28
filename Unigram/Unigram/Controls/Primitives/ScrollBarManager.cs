using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace Unigram.Controls.Primitives
{
    public class ScrollBarManager : VisualStateManager
    {
        protected override bool GoToStateCore(Control control, FrameworkElement templateRoot, string stateName, VisualStateGroup group, VisualState state, bool useTransitions)
        {
            if (group?.Name == "ConsciousStates" && group?.CurrentState?.Name == "Expanded" && stateName == "Collapsed")
            {
                var parent1 = control.Parent as Grid;
                var parent2 = parent1?.Parent as Grid;
                var parent3 = parent2?.Parent as Border;

                if (parent3 != null)
                {
                    var scrollViewer = VisualTreeHelper.GetParent(parent3) as ScrollViewer;
                    if (scrollViewer != null)
                    {
                        GoToState(scrollViewer, "NoIndicator", true);
                    }
                }
            }

            return base.GoToStateCore(control, templateRoot, stateName, group, state, useTransitions);
        }
    }
}
