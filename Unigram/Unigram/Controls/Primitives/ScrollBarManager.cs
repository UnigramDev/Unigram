//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

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
