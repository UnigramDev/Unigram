//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using LinqToVisualTree;
using System.Collections.Generic;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media;

namespace Telegram.Controls
{
    /// <summary>
    /// Marks an element as the root of a set of <see cref="SettingsButton"/>: each one beneath it
    /// reports its position in that set, and up and down move between them.
    /// <para/>
    /// SettingsPage needs this because its buttons read as a single list but are split across a
    /// TopNavView and a couple of panels, and neither the set information a ListView derives from
    /// its items nor its arrow navigation crosses that boundary.
    /// </summary>
    public static class SettingsButtonSet
    {
        #region IsRoot

        public static bool GetIsRoot(DependencyObject obj)
        {
            return (bool)obj.GetValue(IsRootProperty);
        }

        public static void SetIsRoot(DependencyObject obj, bool value)
        {
            obj.SetValue(IsRootProperty, value);
        }

        public static readonly DependencyProperty IsRootProperty =
            DependencyProperty.RegisterAttached("IsRoot", typeof(bool), typeof(SettingsButtonSet), new PropertyMetadata(false));

        #endregion

        /// <summary>
        /// The button before or after <paramref name="owner"/>, or null at either end of the set
        /// and whenever the button isn't in one. It doesn't wrap, no more than a ListView does.
        /// </summary>
        public static SettingsButton Sibling(SettingsButton owner, bool forward)
        {
            var root = FindRoot(owner);
            if (root == null)
            {
                return null;
            }

            SettingsButton previous = null;
            var found = false;

            foreach (var button in Enumerate(root))
            {
                if (found)
                {
                    return button;
                }

                if (button == owner)
                {
                    if (!forward)
                    {
                        return previous;
                    }

                    found = true;
                }

                previous = button;
            }

            return null;
        }

        /// <summary>
        /// The zero-based position of <paramref name="owner"/> and the size of the set it is in,
        /// or false when it is in none.
        /// </summary>
        public static bool TryGetPosition(SettingsButton owner, out int index, out int count)
        {
            index = -1;
            count = 0;

            var root = FindRoot(owner);
            if (root == null)
            {
                return false;
            }

            foreach (var button in Enumerate(root))
            {
                if (button == owner)
                {
                    index = count;
                }

                count++;
            }

            return index >= 0;
        }

        private static DependencyObject FindRoot(SettingsButton owner)
        {
            foreach (var parent in owner.Ancestors())
            {
                if (GetIsRoot(parent))
                {
                    return parent;
                }
            }

            return null;
        }

        // Walked on demand rather than stamped on each button: nothing raises a callback when a
        // group is loaded or an option hidden, so a stored index would go stale with nothing to
        // refresh it. ListRadioButtonAutomationPeer computes its own for the same reason.
        private static IEnumerable<SettingsButton> Enumerate(DependencyObject parent)
        {
            var count = VisualTreeHelper.GetChildrenCount(parent);

            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);

                // A collapsed group is neither read nor reachable, so it isn't part of the set.
                if (child is UIElement element && element.Visibility == Visibility.Collapsed)
                {
                    continue;
                }

                if (child is SettingsButton button)
                {
                    // No button nests another, so the walk stops rather than descend a template.
                    yield return button;
                }
                else
                {
                    foreach (var nested in Enumerate(child))
                    {
                        yield return nested;
                    }
                }
            }
        }
    }
}
