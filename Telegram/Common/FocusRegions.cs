//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System.Collections.Generic;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation;
using Windows.UI.Xaml.Automation.Peers;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;

namespace Telegram.Common
{
    /// <summary>
    /// F6 and Shift+F6 move between the regions of a window: the chat list, the messages, the
    /// composer, and whatever else is marked with a landmark.
    /// </summary>
    public static class FocusRegions
    {
        /// <summary>
        /// Moves focus to the region after (or before) the one that holds it.
        /// </summary>
        public static bool MoveFocus(DependencyObject root, XamlRoot xamlRoot, bool backwards)
        {
            var regions = new List<Control>();
            Collect(root, regions);

            if (regions.Count == 0)
            {
                return false;
            }

            var step = backwards ? -1 : 1;
            var index = IndexOfRegion(regions, FocusManagerEx.TryGetFocusedElement(xamlRoot) as DependencyObject);

            // Focus outside every region - a flyout, or nothing focused at all - starts the cycle
            // from the end it is about to travel towards.
            var next = index < 0
                ? (backwards ? regions.Count - 1 : 0)
                : index + step;

            for (int i = 0; i < regions.Count; i++)
            {
                var region = regions[(next % regions.Count + regions.Count) % regions.Count];
                if (region.Focus(FocusState.Keyboard))
                {
                    return true;
                }

                // A region can refuse focus, an empty list being the usual one.
                next += step;
            }

            return false;
        }

        /// <summary>
        /// Collects the landmarks in tree order, without descending into one: the walk never
        /// reaches the realized children of a list, which is what makes it cheap enough for a key.
        /// </summary>
        private static void Collect(DependencyObject parent, List<Control> regions)
        {
            var count = VisualTreeHelper.GetChildrenCount(parent);

            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);

                if (child is FrameworkElement element && element.Visibility == Visibility.Collapsed)
                {
                    continue;
                }

                if (child is Control control && control.IsEnabled && AutomationProperties.GetLandmarkType(control) != AutomationLandmarkType.None)
                {
                    regions.Add(control);
                    continue;
                }

                Collect(child, regions);
            }
        }

        private static int IndexOfRegion(List<Control> regions, DependencyObject focused)
        {
            while (focused != null)
            {
                if (focused is Control control)
                {
                    var index = regions.IndexOf(control);
                    if (index >= 0)
                    {
                        return index;
                    }
                }

                focused = VisualTreeHelper.GetParent(focused);
            }

            return -1;
        }
    }
}
